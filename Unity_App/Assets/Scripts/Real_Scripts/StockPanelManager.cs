using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StockPanelManager : MonoBehaviour
{
    [Header("Main Panels")]
    public GameObject stockListPanel;
    public GameObject realSimulationPanel;
    public GameObject loadingPanel;

    [Header("Real Simulation Child Panels")]
    public GameObject orderBookPanel;
    public GameObject executionPanel;
    public GameObject infoPanel;
    public GameObject newsPanel;

    [Header("Buy / Sell Panels")]
    public TMP_Text Buy_Sell_Text1;
    public TMP_Text Buy_Sell_Text2;

    [Header("매수 / 매도 총 잔량 값")]
    public TMP_Text totalQuantityValueText;

    public GameObject Buy_Sell_Check_Panel;

    // 추가
    public Image orderButtonImage;       // "매수 주문하기 / 매도 주문하기" 버튼 배경
    public TMP_Text orderButtonText;     // 위 버튼 텍스트

    public OrderListLoader orderListLoader;
    private System.Action orderActionConfirm;
    private System.Func<string> orderActionDetails;
    private string orderActionTitle;
    private GameObject orderActionPanel;
    private System.Action orderActionClosed;
    private System.Action orderActionDisplay;

    [Header("AI_Ramen")]
    public GameObject Real_AI_Panel;
    public GameObject Reasoning;
    public GameObject History;
    public GameObject Compare;
    public GameObject Compare_Buy;
    public GameObject Compare_Hold;
    public GameObject Compare_Sell;

    public TMP_Text ReasoningButtonText;
    public TMP_Text HistoryButtonText;
    public TMP_Text CompareButtonText;

    public GameObject ReasoningButtonImage;
    public GameObject HistoryButtonImage;
    public GameObject CompareButtonImage;

    private AiComparePanelController aiComparePanelController;

    [Header("Loaders")]
    public StockCandlestickLoader stockLoader;
    public OrderBookLoader orderBookLoader;
    public ExecutionListLoader executionListLoader;
    public NewsListLoader newsListLoader;

    [Header("Detail Loader")]
    public StockDetailLoader detailLoader;

    [Header("Real Buttons Visuals")]
    public TMP_Text chartButtonText;
    public GameObject chartButtonImage;

    public TMP_Text bidButtonText;
    public GameObject bidButtonImage;

    public TMP_Text executionButtonText;
    public GameObject executionButtonImage;

    public TMP_Text infoButtonText;
    public GameObject infoButtonImage;

    public TMP_Text newsButtonText;
    public GameObject newsButtonImage;

    [Header("OrderPanel")]
    public GameObject left1;
    public GameObject left2;
    public GameObject right1;
    public GameObject right2;

    [Header("Loading")]
    public float minimumLoadingTime = 0.3f;

    [Min(1f)]
    [Tooltip("최초 데이터 수신을 기다리는 최대 시간(초)")]
    public float dataLoadingTimeout = 30f;

    [Header("메인 호가 스크롤")]
    [Tooltip("OrderBook 안에서 사용하는 Bid_Ask_List의 Scroll Rect를 연결하세요.")]
    public ScrollRect orderBookScrollRect;

    private enum RealDataTab
    {
        OrderBook,
        Execution,
        Info,
        News
    }

    private int tabLoadGeneration;

    private string currentSymbol = "";
    private bool buySellCheckPanelOpened = false;
    private bool isBuyMode = true;
    public bool IsBuyMode => isBuyMode;
    public bool IsOrderBusy => (stockLoader != null && stockLoader.IsTrading) ||
                              (orderListLoader != null && orderListLoader.IsRequesting);

    public Color buyColor =
    new Color32(0xF6, 0x6B, 0x24, 0xFF);

    public Color sellColor =
        new Color32(0x4A, 0x90, 0xE2, 0xFF);

    private void Start()
    {
        SetActiveSafe(
            stockListPanel,
            false
        );

        SetActiveSafe(
            realSimulationPanel,
            false
        );

        SetActiveSafe(
            orderBookPanel,
            false
        );

        SetActiveSafe(
            executionPanel,
            false
        );

        SetActiveSafe(
            infoPanel,
            false
        );

        SetActiveSafe(
            newsPanel,
            false
        );

        SetActiveSafe(
            loadingPanel,
            true
        );

        SetRealButtonVisuals(
            showChart: false,
            showOrderBook: false,
            showExecution: false,
            showInfo: false,
            showNews: false
        );
    }


    // =========================================================
    // 공통
    // =========================================================

    private void SetActiveSafe(
        GameObject target,
        bool active
    )
    {
        if (target != null)
        {
            target.SetActive(
                active
            );
        }
    }


    public void ShowLoading()
    {
        SetActiveSafe(
            loadingPanel,
            true
        );
    }


    public void HideLoading()
    {
        SetActiveSafe(
            loadingPanel,
            false
        );
    }


    public void SetCurrentSymbol(
        string symbol
    )
    {
        CancelPendingTabLoad();

        currentSymbol =
            symbol != null
                ? symbol.Trim()
                : "";

        Debug.Log(
            "현재 선택 종목 저장: " +
            currentSymbol
        );
    }


    private bool HasCurrentSymbol()
    {
        if (
            !string.IsNullOrWhiteSpace(
                currentSymbol
            )
        )
        {
            return true;
        }

        Debug.LogWarning(
            "현재 선택된 종목 코드가 없습니다."
        );

        return false;
    }


    private void StopOrderBook()
    {
        if (orderBookLoader != null)
        {
            orderBookLoader.StopOrderBook();
        }
    }


    private void StopExecutions()
    {
        if (executionListLoader != null)
        {
            executionListLoader.StopExecutions();
        }
    }


    private void StopNews()
    {
        if (newsListLoader != null)
        {
            newsListLoader.StopNews();
        }
    }


    private void HideAllRealChildPanels()
    {

        SetActiveSafe(
            orderBookPanel,
            false
        );

        SetActiveSafe(
            executionPanel,
            false
        );

        SetActiveSafe(
            infoPanel,
            false
        );

        SetActiveSafe(
            newsPanel,
            false
        );
    }


    private void ClearRealButtonVisuals()
    {
        SetRealButtonVisuals(
            showChart: false,
            showOrderBook: false,
            showExecution: false,
            showInfo: false,
            showNews: false
        );
    }


    // =========================================================
    // 매수 / 매도 패널
    // =========================================================


    private void Update()
    {
        if (!buySellCheckPanelOpened || stockLoader == null) return;
        // The separate editable amendment panel manages its own UI.
        if (orderActionPanel != null && orderActionPanel != Buy_Sell_Check_Panel) return;
        if (orderActionDisplay != null) { orderActionDisplay(); return; }
        stockLoader.RefreshTradeConfirmationUI(
            orderActionConfirm != null ? orderActionTitle : null,
            orderActionDetails != null ? orderActionDetails() : null,
            !IsOrderBusy);
    }

    public void CloseTradeConfirmation()
    {
        if (IsOrderBusy) return;
        buySellCheckPanelOpened = false;
        SetActiveSafe(Buy_Sell_Check_Panel, false);
        SetActiveSafe(orderActionPanel, false);
        var closed = orderActionClosed;
        orderActionClosed = null;
        orderActionPanel = null;
        orderActionConfirm = null;
        orderActionDetails = null;
        closed?.Invoke();
        orderActionDisplay = null;
        if (stockLoader != null) stockLoader.CancelPreparedTrade();
    }


    public void OpenOrderActionConfirmation(string title, System.Func<string> details, System.Action confirm,
        GameObject panelOverride = null, System.Action closed = null, System.Action display = null)
    {
        if (IsOrderBusy) return;
        orderActionTitle = title;
        orderActionDetails = details;
        orderActionConfirm = confirm;
        orderActionPanel = panelOverride != null ? panelOverride : Buy_Sell_Check_Panel;
        orderActionClosed = closed;
        orderActionDisplay = display;
        buySellCheckPanelOpened = true;
        SetActiveSafe(Buy_Sell_Check_Panel, false);
        SetActiveSafe(orderActionPanel, true);
        Update();
    }

    public void Show_Buy_Sell_Check_panel()
    {
        if (IsOrderBusy) return;
        if (buySellCheckPanelOpened) { CloseTradeConfirmation(); return; }
        if (stockLoader == null || !stockLoader.TryPrepareTrade(isBuyMode)) return;
        orderActionConfirm = null;
        orderActionDetails = null;
        buySellCheckPanelOpened = !buySellCheckPanelOpened;
        orderActionDisplay = null;

        SetActiveSafe(
            Buy_Sell_Check_Panel,
            buySellCheckPanelOpened
        );

        Update(); // Populate labels immediately when opening, before the next frame.
    }

    public void ConfirmTrade()
    {
        if (IsOrderBusy) return;
        if (orderActionConfirm != null) orderActionConfirm();
        else if (stockLoader != null)
        {
            stockLoader.ConfirmPreparedTrade();
            if (stockLoader.IsTrading)
            {
                // Hide only: do not cancel the prepared request being sent.
                buySellCheckPanelOpened = false;
                SetActiveSafe(Buy_Sell_Check_Panel, false);
            }
        }
    }

    public void Show_Buy_panel()
    {
        if (IsOrderBusy) return;
        CloseTradeConfirmation();
        isBuyMode = true;

        Buy_Sell_Text1.text = "매수 호가 현황";
        Buy_Sell_Text2.text = "총 매수 잔량";

        if (orderButtonText != null)
            orderButtonText.text = "매수 주문하기";

        if (orderButtonImage != null)
            orderButtonImage.color = buyColor;


        // 추가
        if (orderBookLoader != null)
        {
            orderBookLoader.ShowBuyOrderBook(
                currentSymbol
            );
        }

        if (totalQuantityValueText != null)
        {
            totalQuantityValueText.color =
                buyColor;
        }

        left1.SetActive(true);
        left2.SetActive(false);
        right1.SetActive(true);
        right2.SetActive(false);
    }

    public void Show_Sell_panel()
    {
        if (IsOrderBusy) return;
        CloseTradeConfirmation();
        isBuyMode = false;

        Buy_Sell_Text1.text = "매도 호가 현황";
        Buy_Sell_Text2.text = "총 매도 잔량";

        if (orderButtonText != null)
            orderButtonText.text = "매도 주문하기";

        if (orderButtonImage != null)
            orderButtonImage.color = sellColor;


        // 추가
        if (orderBookLoader != null)
        {
            orderBookLoader.ShowSellOrderBook(
                currentSymbol
            );
        }

        if (totalQuantityValueText != null)
        {
            totalQuantityValueText.color =
                sellColor;
        }

        left1.SetActive(true);
        left2.SetActive(false);
        right1.SetActive(true);
        right2.SetActive(false);
    }

    public void Show_Correct_panel()
    {
        if (IsOrderBusy) return;
        CloseTradeConfirmation();
        if (orderBookLoader != null)
            orderBookLoader.StopTradeOrderBook();
        left1.SetActive(false);
        left2.SetActive(true);
        right1.SetActive(false);
        right2.SetActive(true);
        if (orderListLoader != null) orderListLoader.Refresh();
    }
    // =========================================================
    // 목록으로 이동
    // =========================================================

    public void OnClickBackToList()
    {
        StartCoroutine(
            ShowListAfterLoading()
        );
    }


    // 검색에서는 공용 로딩창 없이 결과 목록을 즉시 연다.
    public void ShowListWithoutLoading()
    {
        PrepareStockList();
        SetActiveSafe(stockListPanel, true);
        HideLoading();
    }


    private int PrepareStockList()
    {
        int generation = CancelPendingTabLoad();

        if (stockLoader != null)
            stockLoader.StopChartRequests();

        StopOrderBook();
        StopExecutions();
        StopNews();
        HideAllRealChildPanels();
        SetActiveSafe(realSimulationPanel, false);
        ClearRealButtonVisuals();
        return generation;
    }


    public IEnumerator ShowListAfterLoading()
    {
        int generation = PrepareStockList();
        ShowLoading();
        SetActiveSafe(stockListPanel, false);

        yield return new WaitForSeconds(minimumLoadingTime);

        if (generation != tabLoadGeneration)
            yield break;

        SetActiveSafe(stockListPanel, true);
        HideLoading();
    }


    // =========================================================
    // 차트 탭
    // =========================================================

    public void OnClickBackToChart()
    {
        if (!HasCurrentSymbol())
            return;

        int generation = CancelPendingTabLoad();
        ShowLoading();
        StopOrderBook();
        StopExecutions();
        StopNews();
        HideAllRealChildPanels();
        ClearRealButtonVisuals();

        StartCoroutine(ShowChartTabAfterLoading(generation));
    }


    private IEnumerator ShowChartTabAfterLoading(int generation)
    {
        if (stockLoader != null)
        {
            stockLoader.ReloadOnlyCandles();
        }

        yield return new WaitForSeconds(
            minimumLoadingTime
        );

        if (generation != tabLoadGeneration)
            yield break;

        SetActiveSafe(
            realSimulationPanel,
            true
        );

        SetActiveSafe(
            orderBookPanel,
            false
        );

        SetActiveSafe(
            executionPanel,
            false
        );

        SetActiveSafe(
            infoPanel,
            false
        );

        SetActiveSafe(
            newsPanel,
            false
        );

        SetRealButtonVisuals(
            showChart: true,
            showOrderBook: false,
            showExecution: false,
            showInfo: false,
            showNews: false
        );

        HideLoading();
    }


    /*
     * StockCandlestickLoader의 SearchFlow에서 호출한다.
     * 종목을 처음 선택한 뒤 차트 화면을 표시한다.
     */
    public IEnumerator ShowChartAfterLoading()
    {
        int generation = BeginDataTabLoad();

        yield return ShowDataTabAfterLoading(
            RealDataTab.OrderBook,
            generation,
            resetSimulationScroll: true
        );
    }

    private void ResetSimulationScrollPosition()
    {
        if (realSimulationPanel == null)
            return;

        // The scene also has a list with this name in another panel.
        Transform list = realSimulationPanel.transform.Find("Real_simulation_List");
        ScrollRect scrollRect = list != null ? list.GetComponent<ScrollRect>() : null;
        if (scrollRect == null || scrollRect.content == null)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();

        Vector2 position = scrollRect.content.anchoredPosition;
        position.y = 0f;
        scrollRect.content.anchoredPosition = position;
    }

    // =========================================================
    // 호가 탭
    // =========================================================

    public void OnClickOrderBook()
    {
        OpenDataTab(RealDataTab.OrderBook);
    }

    public void OnClickExecution()
    {
        OpenDataTab(RealDataTab.Execution);
    }

    public void OnClickInfo()
    {
        OpenDataTab(RealDataTab.Info);
    }

    public void OnClickNews()
    {
        OpenDataTab(RealDataTab.News);
    }

    private int CancelPendingTabLoad()
    {
        tabLoadGeneration++;

        if (detailLoader != null)
            detailLoader.StopDetail();

        return tabLoadGeneration;
    }

    private int BeginDataTabLoad()
    {
        int generation = CancelPendingTabLoad();

        ShowLoading();
        StopOrderBook();
        StopExecutions();
        StopNews();
        HideAllRealChildPanels();
        ClearRealButtonVisuals();

        SetActiveSafe(stockListPanel, false);
        SetActiveSafe(realSimulationPanel, true);

        return generation;
    }

    private void OpenDataTab(RealDataTab tab)
    {
        if (!HasCurrentSymbol())
            return;

        // 현재가 WebSocket을 유지하면서 캔들 요청만 중지합니다.
        if (stockLoader != null)
            stockLoader.StopOnlyCandleRequests();

        int generation = BeginDataTabLoad();
        StartCoroutine(ShowDataTabAfterLoading(tab, generation));
    }

    private IEnumerator ShowDataTabAfterLoading(
        RealDataTab tab,
        int generation,
        bool resetSimulationScroll = false)
    {
        if (generation != tabLoadGeneration)
            yield break;

        SetActiveSafe(orderBookPanel, tab == RealDataTab.OrderBook);
        SetActiveSafe(executionPanel, tab == RealDataTab.Execution);
        SetActiveSafe(infoPanel, tab == RealDataTab.Info);
        SetActiveSafe(newsPanel, tab == RealDataTab.News);

        float startedAt = Time.realtimeSinceStartup;
        System.Func<bool> isLoading = null;
        System.Action cancelLoad = null;

        // 비활성 패널에 붙은 Loader도 요청을 시작할 수 있도록 먼저 표시합니다.
        switch (tab)
        {
            case RealDataTab.OrderBook:
                if (orderBookLoader != null)
                {
                    orderBookLoader.ShowOrderBook(currentSymbol);
                    isLoading = () => orderBookLoader != null && orderBookLoader.IsLoading;
                    cancelLoad = StopOrderBook;
                }
                break;

            case RealDataTab.Execution:
                if (executionListLoader != null)
                {
                    executionListLoader.ShowExecutions(currentSymbol);
                    isLoading = () => executionListLoader != null && executionListLoader.IsLoading;
                    cancelLoad = StopExecutions;
                }
                break;

            case RealDataTab.Info:
                if (detailLoader != null)
                {
                    detailLoader.LoadDetail(currentSymbol);
                    isLoading = () => detailLoader != null && detailLoader.IsLoading;
                    cancelLoad = detailLoader.StopDetail;
                }
                break;

            case RealDataTab.News:
                if (newsListLoader != null)
                {
                    newsListLoader.ShowNews(currentSymbol);
                    isLoading = () => newsListLoader != null && newsListLoader.IsLoading;
                    cancelLoad = StopNews;
                }
                break;
        }

        if (isLoading == null)
            Debug.LogError($"[탭 로딩] {tab} Loader가 연결되지 않았습니다.", this);

        float minimumSeconds = Mathf.Max(0f, minimumLoadingTime);
        float timeoutSeconds = Mathf.Max(minimumSeconds, Mathf.Max(1f, dataLoadingTimeout));

        while (generation == tabLoadGeneration)
        {
            float elapsed = Time.realtimeSinceStartup - startedAt;
            bool waitingForData = isLoading != null && isLoading();

            if (!waitingForData && elapsed >= minimumSeconds)
                break;

            if (waitingForData && elapsed >= timeoutSeconds)
            {
                cancelLoad?.Invoke();
                Debug.LogError($"[탭 로딩] {tab} 데이터 수신 시간이 초과되었습니다.", this);
                break;
            }

            yield return null;
        }

        if (generation != tabLoadGeneration)
            yield break;

        if (tab == RealDataTab.OrderBook && orderBookLoader != null &&
            !string.IsNullOrEmpty(orderBookLoader.LoadError))
        {
            Debug.LogWarning(orderBookLoader.LoadError, this);
        }

        if (tab == RealDataTab.Info && detailLoader != null &&
            !string.IsNullOrEmpty(detailLoader.LoadError))
        {
            Debug.LogWarning(detailLoader.LoadError, this);
        }

        // 생성/삭제된 Row와 텍스트의 크기가 반영된 뒤 로딩을 닫습니다.
        yield return null;

        if (generation != tabLoadGeneration)
            yield break;

        Canvas.ForceUpdateCanvases();

        if (resetSimulationScroll)
            ResetSimulationScrollPosition();

        CenterOrderBookScroll();

        SetRealButtonVisuals(
            showChart: false,
            showOrderBook: tab == RealDataTab.OrderBook,
            showExecution: tab == RealDataTab.Execution,
            showInfo: tab == RealDataTab.Info,
            showNews: tab == RealDataTab.News
        );

        HideLoading();
    }

    private void CenterOrderBookScroll()
    {
        if (orderBookScrollRect == null || orderBookScrollRect.content == null)
            return;

        orderBookScrollRect.StopMovement();
        orderBookScrollRect.verticalNormalizedPosition = 0.5f;
    }

    // =========================================================
    // 실시간 탭 버튼 색상
    // =========================================================

    private void SetRealButtonVisuals(
        bool showChart,
        bool showOrderBook,
        bool showExecution,
        bool showInfo,
        bool showNews
    )
    {
        SetButtonVisual(
            chartButtonText,
            chartButtonImage,
            showChart
        );

        SetButtonVisual(
            bidButtonText,
            bidButtonImage,
            showOrderBook
        );

        SetButtonVisual(
            executionButtonText,
            executionButtonImage,
            showExecution
        );

        SetButtonVisual(
            infoButtonText,
            infoButtonImage,
            showInfo
        );

        SetButtonVisual(
            newsButtonText,
            newsButtonImage,
            showNews
        );
    }


    private void SetButtonVisual(
        TMP_Text targetText,
        GameObject targetImage,
        bool selected
    )
    {
        SetTextColor(
            targetText,
            selected
                ? "#F66B24"
                : "#000000"
        );

        SetActiveSafe(
            targetImage,
            selected
        );
    }


    public void SetTextColor(
        TMP_Text text,
        string hexColor
    )
    {
        if (text == null)
        {
            return;
        }

        if (
            ColorUtility.TryParseHtmlString(
                hexColor,
                out Color color
            )
        )
        {
            text.color = color;
        }
    }
    // =========================================================
    // AI 패널
    // =========================================================

    public void Show_Real_AI_Panel()
    {
        SetActiveSafe(
            Real_AI_Panel,
            true
        );

        SetActiveSafe(
            Reasoning,
            true
        );

        SetActiveSafe(
            History,
            false
        );

        SetActiveSafe(
            Compare,
            false
        );

        SetActiveSafe(
            Compare_Buy,
            false
        );

        SetActiveSafe(
            Compare_Hold,
            false
        );

        SetActiveSafe(
            Compare_Sell,
            false
        );

        Real_AI_SetRealButtonVisuals(
            showReasoning: true,
            showHistory: false,
            showCompare: false
        );
    }


    public void Show_Real_AI_Reasoning_Panel()
    {
        SetActiveSafe(
            Reasoning,
            true
        );

        SetActiveSafe(
            History,
            false
        );

        SetActiveSafe(
            Compare,
            false
        );

        SetActiveSafe(
            Compare_Buy,
            false
        );

        SetActiveSafe(
            Compare_Hold,
            false
        );

        SetActiveSafe(
            Compare_Sell,
            false
        );

        Real_AI_SetRealButtonVisuals(
            showReasoning: true,
            showHistory: false,
            showCompare: false
        );
    }


    public void Show_Real_AI_History_Panel()
    {
        SetActiveSafe(
            Reasoning,
            false
        );

        SetActiveSafe(
            History,
            true
        );

        SetActiveSafe(
            Compare,
            false
        );

        SetActiveSafe(
            Compare_Buy,
            false
        );

        SetActiveSafe(
            Compare_Hold,
            false
        );

        SetActiveSafe(
            Compare_Sell,
            false
        );

        Real_AI_SetRealButtonVisuals(
            showReasoning: false,
            showHistory: true,
            showCompare: false
        );
    }


    public void Show_Real_AI_Compare_Panel()
    {
        SetActiveSafe(
            Reasoning,
            false
        );

        SetActiveSafe(
            History,
            false
        );

        SetActiveSafe(
            Compare,
            true
        );

        SetActiveSafe(
            Compare_Buy,
            false
        );

        SetActiveSafe(
            Compare_Hold,
            false
        );

        SetActiveSafe(
            Compare_Sell,
            false
        );

        Real_AI_SetRealButtonVisuals(
            showReasoning: false,
            showHistory: false,
            showCompare: true
        );
    }


    public void Show_Real_AI_Compare_Buy_Panel()
    {
        SetActiveSafe(
            Reasoning,
            false
        );

        SetActiveSafe(
            History,
            false
        );

        SetActiveSafe(
            Compare,
            true
        );

        SetActiveSafe(
            Compare_Buy,
            true
        );

        SetActiveSafe(
            Compare_Hold,
            false
        );

        SetActiveSafe(
            Compare_Sell,
            false
        );
    }


    public void Show_Real_AI_Compare_Hold_Panel()
    {
        SetActiveSafe(
            Reasoning,
            false
        );

        SetActiveSafe(
            History,
            false
        );

        SetActiveSafe(
            Compare,
            true
        );

        SetActiveSafe(
            Compare_Buy,
            false
        );

        SetActiveSafe(
            Compare_Hold,
            true
        );

        SetActiveSafe(
            Compare_Sell,
            false
        );
    }


    public void Show_Real_AI_Compare_Sell_Panel()
    {
        SetActiveSafe(
            Reasoning,
            false
        );

        SetActiveSafe(
            History,
            false
        );

        SetActiveSafe(
            Compare,
            true
        );

        SetActiveSafe(
            Compare_Buy,
            false
        );

        SetActiveSafe(
            Compare_Hold,
            false
        );

        SetActiveSafe(
            Compare_Sell,
            true
        );
    }


    public void Show_Real_AI_Compare_Back()
    {
        SetActiveSafe(
            Real_AI_Panel,
            false
        );

        SetActiveSafe(
            Reasoning,
            false
        );

        SetActiveSafe(
            History,
            false
        );

        SetActiveSafe(
            Compare,
            false
        );

        SetActiveSafe(
            Compare_Buy,
            false
        );

        SetActiveSafe(
            Compare_Hold,
            false
        );

        SetActiveSafe(
            Compare_Sell,
            false
        );
    }


    private void Real_AI_SetRealButtonVisuals(
        bool showReasoning,
        bool showHistory,
        bool showCompare
    )
    {
        SetButtonVisual(
            ReasoningButtonText,
            ReasoningButtonImage,
            showReasoning
        );

        SetButtonVisual(
            HistoryButtonText,
            HistoryButtonImage,
            showHistory
        );

        SetButtonVisual(
            CompareButtonText,
            CompareButtonImage,
            showCompare
        );

        ResetAiCompareSelectionColors();
    }

    private void ResetAiCompareSelectionColors()
    {
        if (Compare == null)
            return;

        if (aiComparePanelController == null || aiComparePanelController.selectorPanel != Compare)
        {
            aiComparePanelController = System.Array.Find(
                FindObjectsByType<AiComparePanelController>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                controller => controller.selectorPanel == Compare
            );
        }

        if (aiComparePanelController != null)
            aiComparePanelController.ResetSelectionColors();
    }


    // =========================================================
    // 오브젝트 종료
    // =========================================================

    private void OnDestroy()
    {
        CancelPendingTabLoad();

        /*
         * 씬 전환 또는 오브젝트 삭제 시
         * 열려 있던 통신과 목록을 안전하게 정리한다.
         */
        StopOrderBook();
        StopExecutions();
        StopNews();
    }
}
