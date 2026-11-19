// Offline audit only: executes selected source functions with in-memory mocks.
// No server startup, database client, network calls, or real orders are used.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const compilerRoot = 'C:/Users/hk010/PycharmProjects/PythonProject5/Capstone-ver0.1-main';
const root = process.argv[2] || compilerRoot;
const ts = require(compilerRoot + '/server/node_modules/typescript');
const path = root + '/server/src/services/trading.service.ts';
const source = fs.readFileSync(path, 'utf8');
const parsed = ts.createSourceFile(path, source, ts.ScriptTarget.Latest, true);
const wanted = new Set(['getUserId', 'getOrCreateAccount', 'upsertBuyHolding', 'checkPendingOrders',
  'getOrCreateAccountUnlocked', 'checkPendingOrdersUnlocked']);
const functions = parsed.statements.filter(statement =>
  ts.isVariableStatement(statement) && statement.declarationList.declarations.some(
    declaration => ts.isIdentifier(declaration.name) && wanted.has(declaration.name.text)
  )
).map(statement => statement.getText(parsed)).join('\n');
const selectedCount = parsed.statements.filter(statement => ts.isVariableStatement(statement) &&
  statement.declarationList.declarations.some(declaration =>
    ts.isIdentifier(declaration.name) && wanted.has(declaration.name.text))).length;
const lockPath = root + '/server/src/services/accountMutationLock.ts';
const hasAccountLock = fs.existsSync(lockPath);
assert.equal(selectedCount, hasAccountLock ? 6 : 4);

const clone = value => JSON.parse(JSON.stringify(value));
const state = {
  account: { userId: 'demo-user', cash: 1000, reservedCash: 100 },
  holding: null,
  order: { _id: 'offline-order', userId: 'demo-user', symbol: '005930', name: 'Offline test',
    market: 'KRX', side: 'BUY', orderType: 'LIMIT', status: 'PENDING',
    quantity: 1, filledQuantity: 0, limitPrice: 100, reservedAmount: 100, reservedQuantity: 0 },
};
function document(name) {
  const copy = clone(state[name]);
  copy.save = async () => {
    const saved = { ...copy };
    delete saved.save;
    state[name] = saved;
    return copy;
  };
  return copy;
}
let simulatedAppFills = 0;
const sandbox = {
  exports: {}, console, DEFAULT_INITIAL_CASH: 0,
  getKrxMarketStatus: () => ({ isOpen: true, orderAllowedByOverride: false }),
  TradeOrder: { find: () => ({ sort: async () => [document('order')] }) },
  TradingAccount: { findOne: async () => document('account') },
  Holding: { findOne: async () => state.holding ? document('holding') : null },
  getCurrentQuote: async () => {
    // App commits a fill after the web read PENDING, before it receives its quote.
    simulatedAppFills++;
    state.account.cash -= 100;
    state.account.reservedCash = 0;
    state.holding = { userId: 'demo-user', symbol: '005930', quantity: 1,
      avgPrice: 100, reservedQuantity: 0, totalBuyAmount: 100 };
    state.order.status = 'FILLED';
    state.order.filledQuantity = 1;
    state.order.reservedAmount = 0;
    return { price: 100 };
  },
};
if (hasAccountLock) {
  const lockModule = { exports: {}, require: name => {
    assert.equal(name, 'async_hooks');
    return require('node:async_hooks');
  } };
  const lockCode = ts.transpileModule(fs.readFileSync(lockPath, 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 },
  }).outputText;
  vm.runInNewContext(lockCode, lockModule, { filename: lockPath });
  sandbox.withAccountMutation = lockModule.exports.withAccountMutation;
}
const code = ts.transpileModule(functions, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 },
}).outputText;
vm.runInNewContext(code, sandbox, { filename: path });
(async () => {
  const result = await sandbox.exports.checkPendingOrders('demo-user');
  assert.equal(simulatedAppFills, 1);
  assert.equal(result.filledCount, 1);
  assert.equal(state.account.cash, 800);
  assert.equal(state.holding.quantity, 2);
  console.log(JSON.stringify({
    scope: 'offline mocks only; no real data accessed',
    source_root: root,
    actual_account_mutation_lock_included: hasAccountLock,
    expected_after_one_order: { cash: 900, quantity: 1 },
    observed_after_app_fill_then_web_continues: { cash: state.account.cash, quantity: state.holding.quantity },
    finding: 'web continues filling the stale PENDING document after another process has filled it',
  }));
})().catch(error => { console.error(error); process.exitCode = 1; });
