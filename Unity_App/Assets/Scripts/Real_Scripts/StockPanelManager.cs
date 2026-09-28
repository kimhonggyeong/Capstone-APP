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


    public IEnumerator ShowListAfterLoading()
    {
        ShowLoading();

        if (stockLoader != null)
        {
            stockLoader.StopChartRequests();
        }

        StopOrderBook();
        StopExecutions();
        StopNews();

        HideAllRealChildPanels();

        SetActiveSafe(
            realSimulationPanel,
            false
        );

        SetActiveSafe(
            stockListPanel,
            false
        );

        ClearRealButtonVisuals();

        yield return new WaitForSeconds(
            minimumLoadingTime
        );

        SetActiveSafe(
            stockListPanel,
            true
        );

        HideLoading();
    }


    // =========================================================
    // 차트 탭
    // =========================================================

    public void OnClickBackToChart()
    {
        if (!HasCurrentSymbol())
        {
            return;
        }

        ShowLoading();

        StopOrderBook();
        StopExecutions();
        StopNews();

        HideAllRealChildPanels();
        ClearRealButtonVisuals();

        StartCoroutine(
            ShowChartTabAfterLoading()
        );
    }


    private IEnumerator ShowChartTabAfterLoading()
    {
        if (stockLoader != null)
        {
            stockLoader.ReloadOnlyCandles();
        }

        yield return new WaitForSeconds(
            minimumLoadingTime
        );

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
        ShowLoading();

        StopOrderBook();
        StopExecutions();
        StopNews();

        SetActiveSafe(
            stockListPanel,
            false
        );

        SetActiveSafe(
            realSimulationPanel,
            true
        );


        SetActiveSafe(
            orderBookPanel,
            true
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

        ClearRealButtonVisuals();

        yield return new WaitForSeconds(
            minimumLoadingTime
        );

        SetActiveSafe(
            realSimulationPanel,
            true
        );

        if (orderBookLoader != null)
        {
            orderBookLoader.ShowOrderBook(currentSymbol);
        }

        SetRealButtonVisuals(
            showChart: false,
            showOrderBook: true,
            showExecution: false,
            showInfo: false,
            showNews: false
        );

        HideLoading();
    }
    // =========================================================
    // 호가 탭
    // =========================================================

    public void OnClickOrderBook()
    {
        if (!HasCurrentSymbol())
        {
            return;
        }

        ShowLoading();

        /*
         * 차트 캔들 요청만 중지한다.
         * 현재가 WebSocket은 유지한다.
         */
        if (stockLoader != null)
        {
            stockLoader.StopOnlyCandleRequests();
        }

        StopExecutions();
        StopNews();

        HideAllRealChildPanels();
        ClearRealButtonVisuals();

        StartCoroutine(
            ShowOrderBookAfterLoading()
        );
    }


    private IEnumerator ShowOrderBookAfterLoading()
    {
        StopExecutions();
        StopNews();

        if (orderBookLoader != null)
        {
            orderBookLoader.ShowOrderBook(
                currentSymbol
            );
        }
        else
        {
            Debug.LogError(
                "orderBookLoader가 연결되지 않았습니다."
            );
        }

        yield return new WaitForSeconds(
            minimumLoadingTime
        );

        SetActiveSafe(
            realSimulationPanel,
            true
        );

        //SetActiveSafe(
        //    chartPanel,
        //    false
        //);

        SetActiveSafe(
            orderBookPanel,
            true
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
            showChart: false,
            showOrderBook: true,
            showExecution: false,
            showInfo: false,
            showNews: false
        );

        HideLoading();
    }


    // =========================================================
    // 체결 탭
    // =========================================================

    public void OnClickExecution()
    {
        if (!HasCurrentSymbol())
        {
            return;
        }

        ShowLoading();

        /*
         * 차트 캔들 요청만 중지한다.
         * 현재가 WebSocket은 유지한다.
         */
        if (stockLoader != null)
        {
            stockLoader.StopOnlyCandleRequests();
        }

        StopOrderBook();
        StopExecutions();
        StopNews();

        HideAllRealChildPanels();
        ClearRealButtonVisuals();

        StartCoroutine(
            ShowExecutionAfterLoading()
        );
    }


    private IEnumerator ShowExecutionAfterLoading()
    {
        if (executionListLoader == null)
        {
            Debug.LogError(
                "executionListLoader가 연결되지 않았습니다."
            );

            HideLoading();
            yield break;
        }

        /*
         * ExecutionListLoader가 executionPanel에 붙어 있다면
         * 먼저 패널을 활성화한 후 ShowExecutions를 호출해야
         * OnDisable과 충돌하지 않는다.
         */
        SetActiveSafe(
            realSimulationPanel,
            true
        );

        //SetActiveSafe(
        //    chartPanel,
        //    false
        //);

        SetActiveSafe(
            orderBookPanel,
            false
        );

        SetActiveSafe(
            executionPanel,
            true
        );

        SetActiveSafe(
            infoPanel,
            false
        );

        SetActiveSafe(
            newsPanel,
            false
        );

        executionListLoader.ShowExecutions(
            currentSymbol
        );

        yield return new WaitForSeconds(
            minimumLoadingTime
        );

        SetRealButtonVisuals(
            showChart: false,
            showOrderBook: false,
            showExecution: true,
            showInfo: false,
            showNews: false
        );

        HideLoading();
    }


    // =========================================================
    // 종목 정보 탭
    // =========================================================

    public void OnClickInfo()
    {
        if (!HasCurrentSymbol())
        {
            return;
        }

        ShowLoading();

        if (stockLoader != null)
        {
            stockLoader.StopOnlyCandleRequests();
        }

        StopOrderBook();
        StopExecutions();
        StopNews();

        HideAllRealChildPanels();
        ClearRealButtonVisuals();

        if (detailLoader != null)
        {
            detailLoader.LoadDetail(
                currentSymbol
            );
        }
        else
        {
            Debug.LogError(
                "detailLoader가 연결되지 않았습니다."
            );
        }

        StartCoroutine(
            ShowInfoAfterLoading()
        );
    }


    private IEnumerator ShowInfoAfterLoading()
    {
        yield return new WaitForSeconds(
            minimumLoadingTime
        );

        SetActiveSafe(
            realSimulationPanel,
            true
        );

        //SetActiveSafe(
        //    chartPanel,
        //    false
        //);

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
            true
        );

        SetActiveSafe(
            newsPanel,
            false
        );

        SetRealButtonVisuals(
            showChart: false,
            showOrderBook: false,
            showExecution: false,
            showInfo: true,
            showNews: false
        );

        HideLoading();
    }


    // =========================================================
    // 뉴스 탭
    // =========================================================

    public void OnClickNews()
    {
        if (!HasCurrentSymbol())
        {
            return;
        }

        ShowLoading();

        /*
         * 뉴스 탭에서는 차트 캔들 요청만 중지한다.
         * 상단 현재가를 계속 표시해야 한다면
         * 가격 WebSocket은 유지한다.
         */
        if (stockLoader != null)
        {
            stockLoader.StopOnlyCandleRequests();
        }

        StopOrderBook();
        StopExecutions();
        StopNews();

        HideAllRealChildPanels();
        ClearRealButtonVisuals();

        StartCoroutine(
            ShowNewsAfterLoading()
        );
    }


    private IEnumerator ShowNewsAfterLoading()
    {
        if (newsListLoader == null)
        {
            Debug.LogError(
                "newsListLoader가 연결되지 않았습니다."
            );

            HideLoading();
            yield break;
        }

        /*
         * NewsListLoader가 NewsPanel에 붙어 있을 수 있으므로
         * 패널을 먼저 활성화한다.
         */
        SetActiveSafe(
            realSimulationPanel,
            true
        );

        //SetActiveSafe(
        //    chartPanel,
        //    false
        //);

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
            true
        );

        /*
         * 현재 선택한 종목 코드를 전달한다.
         *
         * 005930 → 삼성전자 뉴스
         * 000660 → SK하이닉스 뉴스
         * 035420 → NAVER 뉴스
         */
        newsListLoader.ShowNews(
            currentSymbol
        );

        yield return new WaitForSeconds(
            minimumLoadingTime
        );

        SetRealButtonVisuals(
            showChart: false,
            showOrderBook: false,
            showExecution: false,
            showInfo: false,
            showNews: true
        );

        HideLoading();
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
    }


    // =========================================================
    // 오브젝트 종료
    // =========================================================

    private void OnDestroy()
    {
        /*
         * 씬 전환 또는 오브젝트 삭제 시
         * 열려 있던 통신과 목록을 안전하게 정리한다.
         */
        StopOrderBook();
        StopExecutions();
        StopNews();
    }
}
