using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// Put on an always-active manager, NOT on the hidden correction tab.
public class OrderListLoader : MonoBehaviour
{
    public StockPanelManager panelManager;
    public StockCandlestickLoader stockLoader;
    public TradeMyData myData;
    [Header("주문 프리팹 목록")]
    public Transform content;
    public OrderRowUI rowPrefab;
    public bool currentSymbolOnly = true;
    public bool pendingOnly = false;
    public float refreshSeconds = 5f;
    [Header("선택 주문 상세 / 정정 입력")]
    public TMP_Text selectedOrderText;
    [Header("선택한 주문 상세: 각각 값 텍스트 연결")]
    public TMP_Text selectedSideText;
    public TMP_Text selectedStockNameText;
    public TMP_Text selectedOrderIdText;
    public TMP_Text selectedOrderPriceText;
    public TMP_Text selectedOrderQuantityText;
    public TMP_Text selectedRemainingQuantityText;
    public TMP_Text selectedOrderStatusText;
    public TMP_Text selectedOrderTypeText;
    public TMP_Text selectedOrderTimeText;
    public TMP_Text selectedValidityText;
    [Header("선택 주문 종목의 현재가 정보")]
    public TMP_Text selectedCurrentPriceText;
    public TMP_Text selectedChangeText;
    public TMP_Text selectedVolumeText;
    public TMP_Text messageText;
    public Button amendSelectedButton, cancelSelectedButton;
    [Header("입력 가능한 주문 정정 확인창")]
    public GameObject amendPanel;
    public TMP_Text amendPanelStockText;
    public TMP_InputField amendPanelPriceInput, amendPanelQuantityInput;
    public TMP_Text amendPanelAvailableQuantityText, amendPanelAvailableCashText;
    public TMP_Text amendPanelEstimatedAmountText, amendPanelNoticeText;
    public Button amendPanelConfirmButton, amendPanelCancelButton;
    public int amendPriceStep = 1;
    public bool IsRequesting { get; private set; }
    public TradeOrderData SelectedOrder { get; private set; }

    readonly List<OrderRowUI> rows = new List<OrderRowUI>();
    bool refreshing, refreshQueued;
    float nextRefresh;
    string sideFilter = "", observedSymbol;
    string pendingTopUpRequestId;
    string quoteSymbol;
    bool quoteLoading;
    float nextQuoteRefresh;
    TradeOrderData editingOrder;
    PortfolioResponse editingPortfolio;
    int editorGeneration, submittedAmendPrice, submittedAmendQuantity;
    string submittedAmendId;
    bool amendmentPortfolioLoading;
    bool amendmentOrderChanged;
    string Symbol => stockLoader != null ? stockLoader.symbol : "";

