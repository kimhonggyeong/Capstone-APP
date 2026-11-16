import fs from 'node:fs/promises';
import {FileBlob, PresentationFile} from 'file:///C:/Users/hk010/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const source='C:/Users/hk010/OneDrive/Documents/카카오톡 받은 파일/[Template 12] 전시회 판넬 [C-3조] ANTITUDE_시나리오 기반 AI 투자 판단 학습 플랫폼.pptx';
const p=await PresentationFile.importPptx(await FileBlob.load(source));
const report=await p.inspect({select:'$.slides[*]',include:['id','index','layout','elements.id','elements.kind','elements.name','elements.text','elements.position','elements.style']});
await fs.writeFile(new URL('./source-inspect.ndjson',import.meta.url), report.ndjson);
console.log('SLIDES', p.slides.items.length);
console.log('MASTERS',p.masters.items.length);
console.log('LAYOUTS',p.layouts.items.length);
for(const [i,s] of p.slides.items.entries()){
  const png=await s.export({format:'png',scale:0.7});
  await fs.writeFile(new URL(`./source-${i+1}.png`,import.meta.url),new Uint8Array(await png.arrayBuffer()));
  const layout=await s.export({format:'layout',detail:'full'});
  await fs.writeFile(new URL(`./source-${i+1}.json`,import.meta.url),await layout.text());
}
console.log(report.ndjson);