    void Start()
    {
        if (panelManager == null) panelManager = GetComponent<StockPanelManager>();
        if (panelManager != null)
        {
            panelManager.orderListLoader = this;
            if (stockLoader == null) stockLoader = panelManager.stockLoader;
        }
        if (myData == null) myData = FindFirstObjectByType<TradeMyData>(FindObjectsInactive.Include);
        if (stockLoader != null) stockLoader.TradeCompleted += Refresh;
        observedSymbol = Symbol;
        ClearSelectedDetails();
        if (amendPanel != null) amendPanel.SetActive(false);
        Refresh();
    }
    void OnDestroy()
    {
        if (stockLoader != null) stockLoader.TradeCompleted -= Refresh;
    }
    void Update()
    {
        UpdateAmendEditor();
        if (observedSymbol != Symbol)
        { observedSymbol = Symbol; SelectedOrder = null; ClearSelectedDetails(); refreshQueued = true; }
        if (!IsRequesting && !refreshing && (refreshQueued || Time.unscaledTime >= nextRefresh)) Refresh();
        bool editable = !IsRequesting && SelectedOrder != null && SelectedOrder.CanEdit;
        if (amendSelectedButton != null) amendSelectedButton.interactable = editable;
        if (cancelSelectedButton != null) cancelSelectedButton.interactable = editable;
        foreach (var row in rows) if (row != null) row.SetInteractable(!IsRequesting && editingOrder == null);
        if (SelectedOrder != null && !quoteLoading && Time.unscaledTime >= nextQuoteRefresh &&
            (selectedCurrentPriceText != null || selectedChangeText != null || selectedVolumeText != null))
            StartCoroutine(LoadSelectedQuote());
    }
    void Message(string value)
    {
        if (messageText != null) messageText.text = value;
        if (stockLoader != null) stockLoader.ReportTradeMessage(value);
        else Debug.Log("[OrderList] " + value);
    }
    public void FilterAll() { sideFilter = ""; Refresh(); }
    public void FilterBuy() { sideFilter = "BUY"; Refresh(); }
    public void FilterSell() { sideFilter = "SELL"; Refresh(); }
    public void Refresh()
    {
        if (IsRequesting || refreshing) { refreshQueued = true; return; }
        refreshQueued = false;
        nextRefresh = Time.unscaledTime + Math.Max(2f, refreshSeconds);
        StartCoroutine(LoadRows());
    }
    IEnumerator LoadRows()
    {
        refreshing = true;
        string capturedSymbol = Symbol, capturedSide = sideFilter;
        string path = "/orders?limit=100";
        if (currentSymbolOnly && capturedSymbol != "") path += "&symbol=" + Uri.EscapeDataString(capturedSymbol);
        if (capturedSide != "") path += "&side=" + capturedSide;
        if (pendingOnly) path += "&status=PENDING";
        yield return Send("GET", path, null, response =>
        {
            if (capturedSymbol != Symbol || capturedSide != sideFilter) { refreshQueued = true; return; }
            var data = response.ToObject<TradeOrdersResponse>();
            if (data == null || !data.success) { Message("주문목록 응답이 올바르지 않습니다."); return; }
            if (editingOrder != null)
            {
                var latest = Array.Find(data.data ?? Array.Empty<TradeOrderData>(), o => o._id == editingOrder._id);
                if (latest != null && (!latest.CanEdit || latest.version != editingOrder.version)) amendmentOrderChanged = true;
            }
            if (content == null || rowPrefab == null) return; // Inspector connection needed before drawing.
            string selectedId = SelectedOrder?._id;
            int oldVersion = SelectedOrder?.version ?? -1;
            foreach (var row in rows) if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            rows.Clear();
            SelectedOrder = null;
            foreach (var order in data.data ?? Array.Empty<TradeOrderData>())
            {
                var row = Instantiate(rowPrefab, content);
                row.Bind(order, SelectOrder, o => { SelectOrder(o); OpenAmendConfirmation(); },
                    o => { SelectOrder(o); OpenCancelConfirmation(); });
                rows.Add(row);
                if (order._id == selectedId) Select(order, oldVersion != order.version);
            }
            if (SelectedOrder == null) ClearSelectedDetails();
        });
        refreshing = false;
    }
    public void SelectOrder(TradeOrderData order) { if (!IsRequesting && editingOrder == null) Select(order, true); }
    void Select(TradeOrderData order, bool fillInputs)
    {
        SelectedOrder = order;
        foreach (var row in rows) row.SetSelected(row.Order._id == order._id);
        if (selectedOrderText != null) selectedOrderText.text =
            $"{order.name} ({order.symbol})\n{order.SideLabel} / {order.StatusLabel}\n" +
            $"주문가격 {order.orderPrice:N0}원\n주문수량 {order.quantity:N0}주\n" +
            $"미체결수량 {Math.Max(0, order.quantity - order.filledQuantity):N0}주\n" +
            $"주문유형 {(order.orderType == "LIMIT" ? "지정가" : "시장가")}\n주문시각 {OrderTimeFormat.Korea(order.createdAt)}";
        SetText(selectedSideText, order.SideLabel);
        if (selectedSideText != null) selectedSideText.color = order.side == "BUY" ? Color.red : Color.blue;
        SetText(selectedStockNameText, order.name + " (" + order.symbol + ")");
        SetText(selectedOrderIdText, order._id);
        SetText(selectedOrderPriceText, $"{order.orderPrice:N0}원");
        SetText(selectedOrderQuantityText, $"{order.quantity:N0}주");
        SetText(selectedRemainingQuantityText, $"{Math.Max(0, order.quantity - order.filledQuantity):N0}주");
        SetText(selectedOrderStatusText, order.StatusLabel);
        SetText(selectedOrderTypeText, order.orderType == "LIMIT" ? "지정가" : "시장가");
        SetText(selectedOrderTimeText, OrderTimeFormat.Korea(order.createdAt));
        // No expiration policy exists in the current server. Do not invent a date.
        SetText(selectedValidityText, "만료 미설정");
        if (quoteSymbol != order.symbol)
        {
            quoteSymbol = order.symbol;
            nextQuoteRefresh = 0;
            ClearQuoteDetails();
        }
    }
    static void SetText(TMP_Text text, string value) { if (text != null) text.text = value; }
    void ClearQuoteDetails()
    {
        SetText(selectedCurrentPriceText, "-");
        SetText(selectedChangeText, "-");
        if (selectedChangeText != null) selectedChangeText.color = Color.black;
        SetText(selectedVolumeText, "-");
    }
    void ClearSelectedDetails()
    {
        SetText(selectedOrderText, "주문을 선택해 주세요.");
        foreach (var text in new[] { selectedSideText, selectedStockNameText, selectedOrderIdText,
            selectedOrderPriceText, selectedOrderQuantityText, selectedRemainingQuantityText,
            selectedOrderStatusText, selectedOrderTypeText, selectedOrderTimeText, selectedValidityText })
            SetText(text, "-");
        quoteSymbol = null;
        nextQuoteRefresh = 0;
        ClearQuoteDetails();
    }
    IEnumerator LoadSelectedQuote()
    {
        quoteLoading = true;
        string symbol = SelectedOrder.symbol;
        // Public market-data endpoint: do not require an account token here.
        using (var request = UnityWebRequest.Get(ServerConfig.HttpBaseUrl + "/stock-info?symbol=" + Uri.EscapeDataString(symbol)))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (SelectedOrder != null && SelectedOrder.symbol == symbol)
            {
                StockInfoResponse info = null;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try { info = JsonConvert.DeserializeObject<StockInfoResponse>(request.downloadHandler.text); }
                    catch (Exception error) { Debug.LogWarning("[OrderList] 시세 파싱 실패: " + error.Message); }
                }
                if (info != null && info.price > 0 && info.symbol == symbol)
                {
                    SetText(selectedCurrentPriceText, $"{info.price:N0}원");
                    string sign = info.change > 0 ? "+" : "";
                    SetText(selectedChangeText, $"{sign}{info.change:N0}원 ({info.change_rate:+0.00;-0.00;0.00}%)");
                    if (selectedChangeText != null) selectedChangeText.color =
                        info.change > 0 ? Color.red : info.change < 0 ? Color.blue : Color.black;
                    SetText(selectedVolumeText, $"{info.volume:N0}주");
                }
                else ClearQuoteDetails();
            }
        }
        quoteLoading = false;
        nextQuoteRefresh = quoteSymbol == symbol ? Time.unscaledTime + Math.Max(2f, refreshSeconds) : 0;
    }
    bool Read(TMP_InputField input, out int value)
    {
        value = 0;
        return input != null && int.TryParse(input.text.Replace(",", "").Trim(), out value) && value > 0;
    }
    public void OpenAmendConfirmation()
    {
        if (editingOrder != null) return;
        if (panelManager == null || panelManager.IsOrderBusy || SelectedOrder == null || !SelectedOrder.CanEdit) return;
        if (amendPanel == null || amendPanelPriceInput == null || amendPanelQuantityInput == null)
        { Message("정정 확인창과 창 안의 가격·수량 입력칸을 연결하세요."); return; }
        editingOrder = SelectedOrder;
        editingPortfolio = null;
        submittedAmendId = null;
        editorGeneration++;
        amendmentPortfolioLoading = false;
        amendmentOrderChanged = false;
        amendPanelPriceInput.text = (editingOrder.limitPrice ?? editingOrder.orderPrice).ToString();
        amendPanelQuantityInput.text = (editingOrder.quantity - editingOrder.filledQuantity).ToString();
        SetText(amendPanelStockText, editingOrder.name + " (" + editingOrder.symbol + ") / " + editingOrder.SideLabel);
        panelManager.OpenOrderActionConfirmation("주문 정정 확인", null, ConfirmAmendFromPanel,
            amendPanel, ResetAmendEditor);
        StartCoroutine(LoadAmendPortfolio());
        UpdateAmendEditor();
    }
    void ResetAmendEditor()
    {
        editorGeneration++;
        editingOrder = null;
        editingPortfolio = null;
        submittedAmendId = null;
        amendmentPortfolioLoading = false;
    }
    public void CancelAmendConfirmation()
    {
        if (!IsRequesting && panelManager != null) panelManager.CloseTradeConfirmation();
    }
    IEnumerator LoadAmendPortfolio()
    {
        if (editingOrder == null || amendmentPortfolioLoading) yield break;
        int generation = editorGeneration;
        amendmentPortfolioLoading = true;
        yield return Send("GET", "/portfolio", null, json =>
        {
            if (generation == editorGeneration) editingPortfolio = json.ToObject<PortfolioResponse>();
        });
        if (generation == editorGeneration) amendmentPortfolioLoading = false;
    }
    void AmendCapacity(out long cash, out long quantity)
    {
        cash = quantity = 0;
        if (editingOrder == null || editingPortfolio == null) return;
        // Add back ONLY this order's reservation, not other orders' reservations.
        cash = Math.Max(0, editingPortfolio.availableCash +
            (editingOrder.side == "BUY" ? editingOrder.reservedAmount : 0));
        if (editingOrder.side == "BUY")
        {
            if (Read(amendPanelPriceInput, out int price)) quantity = cash / price;
        }
        else
        {
            foreach (var holding in editingPortfolio.holdings ?? new List<Holding>())
                if (holding.symbol == editingOrder.symbol)
                    quantity = Math.Max(0L, (long)holding.availableQuantity + editingOrder.reservedQuantity);
        }
    }
    void UpdateAmendEditor()
    {
        if (editingOrder == null) return;
        bool valid = Read(amendPanelPriceInput, out int price) && Read(amendPanelQuantityInput, out _);
        Read(amendPanelQuantityInput, out int quantity);
        AmendCapacity(out long cash, out long possible);
        bool ready = editingPortfolio != null;
        SetText(amendPanelAvailableQuantityText, ready ? $"가능 {possible:N0}주" : "조회 중…");
        SetText(amendPanelAvailableCashText, editingOrder.side == "BUY" ?
            (ready ? $"{cash:N0}원" : "조회 중…") : "매도는 가능 수량 기준");
        SetText(amendPanelEstimatedAmountText, $"{(long)Math.Max(0, price) * Math.Max(0, quantity):N0}원");
        SetText(amendPanelNoticeText, amendmentOrderChanged ? "주문이 체결되거나 변경되었습니다. 창을 닫고 다시 선택하세요." :
            "정정 후 조건 충족 시 즉시 체결될 수 있습니다.\n확인 전 체결·변경된 주문은 정정할 수 없습니다.");
        amendPanelPriceInput.interactable = !IsRequesting;
        amendPanelQuantityInput.interactable = !IsRequesting;
        if (amendPanelConfirmButton != null) amendPanelConfirmButton.interactable =
            !IsRequesting && !amendmentOrderChanged && ready && valid && quantity <= possible;
        if (amendPanelCancelButton != null) amendPanelCancelButton.interactable = !IsRequesting;
    }
    public void ConfirmAmendFromPanel()
    {
        if (editingOrder == null || IsRequesting) return;
        if (amendmentOrderChanged) { Message("주문이 변경되었습니다. 창을 닫고 다시 선택하세요."); return; }
        if (!Read(amendPanelPriceInput, out int price) || !Read(amendPanelQuantityInput, out int quantity))
        { Message("정정할 가격과 수량은 1 이상으로 입력하세요."); return; }
        if (editingPortfolio == null) { Message("계좌 정보를 확인 중입니다. 잠시 후 다시 확인하세요."); return; }
        AmendCapacity(out _, out long possible);
        if (quantity > possible) { Message("정정 가능 금액 또는 수량이 부족합니다."); return; }
        if (submittedAmendId == null || submittedAmendPrice != price || submittedAmendQuantity != quantity)
        {
            submittedAmendId = Guid.NewGuid().ToString();
            submittedAmendPrice = price;
            submittedAmendQuantity = quantity;
        }
        // Read final dialog inputs here; retain original order version for the server's race check.
        StartCoroutine(Mutate(editingOrder._id + "/amend", new { quantity, limitPrice = price,
            expectedVersion = editingOrder.version, clientRequestId = submittedAmendId }));
    }
    void ChangeAmendInput(TMP_InputField input, int delta)
    {
        if (editingOrder == null || IsRequesting || input == null) return;
        int.TryParse(input.text.Replace(",", "").Trim(), out int old);
        input.text = Math.Max(1L, Math.Min(int.MaxValue, (long)old + delta)).ToString();
    }
    public void IncreaseAmendPrice() { ChangeAmendInput(amendPanelPriceInput, Math.Max(1, amendPriceStep)); }
    public void DecreaseAmendPrice() { ChangeAmendInput(amendPanelPriceInput, -Math.Max(1, amendPriceStep)); }
    public void IncreaseAmendQuantity() { ChangeAmendInput(amendPanelQuantityInput, 1); }
    public void DecreaseAmendQuantity() { ChangeAmendInput(amendPanelQuantityInput, -1); }
    public void SetAmendPriceToCurrent()
    {
        if (editingOrder != null && !IsRequesting) StartCoroutine(SetAmendPriceFromQuote());
    }
    IEnumerator SetAmendPriceFromQuote()
    {
        int generation = editorGeneration;
        string symbol = editingOrder.symbol;
        string originalInput = amendPanelPriceInput.text;
        yield return Send("GET", "/stock-info?symbol=" + Uri.EscapeDataString(symbol), null, json =>
        {
            if (generation != editorGeneration || editingOrder == null || IsRequesting || amendPanelPriceInput.text != originalInput) return;
            var quote = json.ToObject<StockInfoResponse>();
            if (quote != null && quote.symbol == symbol && quote.price > 0) amendPanelPriceInput.text = quote.price.ToString();
        });
    }
    public void OpenCancelConfirmation()
    {
        if (editingOrder != null) return;
        if (panelManager == null || panelManager.IsOrderBusy || SelectedOrder == null || !SelectedOrder.CanEdit) return;
        TradeOrderData frozen = SelectedOrder;
        long price = frozen.limitPrice ?? frozen.orderPrice;
        string details = $"{frozen.name} ({frozen.symbol})\n매매종류: {frozen.SideLabel}\n" +
            $"주문번호: {frozen._id}\n주문가격: {price:N0}원\n주문수량: {frozen.quantity:N0}주\n" +
            $"미체결수량: {Math.Max(0, frozen.quantity - frozen.filledQuantity):N0}주\n" +
            $"주문시간: {OrderTimeFormat.Korea(frozen.createdAt)}\n미체결 주문을 취소하시겠습니까?";
        panelManager.OpenOrderActionConfirmation("취소 확인", () => details, () =>
        {
            if (!IsRequesting) StartCoroutine(Mutate(frozen._id + "/cancel", new { expectedVersion = frozen.version }));
        }, display: () =>
        {
            if (stockLoader != null) stockLoader.RefreshCancelConfirmationUI(frozen, details, !IsRequesting);
        });
    }
    IEnumerator Mutate(string path, object body)
    {
        IsRequesting = true;
        bool success = false;
        yield return Send("POST", "/orders/" + path, body, json =>
        {
            var response = json.ToObject<TradeOrderResult>();
            if (response == null || !response.success || response.data == null) { Message("주문 응답이 올바르지 않습니다."); return; }
            success = true;
            Message($"{response.data.name} {response.data.SideLabel} 주문 {response.data.StatusLabel}");
        });
        IsRequesting = false;
        if (success && panelManager != null) panelManager.CloseTradeConfirmation();
        // On failure retain the frozen request for a deliberate retry; do not auto-submit.
        Refresh();
        if (stockLoader != null) stockLoader.RefreshPortfolio();
        if (myData != null) myData.ReloadPortfolio();
    }
    public void TopUpDemoCash()
    {
        if (!IsRequesting && (panelManager == null || !panelManager.IsOrderBusy)) StartCoroutine(TopUp());
    }
    IEnumerator TopUp()
    {
        IsRequesting = true;
        if (pendingTopUpRequestId == null) pendingTopUpRequestId = Guid.NewGuid().ToString();
        yield return Send("POST", "/top-up", new { clientRequestId = pendingTopUpRequestId },
            json => { pendingTopUpRequestId = null; Message("모의투자 자금 100만원 입금 완료"); });
        IsRequesting = false;
        if (stockLoader != null) stockLoader.RefreshPortfolio();
        if (myData != null) myData.ReloadPortfolio();
    }
    IEnumerator Send(string method, string path, object body, Action<JObject> callback)
    {
        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn())
        { Message("로그인이 필요합니다."); yield break; }
        using (var request = new UnityWebRequest(ServerConfig.HttpBaseUrl + path, method))
        {
            request.timeout = 30;
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            AuthManager.Instance.AddAuthorizationHeader(request);
            yield return request.SendWebRequest();
            string text = request.downloadHandler.text;
            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = request.error;
                try { var json = JObject.Parse(text); error = (json["detail"] ?? json["message"])?.ToString() ?? error; }
                catch { }
                if (request.responseCode == 401) { AuthManager.Instance.Logout(); error = "로그인이 만료되었습니다."; }
                if (request.responseCode == 0) error += "\n처리 결과가 불확실합니다. 주문내역/잔액을 확인하세요.";
                Message(error);
                yield break;
            }
            JObject result;
            try { result = JObject.Parse(text); }
            catch (Exception error) { Message("응답 파싱 실패: " + error.Message); yield break; }
            callback(result);
        }
    }
}


