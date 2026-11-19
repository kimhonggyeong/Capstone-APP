using NativeWebSocket;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XCharts.Runtime;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class StockCandlestickLoader : MonoBehaviour
{


    public string symbol = "005930"; // 삼성전자
    public string period = "1mo";
    public string interval = "1d";

    private int currentPrice = 0;
    public int CurrentPrice => currentPrice;
    public bool IsTrading => isTrading;
    public PortfolioResponse LatestPortfolio { get; private set; }
    public event Action TradeCompleted;
    public event Action<PortfolioResponse> PortfolioUpdated;
    private string preparedSymbol, preparedName, preparedSide, preparedOrderType, preparedRequestId;
    private int preparedQuantity, preparedPrice;
    private int previousClosePrice = 0;

    public StockPanelManager panelManager;
    public Image checkButtonImage;       // 체크 패널 안 확인 버튼 배경
    public TMP_Text checkButtonText;     // 필요하면 확인 버튼 텍스트
    [Header("주문 확인 내용 / 주문목록")]
    public TMP_Text tradeConfirmationTitleText;
    public TMP_Text tradeConfirmationDetailsText;
    [Header("매수/매도 확인창 개별 텍스트")]
    public TMP_Text tradeConfirmationGuideText;
    public TMP_Text tradeConfirmationStockText;
    public TMP_Text tradeConfirmationSideText;
    public TMP_Text tradeConfirmationQuantityText;
    public TMP_Text tradeConfirmationPriceText;
    public TMP_Text tradeConfirmationFeeText;
    public TMP_Text tradeConfirmationTaxText;
    public TMP_Text tradeConfirmationAmountText;
    public TMP_Text tradeConfirmationCurrentPriceText;

    public CandlestickChart chart;

    [Header("차트 로딩")]
    [Tooltip("1분/일/주/월/년 차트를 불러오는 동안 표시할 로딩 패널")]
    public GameObject chartLoadingPanel;

    [Header("차트 기간 버튼 텍스트")]
    public TMP_Text minuteChartButtonText;
    public TMP_Text dailyChartButtonText;
    public TMP_Text weeklyChartButtonText;
    public TMP_Text monthlyChartButtonText;
    public TMP_Text yearlyChartButtonText;

    [Header("View")]
    public int visibleCount = 120;
    public int minVisibleCount = 10;
    public int maxVisibleCount = 1000;

    [Header("Drag / Zoom")]
    public float dragSensitivity = 0.05f;
    public int wheelZoomStep = 5;

    [Header("Mobile Touch")]
    public RectTransform chartTouchArea;

    public float touchDragSensitivity = 0.05f;
    public float pinchThreshold = 12f;
    public int pinchZoomStep = 5;

    private bool isTouchingChart = false;

    private Vector2 lastTouchPos;
    private float touchDragAccum = 0f;

    private float lastPinchDistance = -1f;
    private float pinchAccum = 0f;
    private bool isPinching = false;

    [Header("Moving Average")]
    public bool showMA5 = true;
    public bool showMA20 = true;
    public bool showMA60 = true;
    public bool showMA120 = true;

    [Header("Lazy Candle Loading")]
    [Tooltip("최초 진입 시 요청할 최소 캔들 수. 이동평균 계산에 필요한 여유분은 자동으로 추가됩니다.")]
    public int initialLoadCount = 200;

    [Tooltip("과거 방향으로 이동했을 때 한 번에 추가로 요청할 캔들 수")]
    public int olderLoadCount = 120;

    [Tooltip("현재 로딩된 데이터의 왼쪽 끝에서 이 개수만큼 가까워지면 과거 데이터를 미리 요청")]
    public int prefetchThreshold = 30;

    [Header("Search UI")]
    public TMP_Text stockCodeText;
    public TMP_Text stockNameText;
    public TMP_Text stockPriceText;
    [Header("주문창 상단: 차트 상단과 실시간 연동")]
    public TMP_Text orderPanelStockNameText;
    public TMP_Text orderPanelCurrentPriceText;
    public TMP_Text orderPanelChangeText;

    [Header("Stock Summary UI")]
    public TMP_Text openPriceText;
    public TMP_Text highPriceText;
    public TMP_Text lowPriceText;
    public TMP_Text volumeText;

    [Header("Trade UI")]
    public TMP_InputField orderPriceInput;
    public TMP_InputField quantityInput;

    public TMP_Dropdown orderTypeDropdown;

    public Button orderButton;

    public TMP_Text tradeMessageText;
    [Header("주문 가능 수량/금액 및 예상 금액")]
    public TMP_Text availableCashText;
    public TMP_Text availableQuantityText;
    public TMP_Text estimatedAmountText;
    public TMP_Text feeText;
    public TMP_Text taxText;

    private bool isTrading;

    // 시장가 선택 시 실시간 가격을 Input에 계속 반영
    private bool followRealtimeOrderPrice = false;

    [Header("Price Change UI")]
    public TMP_Text changeText;

    [Header("Trade Price Buttons")]
    public GameObject orderPriceMinusButton;
    public GameObject orderPricePlusButton;
    public GameObject currentPriceButton;

    private bool isLoading = false;

    private List<CandlePoint> allPoints = new List<CandlePoint>();

    private int viewEndIndex = -1;
    private bool followLatest = true;

    private Vector2 lastMousePos;
    private bool isDragging = false;
    private float dragAccum = 0f;

    private Coroutine loadRoutine;
    private Coroutine olderLoadRoutine;
    private NativeWebSocket.WebSocket priceSocket;

    private bool isMinuteMode = false;

    private bool isLoadingOlder = false;
    private bool hasMoreOlder = true;
    private string nextCandleCursor = null;
    private int candleRequestGeneration = 0;

    private void Start()
    {
        EnsureChartInputBlocker();
        InitChart();

        isMinuteMode = false;
        period = "all";
        interval = "1d";
        visibleCount = 60;
        maxVisibleCount = 3000;

        UpdateChartPeriodButtonColors();

        // 종목을 선택했을 때 LoadStock()에서 필요한 구간만 불러온다.
    }

    private void EnsureChartInputBlocker()
    {
        if (chartTouchArea == null)
        {
            Debug.LogWarning("Chart Touch Area가 연결되지 않아 차트 입력 범위를 제한할 수 없습니다.");
            return;
        }

        Image touchAreaImage = chartTouchArea.GetComponent<Image>();
        if (touchAreaImage != null)
        {
            touchAreaImage.raycastTarget = true;
        }

        if (chartTouchArea.GetComponent<ChartTouchInputBlocker>() == null)
        {
            chartTouchArea.gameObject.AddComponent<ChartTouchInputBlocker>();
        }
    }

    private void Update()
    {
        UpdateOrderEstimateUI();
        HandleMobileTouch();

        if (!isTouchingChart)
        {
            HandleMouseDrag();
            HandleMouseWheel();
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        if (priceSocket != null)
        {
            priceSocket.DispatchMessageQueue();
        }
#endif
    }

    private void HandleMobileTouch()
    {
        Debug.Log("HandleMobileTouch 실행");

        isTouchingChart = false;
        isPinching = false;

        var touches = Touch.activeTouches;

        Debug.Log("activeTouches Count = " + touches.Count);

        if (touches.Count <= 0)
        {
            ResetMobileTouch();
            return;
        }

        if (allPoints == null || allPoints.Count == 0)
        {
            Debug.Log("캔들 데이터 없음");
            return;
        }

        Vector2 pos0 = touches[0].screenPosition;

        if (!IsInsideChartArea(pos0))
        {
            Debug.Log("첫 번째 손가락 차트 영역 밖");
            ResetMobileTouch();
            return;
        }

        isTouchingChart = true;

        if (touches.Count >= 2)
        {
            Vector2 pos1 = touches[1].screenPosition;

            if (!IsInsideChartArea(pos1))
            {
                Debug.Log("두 번째 손가락 차트 영역 밖");
                ResetPinch();
                return;
            }

            isPinching = true;
            HandleMobilePinch(pos0, pos1);
        }
        else
        {
            ResetPinch();
            HandleMobileDrag(pos0);
        }
    }

    private void HandleMobileDrag(Vector2 currentTouchPos)
    {
        var touches = Touch.activeTouches;

        if (touches.Count <= 0)
            return;

        if (touches[0].phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            lastTouchPos = currentTouchPos;
            touchDragAccum = 0f;
            return;
        }

        float deltaX = currentTouchPos.x - lastTouchPos.x;
        lastTouchPos = currentTouchPos;

        touchDragAccum += deltaX * touchDragSensitivity;

        if (Mathf.Abs(touchDragAccum) >= 1f)
        {
            int moveCount = Mathf.RoundToInt(touchDragAccum);

            // 오른쪽으로 밀면 과거/현재 방향이 반대로 느껴지면 여기 부호만 바꾸면 됨
            viewEndIndex -= moveCount;

            viewEndIndex = Mathf.Clamp(viewEndIndex, visibleCount - 1, allPoints.Count - 1);
            followLatest = viewEndIndex >= allPoints.Count - 1;

            Debug.Log($"모바일 차트 이동: moveCount={moveCount}, viewEndIndex={viewEndIndex}");

            touchDragAccum = 0f;
            DrawVisibleCandles();
            CheckNeedOlderCandles();
        }
    }

    private void HandleMobilePinch(Vector2 pos0, Vector2 pos1)
    {
        float currentDistance = Vector2.Distance(pos0, pos1);

        Debug.Log("HandleMobilePinch 실행");

        if (lastPinchDistance < 0f)
        {
            lastPinchDistance = currentDistance;
            pinchAccum = 0f;
            return;
        }

        float delta = currentDistance - lastPinchDistance;
        lastPinchDistance = currentDistance;

        Debug.Log("핀치 delta = " + delta);

        pinchAccum += delta;

        if (Mathf.Abs(pinchAccum) < pinchThreshold)
        {
            return;
        }

        int oldVisibleCount = visibleCount;

        if (pinchAccum > 0f)
        {
            // 손가락 벌림 = 확대 = 캔들 개수 감소
            visibleCount -= pinchZoomStep;
        }
        else
        {
            // 손가락 오므림 = 축소 = 캔들 개수 증가
            visibleCount += pinchZoomStep;
        }

        pinchAccum = 0f;

        visibleCount = Mathf.Clamp(visibleCount, minVisibleCount, maxVisibleCount);
        visibleCount = Mathf.Min(visibleCount, allPoints.Count);

        Debug.Log($"visibleCount: {oldVisibleCount} -> {visibleCount}");

        if (followLatest)
        {
            viewEndIndex = allPoints.Count - 1;
        }
        else
        {
            viewEndIndex = Mathf.Clamp(viewEndIndex, visibleCount - 1, allPoints.Count - 1);
        }

        if (visibleCount != oldVisibleCount)
        {
            DrawVisibleCandles();
            CheckNeedOlderCandles();
        }
    }

    private void ResetMobileTouch()
    {
        isTouchingChart = false;
        touchDragAccum = 0f;
        ResetPinch();
    }

    private void ResetPinch()
    {
        isPinching = false;
        lastPinchDistance = -1f;
        pinchAccum = 0f;
    }

    private bool IsInsideChartArea(Vector2 screenPosition)
    {
        if (chartTouchArea == null)
        {
            return false;
        }

        Canvas canvas = chartTouchArea.GetComponentInParent<Canvas>();
        Camera eventCamera = null;

        // Screen Space - Overlay는 카메라 없이 판정한다.
        // Screen Space - Camera 또는 World Space에서는 Canvas 카메라를 사용한다.
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            chartTouchArea,
            screenPosition,
            eventCamera
        );
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        StopChartRequests();
    }

    private async void OnApplicationQuit()
    {
        if (priceSocket != null)
        {
            await priceSocket.Close();
            priceSocket = null;
        }
    }

    private IEnumerator SearchFlow()
    {
        if (priceSocket != null)
        {
            var closeTask = priceSocket.Close();

            while (!closeTask.IsCompleted)
            {
                yield return null;
            }

            priceSocket = null;
        }

        yield return LoadStockInfo();

        yield return new WaitForSeconds(0.2f);

        // 전체 기간 워밍업 없이 현재 화면과 이동평균 계산에 필요한 구간만 요청
        yield return LoadInitialCandleWindow();

        // 차트 보여주기 전에 포트폴리오 먼저 불러오기
        yield return LoadPortfolio();

        yield return StartCoroutine(panelManager.ShowChartAfterLoading());

        yield return new WaitForSeconds(1.0f);

        yield return ConnectPriceWebSocket();
    }

    private IEnumerator ReconnectPriceWebSocket()
    {
        if (priceSocket != null)
        {
            var closeTask = priceSocket.Close();

            while (!closeTask.IsCompleted)
            {
                yield return null;
            }

            priceSocket = null;
        }

        yield return ConnectPriceWebSocket();
    }

    private IEnumerator ConnectPriceWebSocket()
    {
        string wsUrl = $"{ServerConfig.WebSocketBaseUrl}/ws/price/{symbol}";
        priceSocket = new NativeWebSocket.WebSocket(wsUrl);

        priceSocket.OnOpen += () =>
        {
            Debug.Log("가격 WebSocket 연결됨: " + symbol);
        };

        priceSocket.OnError += (e) =>
        {
            Debug.LogError("가격 WebSocket 에러: " + e);
        };

        priceSocket.OnClose += (e) =>
        {
            Debug.Log("가격 WebSocket 종료");
        };

        priceSocket.OnMessage += (bytes) =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            RealtimePriceMessage msg = JsonConvert.DeserializeObject<RealtimePriceMessage>(json);

            if (msg == null) return;

            currentPrice = msg.price;

            stockPriceText.text =
                $"₩{currentPrice:N0}";

            if (followRealtimeOrderPrice && orderPriceInput != null)
            {
                orderPriceInput.text =
                    currentPrice.ToString("N0");
            }

            if (previousClosePrice > 0)
            {
                int realtimeChange = currentPrice - previousClosePrice;
                double realtimeChangeRate = ((double)realtimeChange / previousClosePrice) * 100.0;

                UpdateChangeText(realtimeChange, realtimeChangeRate);
            }

            if (isMinuteMode && msg.candle != null)
            {
                ApplyLatestCandle(msg.candle);
            }
        };

        var connectTask = priceSocket.Connect();

        while (!connectTask.IsCompleted)
        {
            yield return null;
        }
    }

    private IEnumerator LoadStockInfo()
    {
        string url =
            $"{ServerConfig.HttpBaseUrl}/stock-info?symbol={symbol}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            // Public market-data endpoint: account authentication is not required.
            req.timeout = 30;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    "종목 정보 요청 실패: " +
                    req.downloadHandler.text
                );

                if (stockCodeText != null)
                {
                    stockCodeText.text = symbol;
                }

                yield break;
            }

            StockInfoResponse info =
                JsonConvert.DeserializeObject<StockInfoResponse>(
                    req.downloadHandler.text
                );

            if (info == null)
            {
                yield break;
            }

            currentPrice = info.price;
            previousClosePrice = info.price - info.change;

            // 회사 이름
            if (stockNameText != null)
            {
                stockNameText.text =
                    string.IsNullOrEmpty(info.name)
                        ? symbol
                        : info.name;
            }

            // 종목코드와 시장
            if (stockCodeText != null)
            {
                stockCodeText.text =
                    string.IsNullOrEmpty(info.market)
                        ? info.symbol
                        : $"{info.symbol} · {info.market}";
            }

            // 현재가
            if (stockPriceText != null)
            {
                stockPriceText.text =
                    $"₩{info.price:N0}";
            }

            UpdateChangeText(
                info.change,
                info.change_rate
            );

            // 시가
            if (openPriceText != null)
            {
                openPriceText.text =
                    info.open_price > 0
                        ? info.open_price.ToString("N0")
                        : "-";
            }

            // 고가
            if (highPriceText != null)
            {
                highPriceText.text =
                    info.high_price > 0
                        ? info.high_price.ToString("N0")
                        : "-";
            }

            // 저가
            if (lowPriceText != null)
            {
                lowPriceText.text =
                    info.low_price > 0
                        ? info.low_price.ToString("N0")
                        : "-";
            }

            // 거래량
            if (volumeText != null)
            {
                volumeText.text =
                    info.volume > 0
                        ? info.volume.ToString("N0")
                        : "-";
            }
        }
    }

    private void InitChart()
    {
        chart.RemoveData();

        chart.AddSerie<Line>("MA5");
        chart.AddSerie<Line>("MA20");
        chart.AddSerie<Line>("MA60");
        chart.AddSerie<Line>("MA120");
        chart.AddSerie<Candlestick>("Price");

        SetLineColor(0, new Color(0f, 1f, 0f));
        SetLineColor(1, new Color(1f, 0f, 0f));
        SetLineColor(2, new Color(1f, 0.5f, 0f));
        SetLineColor(3, new Color(0.6f, 0f, 1f));

        SetCandleColor(4);

        for (int i = 0; i <= 3; i++)
        {
            var s = chart.GetSerie(i);
            s.symbol.show = false;
            s.animation.enable = false;
            s.lineStyle.width = 1f;
        }

        chart.GetSerie(4).animation.enable = false;
    }

    private void SetLineColor(int index, Color color)
    {
        var serie = chart.GetSerie(index);

        if (serie != null)
        {
            serie.lineStyle.color = color;
        }
    }

    private void SetCandleColor(int index)
    {
        var serie = chart.GetSerie(index);

        if (serie == null)
        {
            return;
        }

        Color riseColor;
        Color fallColor;

        ColorUtility.TryParseHtmlString("#F66B24", out riseColor);
        ColorUtility.TryParseHtmlString("#426340", out fallColor);

        serie.itemStyle.color = riseColor;
        serie.itemStyle.color0 = fallColor;

        serie.itemStyle.borderColor = riseColor;
        serie.itemStyle.borderColor0 = fallColor;

        serie.itemStyle.borderWidth = 0;
    }

    public void SetMinuteChart()
    {
        isMinuteMode = true;

        period = "1d";
        interval = "1m";
        visibleCount = 120;
        maxVisibleCount = 400;

        UpdateChartPeriodButtonColors();
        ReloadFullChart();
    }

    public void SetDailyChart()
    {
        isMinuteMode = false;

        period = "all";
        interval = "1d";
        visibleCount = 60;
        maxVisibleCount = 3000;

        UpdateChartPeriodButtonColors();
        ReloadFullChart();
    }

    public void SetWeeklyChart()
    {
        isMinuteMode = false;

        period = "all";
        interval = "1wk";
        visibleCount = 60;
        maxVisibleCount = 1000;

        UpdateChartPeriodButtonColors();
        ReloadFullChart();
    }

    public void SetMonthlyChart()
    {
        isMinuteMode = false;

        period = "all";
        interval = "1mo";
        visibleCount = 60;
        maxVisibleCount = 500;

        UpdateChartPeriodButtonColors();
        ReloadFullChart();
    }

    public void SetYearlyChart()
    {
        isMinuteMode = false;

        period = "all";
        interval = "1y";
        visibleCount = 30;
        maxVisibleCount = 100;

        UpdateChartPeriodButtonColors();
        ReloadFullChart();
    }

    private void UpdateChartPeriodButtonColors()
    {
        ColorUtility.TryParseHtmlString("#F66B24", out Color selectedColor);
        Color normalColor = Color.black;

        SetChartPeriodTextColor(minuteChartButtonText, interval == "1m", selectedColor, normalColor);
        SetChartPeriodTextColor(dailyChartButtonText, interval == "1d", selectedColor, normalColor);
        SetChartPeriodTextColor(weeklyChartButtonText, interval == "1wk", selectedColor, normalColor);
        SetChartPeriodTextColor(monthlyChartButtonText, interval == "1mo", selectedColor, normalColor);
        SetChartPeriodTextColor(yearlyChartButtonText, interval == "1y", selectedColor, normalColor);
    }

    private static void SetChartPeriodTextColor(
        TMP_Text target,
        bool selected,
        Color selectedColor,
        Color normalColor
    )
    {
        if (target != null)
            target.color = selected ? selectedColor : normalColor;
    }

    private void ReloadFullChart()
    {
        StopOnlyCandleRequests();
        ResetCandleWindowState();
        SetChartLoadingVisible(true);
        loadRoutine = StartCoroutine(LoadInitialCandleWindow());
    }

    private void SetChartLoadingVisible(bool visible)
    {
        if (chartLoadingPanel != null &&
            chartLoadingPanel.activeSelf != visible)
        {
            chartLoadingPanel.SetActive(visible);
        }
    }

    private void HideChartLoadingIfCurrent(int generation)
    {
        // 이전 요청의 늦은 응답이 현재 요청의 로딩 패널을 끄지 않게 합니다.
        if (generation == candleRequestGeneration)
            SetChartLoadingVisible(false);
    }

    public void GoLatest()
    {
        followLatest = true;

        if (allPoints != null && allPoints.Count > 0)
        {
            viewEndIndex = allPoints.Count - 1;
            DrawVisibleCandles();
        }
    }

    private void ResetCandleWindowState()
    {
        candleRequestGeneration++;
        isLoading = false;
        isLoadingOlder = false;
        hasMoreOlder = true;
        nextCandleCursor = null;
        followLatest = true;
        viewEndIndex = -1;
        allPoints.Clear();
    }

    private int GetInitialRequestCount()
    {
        int movingAveragePadding = 0;

        if (showMA120)
            movingAveragePadding = 120;
        else if (showMA60)
            movingAveragePadding = 60;
        else if (showMA20)
            movingAveragePadding = 20;
        else if (showMA5)
            movingAveragePadding = 5;

        int count = Mathf.Max(initialLoadCount, visibleCount + movingAveragePadding - 1);
        return Mathf.Clamp(count, 10, 500);
    }

    private IEnumerator LoadInitialCandleWindow()
    {
        isLoading = true;

        int generation = candleRequestGeneration;
        string requestedSymbol = symbol;
        string requestedInterval = interval;
        int requestCount = GetInitialRequestCount();

        string url =
            $"{ServerConfig.HttpBaseUrl}/candles/window?symbol={requestedSymbol}" +
            $"&interval={requestedInterval}&limit={requestCount}";

        Debug.Log("초기 캔들 구간 요청: " + url);

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (generation != candleRequestGeneration ||
                requestedSymbol != symbol || requestedInterval != interval)
            {
                Debug.Log("이전 캔들 요청 응답 무시");
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("초기 캔들 요청 실패: " + req.downloadHandler.text);
                isLoading = false;
                HideChartLoadingIfCurrent(generation);
                yield break;
            }

            CandleWindowResponse response =
                JsonConvert.DeserializeObject<CandleWindowResponse>(req.downloadHandler.text);

            if (response == null || response.points == null || response.points.Count == 0)
            {
                Debug.LogError("초기 캔들 데이터 없음");
                isLoading = false;
                HideChartLoadingIfCurrent(generation);
                yield break;
            }

            allPoints = response.points;
            nextCandleCursor = response.next_cursor;
            hasMoreOlder = response.has_more;

            viewEndIndex = allPoints.Count - 1;
            followLatest = true;

            DrawVisibleCandles();
        }

        isLoading = false;
        HideChartLoadingIfCurrent(generation);
    }

    private void CheckNeedOlderCandles()
    {
        if (isLoading || isLoadingOlder || !hasMoreOlder)
            return;

        if (allPoints == null || allPoints.Count == 0)
            return;

        if (string.IsNullOrEmpty(nextCandleCursor))
            return;

        int startIndex = Mathf.Max(0, viewEndIndex - visibleCount + 1);

        if (startIndex <= prefetchThreshold)
        {
            olderLoadRoutine = StartCoroutine(LoadOlderCandleWindow());
        }
    }

    private IEnumerator LoadOlderCandleWindow()
    {
        if (isLoadingOlder || !hasMoreOlder || string.IsNullOrEmpty(nextCandleCursor))
            yield break;

        isLoadingOlder = true;

        int generation = candleRequestGeneration;
        string requestedSymbol = symbol;
        string requestedInterval = interval;
        string requestedCursor = nextCandleCursor;

        string url =
            $"{ServerConfig.HttpBaseUrl}/candles/window?symbol={requestedSymbol}" +
            $"&interval={requestedInterval}&limit={Mathf.Clamp(olderLoadCount, 10, 500)}" +
            $"&before={UnityWebRequest.EscapeURL(requestedCursor)}";

        Debug.Log("과거 캔들 구간 요청: " + url);

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (generation != candleRequestGeneration ||
                requestedSymbol != symbol || requestedInterval != interval)
            {
                isLoadingOlder = false;
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("과거 캔들 요청 실패: " + req.downloadHandler.text);
                isLoadingOlder = false;
                yield break;
            }

            CandleWindowResponse response =
                JsonConvert.DeserializeObject<CandleWindowResponse>(req.downloadHandler.text);

            if (response == null || response.points == null || response.points.Count == 0)
            {
                hasMoreOlder = false;
                nextCandleCursor = null;
                isLoadingOlder = false;
                yield break;
            }

            HashSet<string> existingKeys = new HashSet<string>();

            foreach (CandlePoint point in allPoints)
            {
                existingKeys.Add(GetCandleIdentity(point));
            }

            List<CandlePoint> olderPoints = new List<CandlePoint>();

            foreach (CandlePoint point in response.points)
            {
                if (existingKeys.Add(GetCandleIdentity(point)))
                {
                    olderPoints.Add(point);
                }
            }

            if (olderPoints.Count > 0)
            {
                allPoints.InsertRange(0, olderPoints);

                // 앞에 데이터가 추가되어도 사용자가 보고 있던 위치가 움직이지 않게 보정
                viewEndIndex += olderPoints.Count;
                DrawVisibleCandles();
            }

            nextCandleCursor = response.next_cursor;
            hasMoreOlder = response.has_more;
        }

        isLoadingOlder = false;
        olderLoadRoutine = null;
    }

    private string GetCandleIdentity(CandlePoint point)
    {
        if (point == null)
            return string.Empty;

        return $"{point.date}|{point.open}|{point.high}|{point.low}|{point.close}";
    }

    private void ApplyLatestCandle(CandlePoint latest)
    {
        if (allPoints == null)
        {
            allPoints = new List<CandlePoint>();
        }

        if (allPoints.Count == 0)
        {
            allPoints.Add(latest);
            viewEndIndex = 0;
            DrawVisibleCandles();
            return;
        }

        CandlePoint last = allPoints[allPoints.Count - 1];

        bool sameCandle =
            last.date == latest.date &&
            last.open == latest.open &&
            last.high == latest.high &&
            last.low == latest.low &&
            last.close == latest.close;

        if (sameCandle)
        {
            return;
        }

        if (last.date == latest.date)
        {
            allPoints[allPoints.Count - 1] = latest;
        }
        else
        {
            allPoints.Add(latest);
        }

        if (followLatest)
        {
            viewEndIndex = allPoints.Count - 1;
        }

        DrawVisibleCandles();
    }

    private void DrawVisibleCandles()
    {
        if (chart == null)
        {
            Debug.LogError("Chart 없음");
            return;
        }

        if (allPoints == null || allPoints.Count == 0)
        {
            return;
        }

        chart.ClearData();

        visibleCount = Mathf.Clamp(visibleCount, minVisibleCount, maxVisibleCount);
        visibleCount = Mathf.Min(visibleCount, allPoints.Count);

        viewEndIndex = Mathf.Clamp(viewEndIndex, visibleCount - 1, allPoints.Count - 1);

        int startIndex = Mathf.Max(0, viewEndIndex - visibleCount + 1);

        float minPrice = float.MaxValue;
        float maxPrice = float.MinValue;

        int chartIndex = 0;

        for (int i = startIndex; i <= viewEndIndex; i++)
        {
            CandlePoint p = allPoints[i];

            // 비정상 캔들 데이터 방어
            if (p.open <= 0 || p.high <= 0 || p.low <= 0 || p.close <= 0)
            {
                continue;
            }

            chart.AddXAxisData(p.date);

            float ma5 = CalculateMA(i, 5);
            float ma20 = CalculateMA(i, 20);
            float ma60 = CalculateMA(i, 60);
            float ma120 = CalculateMA(i, 120);

            if (showMA5 && !float.IsNaN(ma5) && !float.IsInfinity(ma5))
            {
                chart.AddData(0, chartIndex, ma5);
                minPrice = Mathf.Min(minPrice, ma5);
                maxPrice = Mathf.Max(maxPrice, ma5);
            }

            if (showMA20 && !float.IsNaN(ma20) && !float.IsInfinity(ma20))
            {
                chart.AddData(1, chartIndex, ma20);
                minPrice = Mathf.Min(minPrice, ma20);
                maxPrice = Mathf.Max(maxPrice, ma20);
            }

            if (showMA60 && !float.IsNaN(ma60) && !float.IsInfinity(ma60))
            {
                chart.AddData(2, chartIndex, ma60);
                minPrice = Mathf.Min(minPrice, ma60);
                maxPrice = Mathf.Max(maxPrice, ma60);
            }

            if (showMA120 && !float.IsNaN(ma120) && !float.IsInfinity(ma120))
            {
                chart.AddData(3, chartIndex, ma120);
                minPrice = Mathf.Min(minPrice, ma120);
                maxPrice = Mathf.Max(maxPrice, ma120);
            }

            chart.AddData(4, chartIndex, p.open, p.close, p.low, p.high);

            minPrice = Mathf.Min(minPrice, p.low);
            maxPrice = Mathf.Max(maxPrice, p.high);

            chartIndex++;
        }

        if (chartIndex == 0)
        {
            Debug.LogWarning("표시할 정상 캔들 데이터가 없음");
            return;
        }

        float padding = (maxPrice - minPrice) * 0.1f;
        if (padding <= 0f || float.IsNaN(padding) || float.IsInfinity(padding))
        {
            padding = 1f;
        }

        YAxis yAxis = chart.GetChartComponent<YAxis>();

        if (yAxis != null)
        {
            yAxis.minMaxType = Axis.AxisMinMaxType.Custom;
            yAxis.min = minPrice - padding;
            yAxis.max = maxPrice + padding;
            yAxis.axisLabel.numericFormatter = "N0";
        }

        chart.RefreshChart();
    }

    private float CalculateMA(int index, int period)
    {
        if (index - period + 1 < 0)
        {
            return float.NaN;
        }

        float sum = 0f;

        for (int i = index - period + 1; i <= index; i++)
        {
            sum += allPoints[i].close;
        }

        return sum / period;
    }

    public bool TryPrepareTrade(bool buy)
    {
        if (isTrading) return false;
        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn())
        { ShowTradeMessage("로그인이 필요합니다."); return false; }
        int quantity = GetQuantity();
        if (quantity <= 0) return false;
        bool limit = orderTypeDropdown == null || orderTypeDropdown.value == 0;
        int price = limit ? GetOrderPrice() : currentPrice;
        if (price <= 0) { ShowTradeMessage("주문가격/현재가를 확인하세요."); return false; }
        preparedSymbol = symbol;
        preparedName = stockNameText != null ? stockNameText.text : symbol;
        preparedSide = buy ? "BUY" : "SELL";
        preparedOrderType = limit ? "LIMIT" : "MARKET";
        preparedQuantity = quantity;
        preparedPrice = limit ? price : 0;
        preparedRequestId = Guid.NewGuid().ToString();
        return true;
    }

    public string GetPreparedTradeDetails()
    {
        if (string.IsNullOrEmpty(preparedRequestId)) return "주문 입력을 확인해 주세요.";
        long price = preparedOrderType == "LIMIT" ? preparedPrice : currentPrice;
        string side = preparedSide == "BUY" ? "매수" : "매도";
        return $"{preparedName} ({preparedSymbol})\n매매종류: {side}\n" +
            $"주문유형: {(preparedOrderType == "LIMIT" ? "지정가" : "시장가")}\n" +
            $"주문수량: {preparedQuantity:N0}주\n예상가격: {price:N0}원\n" +
            $"수수료: 0원\n세금: 0원\n예상금액: {price * preparedQuantity:N0}원\n" +
            (preparedOrderType == "MARKET" ? "최종 확인 시 서버가 조회한 시세로 체결됩니다.\n실제 체결가는 달라질 수 있습니다." :
             "조건 미충족 시 미체결 주문으로 접수됩니다.");
    }

    public string PreparedSideLabel => preparedSide == "BUY" ? "매수" : "매도";
    public string PreparedStockName => preparedName;
    public int PreparedQuantity => preparedQuantity;
    public long PreparedDisplayPrice => preparedOrderType == "LIMIT" ? preparedPrice : currentPrice;
    public void RefreshTradeConfirmationUI(string actionTitle = null, string actionDetails = null, bool interactable = true)
    {
        bool customAction = actionTitle != null;
        Color sideColor = panelManager != null
            ? (PreparedSideLabel == "매수" ? panelManager.buyColor : panelManager.sellColor)
            : (PreparedSideLabel == "매수" ? Color.red : Color.blue);
        SetConfirmationText(tradeConfirmationTitleText, customAction ? actionTitle : PreparedSideLabel + " 주문 확인");
        SetConfirmationText(tradeConfirmationDetailsText, customAction ? actionDetails : GetPreparedTradeDetails());
        SetConfirmationText(checkButtonText, customAction ? actionTitle : PreparedSideLabel + " 확인");
        if (!customAction)
        {
            SetConfirmationText(tradeConfirmationGuideText, PreparedSideLabel + " 주문 내용을 확인하신 후, [확인] 버튼을 클릭하세요.");
            SetConfirmationText(tradeConfirmationStockText, PreparedStockName);
            SetConfirmationText(tradeConfirmationSideText, PreparedSideLabel);
            if (tradeConfirmationSideText != null) tradeConfirmationSideText.color = sideColor;
            SetConfirmationText(tradeConfirmationQuantityText, $"{PreparedQuantity:N0}주");
            SetConfirmationText(tradeConfirmationPriceText, $"{PreparedDisplayPrice:N0}원");
            SetConfirmationText(tradeConfirmationFeeText, "0원");
            SetConfirmationText(tradeConfirmationTaxText, "0원");
            SetConfirmationText(tradeConfirmationAmountText, $"{PreparedDisplayPrice * PreparedQuantity:N0}원");
            SetConfirmationText(tradeConfirmationCurrentPriceText, $"{CurrentPrice:N0}원");
        }
        else
        {
            SetConfirmationText(tradeConfirmationGuideText, actionTitle + " 내용을 확인해 주세요.");
            SetConfirmationText(tradeConfirmationStockText, "");
            SetConfirmationText(tradeConfirmationSideText, "");
            SetConfirmationText(tradeConfirmationQuantityText, "");
            SetConfirmationText(tradeConfirmationPriceText, "");
            SetConfirmationText(tradeConfirmationFeeText, "");
            SetConfirmationText(tradeConfirmationTaxText, "");
            SetConfirmationText(tradeConfirmationAmountText, "");
            SetConfirmationText(tradeConfirmationCurrentPriceText, "");
        }
        if (checkButtonImage != null)
        {
            checkButtonImage.color = customAction ? new Color(1f, .4f, .1f) : sideColor;
            Button button = checkButtonImage.GetComponent<Button>();
            if (button != null) button.interactable = interactable;
        }
    }

    private static void SetConfirmationText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }

    public void RefreshCancelConfirmationUI(TradeOrderData order, string details, bool interactable)
    {
        // Reuse the buy/sell confirmation panel, with a cancel callback in the manager.
        RefreshTradeConfirmationUI("주문 취소 확인", details, interactable);
        SetConfirmationText(tradeConfirmationGuideText, "아래 미체결 주문을 취소하시겠습니까?");
        SetConfirmationText(tradeConfirmationStockText, order.name + " (" + order.symbol + ")");
        SetConfirmationText(tradeConfirmationSideText, order.SideLabel);
        if (tradeConfirmationSideText != null)
            tradeConfirmationSideText.color = order.side == "BUY" ? Color.red : Color.blue;
        int remaining = Math.Max(0, order.quantity - order.filledQuantity);
        long price = order.limitPrice ?? order.orderPrice;
        SetConfirmationText(tradeConfirmationQuantityText, $"{remaining:N0}주");
        SetConfirmationText(tradeConfirmationPriceText, $"{price:N0}원");
        SetConfirmationText(tradeConfirmationFeeText, "0원");
        SetConfirmationText(tradeConfirmationTaxText, "0원");
        // Cancellation is not a sale: no settlement proceeds are created.
        SetConfirmationText(tradeConfirmationAmountText, "해당 없음 (주문 취소)");
        SetConfirmationText(tradeConfirmationCurrentPriceText,
            order.symbol == symbol && currentPrice > 0 ? $"{currentPrice:N0}원" : "-");
        SetConfirmationText(checkButtonText, "취소 확인");
    }
    public void CancelPreparedTrade()
    {
        if (!isTrading) preparedRequestId = null;
    }
    public void ConfirmPreparedTrade()
    {
        if (string.IsNullOrEmpty(preparedRequestId))
        { ShowTradeMessage("먼저 주문 확인창을 열어 주세요."); return; }
        SubmitOrder(preparedSide == "BUY" ? "/buy" : "/sell", PreparedSideLabel);
    }
    public void RefreshPortfolio()
    {
        if (isActiveAndEnabled) StartCoroutine(LoadPortfolio());
    }
    public void ReportTradeMessage(string text) { ShowTradeMessage(text); }
    private void UpdateOrderEstimateUI()
    {
        bool limit = orderTypeDropdown == null || orderTypeDropdown.value == 0;
        int inputPrice = 0, quantity = 0;
        if (orderPriceInput != null) int.TryParse(orderPriceInput.text.Replace(",", "").Trim(), out inputPrice);
        if (quantityInput != null) int.TryParse(quantityInput.text.Replace(",", "").Trim(), out quantity);
        long price = Math.Max(0, limit ? inputPrice : currentPrice);
        if (estimatedAmountText != null) estimatedAmountText.text = $"{price * Math.Max(0, quantity):N0}원";
        if (feeText != null) feeText.text = "0원";
        if (taxText != null) taxText.text = "0원";
        if (LatestPortfolio == null)
        {
            if (availableCashText != null) availableCashText.text = "조회 전";
            if (availableQuantityText != null) availableQuantityText.text = "조회 전";
            if (orderButton != null) orderButton.interactable = false;
            return;
        }
        long available = Math.Max(0, LatestPortfolio.availableCash);
        if (availableCashText != null) availableCashText.text = $"{available:N0}원";
        long possible = price > 0 ? available / price : 0;
        if (panelManager != null && !panelManager.IsBuyMode)
        {
            possible = 0;
            foreach (Holding holding in LatestPortfolio.holdings ?? new List<Holding>())
                if (holding.symbol == symbol) possible = Math.Max(0, holding.availableQuantity);
        }
        if (availableQuantityText != null) availableQuantityText.text = $"{possible:N0}주";
        if (orderButton != null)
            orderButton.interactable = AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn()
                && !isTrading && (panelManager == null || !panelManager.IsOrderBusy)
                && price > 0 && quantity > 0 && quantity <= possible;
    }

    private void LateUpdate()
    {
        // Copy the same display data; this does not modify the limit-order price input.
        MirrorOrderHeader(stockNameText, orderPanelStockNameText, symbol);
        MirrorOrderHeader(stockPriceText, orderPanelCurrentPriceText,
            currentPrice > 0 ? $"{currentPrice:N0}원" : "-");
        MirrorOrderHeader(changeText, orderPanelChangeText, "-");
    }

    private static void MirrorOrderHeader(TMP_Text source, TMP_Text target, string fallback)
    {
        if (target == null || target == source) return;
        string value = source != null ? source.text : fallback;
        if (target.text != value) target.text = value;
        if (source != null && target.color != source.color) target.color = source.color;
    }

    public void OnClickBuy()
    {
        SubmitOrder("/buy", "매수");
    }

    public void OnClickSell()
    {
        SubmitOrder("/sell", "매도");
    }

    private void SubmitOrder(
        string endpoint,
        string actionName)
    {
        if (isTrading)
        {
            ShowTradeMessage(
                "현재 주문을 처리하고 있습니다."
            );

            return;
        }

        bool buy = endpoint == "/buy";
        if (string.IsNullOrEmpty(preparedRequestId) && !TryPrepareTrade(buy)) return;
        if (preparedSide != (buy ? "BUY" : "SELL"))
        { ShowTradeMessage("확인한 주문의 매매종류가 다릅니다. 다시 확인하세요."); return; }
        int quantity = preparedQuantity;

        ShowTradeMessage(
            $"{actionName} 주문 요청 중\n" +
            $"{quantity:N0}주"
        );

        StartCoroutine(
            SendTrade(
                endpoint,
                quantity
            )
        );
    }

    private int GetQuantity()
    {
        if (quantityInput == null)
        {
            Debug.LogWarning(
                "주문수량 입력창이 연결되지 않았습니다."
            );

            return 0;
        }

        string rawQuantity = quantityInput.text
            .Replace(",", "")
            .Trim();

        if (!int.TryParse(
            rawQuantity,
            out int quantity))
        {
            ShowTradeMessage(
                "주문수량을 숫자로 입력해 주세요."
            );

            return 0;
        }

        if (quantity <= 0)
        {
            ShowTradeMessage(
                "주문수량은 1주 이상이어야 합니다."
            );

            return 0;
        }

        return quantity;
    }

    private void ShowTradeMessage(string message)
    {
        Debug.Log(message);

        if (tradeMessageText != null)
        {
            tradeMessageText.text = message;
        }
    }

    private int GetOrderPrice()
    {
        if (orderPriceInput == null)
        {
            Debug.LogWarning(
                "주문가격 입력창이 연결되지 않았습니다."
            );

            return 0;
        }

        string rawPrice = orderPriceInput.text
            .Replace(",", "")
            .Trim();

        if (!int.TryParse(
            rawPrice,
            out int orderPrice))
        {
            ShowTradeMessage(
                "주문가격을 숫자로 입력해 주세요."
            );

            return 0;
        }

        if (orderPrice <= 0)
        {
            ShowTradeMessage(
                "주문가격은 1원 이상이어야 합니다."
            );

            return 0;
        }

        return orderPrice;
    }
    private IEnumerator SendTrade(
        string endpoint,
        int quantity
    )
    {
        if (isTrading)
            yield break;

        isTrading = true;

        SetTradeButtonsInteractable(
            false
        );


        // =========================================================
        // 주문 종류 확인
        // =========================================================

        bool isLimitOrder =
            preparedOrderType == "LIMIT";

        int orderPrice = 0;


        // =========================================================
        // 지정가 주문이면 주문가격 가져오기
        // =========================================================

        if (isLimitOrder)
        {
            orderPrice =
                preparedPrice;

            if (orderPrice <= 0)
            {
                isTrading = false;

                SetTradeButtonsInteractable(
                    true
                );

                yield break;
            }
        }


        // =========================================================
        // 요청 URL
        // =========================================================

        string url =
            $"{ServerConfig.HttpBaseUrl}{endpoint}";


        // =========================================================
        // 주문 데이터 생성
        // =========================================================

        TradeRequest trade =
            new TradeRequest
            {
                symbol =
                    preparedSymbol,

                quantity =
                    quantity,

                /*
                 * Dropdown
                 *
                 * 0 = 지정가
                 * 1 = 시장가
                 */
                order_type =
                    isLimitOrder
                        ? "LIMIT"
                        : "MARKET",

                /*
                 * 지정가
                 * → 입력 가격
                 *
                 * 시장가
                 * → null
                 */
                limit_price =
                    isLimitOrder
                        ? orderPrice
                        : null,
                client_request_id = preparedRequestId
            };


        // =========================================================
        // JSON 변환
        // =========================================================

        string json =
            JsonConvert.SerializeObject(
                trade
            );


        Debug.Log(
            "주문 요청: " +
            json
        );


        // =========================================================
        // HTTP 요청 생성
        // =========================================================

        using UnityWebRequest req =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST
            );


        byte[] bodyRaw =
            System.Text.Encoding.UTF8
            .GetBytes(
                json
            );


        req.uploadHandler =
            new UploadHandlerRaw(
                bodyRaw
            );


        req.downloadHandler =
            new DownloadHandlerBuffer();


        req.SetRequestHeader(
            "Content-Type",
            "application/json"
        );
        req.timeout = 30;


        // =========================================================
        // 로그인 확인
        // =========================================================

        if (
            AuthManager.Instance == null ||
            !AuthManager.Instance.IsLoggedIn()
        )
        {
            isTrading = false;

            SetTradeButtonsInteractable(
                true
            );


            ShowTradeMessage(
                "로그인이 필요합니다."
            );


            yield break;
        }


        AuthManager.Instance
            .AddAuthorizationHeader(
                req
            );


        // =========================================================
        // 서버 요청
        // =========================================================

        yield return
            req.SendWebRequest();


        // =========================================================
        // 주문 처리 상태 해제
        // =========================================================

        isTrading = false;

        SetTradeButtonsInteractable(
            true
        );


        // =========================================================
        // 로그인 만료
        // =========================================================

        if (req.responseCode == 401)
        {
            AuthManager.Instance?
                .Logout();


            ShowTradeMessage(
                "로그인이 만료되었습니다."
            );


            SceneManager.LoadScene(
                "Login"
            );


            yield break;
        }


        // =========================================================
        // 주문 실패
        // =========================================================

        if (
            req.result !=
            UnityWebRequest.Result.Success
        )
        {
            string errorMessage =
                GetServerErrorMessage(
                    req.downloadHandler.text
                );


            ShowTradeMessage(
                $"주문 실패: {errorMessage}"
            );
            if (req.responseCode == 0)
                ShowTradeMessage("통신 실패로 주문 결과가 불확실합니다. 다시 주문하기 전에 주문내역과 잔액을 확인하세요.");
            if (panelManager != null && panelManager.orderListLoader != null)
                panelManager.orderListLoader.Refresh();


            yield break;
        }


        // =========================================================
        // 서버 응답 파싱
        // =========================================================

        TradeResponse response =
            JsonConvert.DeserializeObject
            <TradeResponse>(
                req.downloadHandler.text
            );


        if (response == null)
        {
            ShowTradeMessage(
                "주문 결과를 읽지 못했습니다."
            );


            yield break;
        }


        // =========================================================
        // 주문 성공 메시지
        // =========================================================

        string orderTypeText =
            isLimitOrder
                ? "지정가"
                : "시장가";


        if (response.status == "PENDING")
        {
            ShowTradeMessage($"{response.message}\n{response.symbol} {response.quantity:N0}주\n미체결 주문내역에서 정정·취소할 수 있습니다.");
        }
        else if (isLimitOrder)
        {
            ShowTradeMessage(
                $"{response.message}\n" +
                $"{response.symbol} " +
                $"{response.quantity:N0}주\n" +
                $"{orderTypeText} {orderPrice:N0}원\n" +
                $"체결가 {response.price:N0}원"
            );
        }
        else
        {
            ShowTradeMessage(
                $"{response.message}\n" +
                $"{response.symbol} " +
                $"{response.quantity:N0}주\n" +
                $"{orderTypeText}\n" +
                $"체결가 {response.price:N0}원"
            );
        }


        // =========================================================
        // 주문수량 초기화
        // =========================================================

        preparedRequestId = null;
        if (panelManager != null) panelManager.CloseTradeConfirmation();
        TradeCompleted?.Invoke();
        if (quantityInput != null)
        {
            quantityInput.text = "0";
        }


        // =========================================================
        // 포트폴리오 새로고침
        // =========================================================

        yield return
            LoadPortfolio();
    }

    private void SetTradeButtonsInteractable(
        bool interactable
    )
    {
        if (interactable)
        {
            UpdateOrderEstimateUI();
            return;
        }
        if (orderButton != null)
        {
            orderButton.interactable =
                interactable;
        }
    }

    private string GetServerErrorMessage(
        string responseBody)
    {
        if (string.IsNullOrWhiteSpace(
            responseBody))
        {
            return "서버 응답이 없습니다.";
        }

        try
        {
            FastApiError error =
                JsonConvert.DeserializeObject<
                    FastApiError
                >(responseBody);

            if (
                error != null &&
                !string.IsNullOrWhiteSpace(
                    error.detail)
            )
            {
                return error.detail;
            }
        }
        catch
        {
            // 원문을 아래에서 반환
        }

        return responseBody;
    }

    [Serializable]
    private class FastApiError
    {
        public string detail;
    }

    private IEnumerator LoadPortfolio()
    {
        string url = $"{ServerConfig.HttpBaseUrl}/portfolio";

        Debug.Log("포트폴리오 요청 URL: " + url);

        using (UnityWebRequest req =
    UnityWebRequest.Get(url))
        {
            if (
                AuthManager.Instance == null ||
                !AuthManager.Instance.IsLoggedIn()
            )
            {
                ShowTradeMessage(
                    "로그인이 필요합니다."
                );

                yield break;
            }

            AuthManager.Instance
                .AddAuthorizationHeader(req);

            yield return req.SendWebRequest();

            if (req.responseCode == 401)
            {
                AuthManager.Instance.Logout();

                ShowTradeMessage(
                    "로그인이 만료되었습니다."
                );

                SceneManager.LoadScene("Login");
                yield break;
            }

            Debug.Log(
                "포트폴리오 응답 코드: " +
                req.responseCode
            );

            Debug.Log(
                "포트폴리오 응답 내용: " +
                req.downloadHandler.text
            );

            if (
                req.result !=
                UnityWebRequest.Result.Success
            )
            {
                string errorMessage =
                    GetServerErrorMessage(
                        req.downloadHandler.text
                    );

                ShowTradeMessage(
                    "포트폴리오 조회 실패: " +
                    errorMessage
                );

                yield break;
            }

            PortfolioResponse portfolio =
                JsonConvert.DeserializeObject<
                    PortfolioResponse
                >(
                    req.downloadHandler.text
                );

            if (portfolio == null)
            {
                ShowTradeMessage(
                    "포트폴리오 데이터를 읽지 못했습니다."
                );

                yield break;
            }
            LatestPortfolio = portfolio;
            PortfolioUpdated?.Invoke(portfolio);
        }
    }

    private void HandleMouseDrag()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            isDragging = IsInsideChartArea(mousePosition);
            lastMousePos = mousePosition;
            dragAccum = 0f;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (!isDragging || allPoints == null || allPoints.Count == 0)
        {
            return;
        }

        Vector2 currentMousePos = Mouse.current.position.ReadValue();
        float deltaX = currentMousePos.x - lastMousePos.x;
        lastMousePos = currentMousePos;

        dragAccum += deltaX * dragSensitivity;

        if (Mathf.Abs(dragAccum) >= 1f)
        {
            int moveCount = Mathf.RoundToInt(dragAccum);

            viewEndIndex -= moveCount;
            viewEndIndex = Mathf.Clamp(viewEndIndex, visibleCount - 1, allPoints.Count - 1);

            followLatest = viewEndIndex >= allPoints.Count - 1;

            dragAccum = 0f;
            DrawVisibleCandles();
            CheckNeedOlderCandles();
        }
    }

    private void HandleMouseWheel()
    {
        if (Mouse.current == null) return;

        if (!IsInsideChartArea(Mouse.current.position.ReadValue()))
        {
            return;
        }

        if (allPoints == null || allPoints.Count == 0)
        {
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
        {
            return;
        }

        int oldVisibleCount = visibleCount;

        if (scroll > 0)
        {
            visibleCount -= wheelZoomStep;
        }
        else
        {
            visibleCount += wheelZoomStep;
        }

        visibleCount = Mathf.Clamp(visibleCount, minVisibleCount, maxVisibleCount);
        visibleCount = Mathf.Min(visibleCount, allPoints.Count);

        if (followLatest)
        {
            viewEndIndex = allPoints.Count - 1;
        }
        else
        {
            viewEndIndex = Mathf.Clamp(viewEndIndex, visibleCount - 1, allPoints.Count - 1);
        }

        if (visibleCount != oldVisibleCount)
        {
            DrawVisibleCandles();
            CheckNeedOlderCandles();
        }
    }

    private void UpdateChangeText(
        int change,
        double changeRate
    )
    {
        if (changeText == null)
        {
            return;
        }

        if (change > 0)
        {
            changeText.text =
                $"▲ {change:N0}원 ({Math.Abs(changeRate):F2}%)";

            changeText.color = GetRiseColor();
        }
        else if (change < 0)
        {
            changeText.text =
                $"▼ {Math.Abs(change):N0}원 ({Math.Abs(changeRate):F2}%)";

            changeText.color = GetFallColor();
        }
        else
        {
            changeText.text = "0원 (0.00%)";
            changeText.color = GetNeutralColor();
        }
    }

    public void LoadStock(string newSymbol, string newName = "")
    {
        if (string.IsNullOrEmpty(newSymbol))
            return;

        StopChartRequests();

        // 종목을 바꿀 때 확대·축소 및 이동 위치 초기화
        ResetChartViewForCurrentInterval();

        ResetCandleWindowState();

        symbol = newSymbol;
        currentPrice = 0;
        previousClosePrice = 0;

        if (chart != null)
        {
            chart.ClearData();
            chart.RefreshChart();
        }

        if (stockNameText != null)
        {
            stockNameText.text =
                string.IsNullOrEmpty(newName)
                    ? "-"
                    : newName;
        }

        if (stockCodeText != null)
        {
            stockCodeText.text = symbol;
        }

        if (stockPriceText != null)
        {
            stockPriceText.text = "-";
        }

        if (changeText != null)
        {
            changeText.text = "-";
            changeText.color = GetNeutralColor();
        }

        if (openPriceText != null)
        {
            openPriceText.text = "-";
        }

        if (highPriceText != null)
        {
            highPriceText.text = "-";
        }

        if (lowPriceText != null)
        {
            lowPriceText.text = "-";
        }

        if (volumeText != null)
        {
            volumeText.text = "-";
        }

        if (stockPriceText != null)
            stockPriceText.text = "-";

        if (changeText != null)
            changeText.text = "-";

        loadRoutine = StartCoroutine(SearchFlow());
    }

    public void StopOnlyCandleRequests()
    {
        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }

        if (olderLoadRoutine != null)
        {
            StopCoroutine(olderLoadRoutine);
            olderLoadRoutine = null;
        }

        candleRequestGeneration++;
        isLoading = false;
        isLoadingOlder = false;
        SetChartLoadingVisible(false);
    }

    public void StopChartRequests()
    {
        StopOnlyCandleRequests();
        StopPriceSocket();
    }

    private async void StopPriceSocket()
    {
        if (priceSocket == null)
            return;

        NativeWebSocket.WebSocket socketToClose = priceSocket;
        priceSocket = null;

        try
        {
            await socketToClose.Close();
            Debug.Log("차트 WebSocket 수동 종료");
        }
        catch (Exception e)
        {
            Debug.LogWarning("차트 WebSocket 종료 중 오류: " + e.Message);
        }
    }

    public void ReloadOnlyCandles()
    {
        StopOnlyCandleRequests();
        ResetCandleWindowState();
        loadRoutine = StartCoroutine(LoadInitialCandleWindow());
    }

    private void ResetChartViewForCurrentInterval()
    {
        switch (interval)
        {
            case "1m":
                visibleCount = 120;
                maxVisibleCount = 400;
                break;

            case "1y":
                visibleCount = 30;
                maxVisibleCount = 100;
                break;

            case "1wk":
                visibleCount = 60;
                maxVisibleCount = 1000;
                break;

            case "1mo":
                visibleCount = 60;
                maxVisibleCount = 500;
                break;

            default:
                visibleCount = 60;
                maxVisibleCount = 3000;
                break;
        }

        followLatest = true;
        viewEndIndex = -1;

        isDragging = false;
        dragAccum = 0f;

        isTouchingChart = false;
        touchDragAccum = 0f;

        isPinching = false;
        lastPinchDistance = -1f;
        pinchAccum = 0f;
    }

    private Color GetRiseColor()
    {
        ColorUtility.TryParseHtmlString(
            "#F66B24",
            out Color color
        );

        return color;
    }

    private Color GetFallColor()
    {
        // 하늘색
        ColorUtility.TryParseHtmlString(
            "#4A90E2",
            out Color color
        );

        return color;
    }

    private Color GetNeutralColor()
    {
        ColorUtility.TryParseHtmlString(
            "#707070",
            out Color color
        );

        return color;
    }

    public void PrepareOrderPanel()
    {
        // 처음 열 때 항상 지정가
        if (orderTypeDropdown != null)
        {
            orderTypeDropdown.SetValueWithoutNotify(0);
        }

        followRealtimeOrderPrice = false;

        // 지정가는 직접 수정 가능
        if (orderPriceInput != null)
        {
            orderPriceInput.interactable = true;

            if (currentPrice > 0)
            {
                orderPriceInput.text =
                    currentPrice.ToString("N0");
            }
            else
            {
                orderPriceInput.text = "";
            }
        }
        // 주문수량 기본값
        if (quantityInput != null)
        {
            quantityInput.text = "0";
        }

        SetLimitOrderControlsVisible(true);
    }

    public void OnOrderTypeChanged(int value)
    {
        // 0 = 지정가
        if (value == 0)
        {
            followRealtimeOrderPrice = false;

            if (orderPriceInput != null)
            {
                orderPriceInput.interactable = true;

                if (currentPrice > 0)
                {
                    orderPriceInput.text =
                        currentPrice.ToString("N0");
                }
            }

            SetLimitOrderControlsVisible(true);
        }

        // 1 = 시장가
        else if (value == 1)
        {
            followRealtimeOrderPrice = true;

            if (orderPriceInput != null)
            {
                orderPriceInput.interactable = false;

                if (currentPrice > 0)
                {
                    orderPriceInput.text =
                        currentPrice.ToString("N0");
                }
            }

            SetLimitOrderControlsVisible(false);
        }
    }

    private void SetLimitOrderControlsVisible(
    bool visible
)
    {
        if (orderPriceMinusButton != null)
            orderPriceMinusButton.SetActive(visible);

        if (orderPricePlusButton != null)
            orderPricePlusButton.SetActive(visible);

        if (currentPriceButton != null)
            currentPriceButton.SetActive(visible);
    }

    public void DecreaseOrderPrice()
    {
        if (orderPriceInput == null)
            return;

        int price = GetOrderPrice();

        price -= 500;

        if (price < 0)
            price = 0;

        orderPriceInput.text =
            price.ToString("N0");
    }


    public void IncreaseOrderPrice()
    {
        if (orderPriceInput == null)
            return;

        int price = GetOrderPrice();

        price += 500;

        orderPriceInput.text =
            price.ToString("N0");
    }


    public void DecreaseQuantity()
    {
        if (quantityInput == null)
            return;

        int quantity = 0;

        int.TryParse(
            quantityInput.text.Replace(",", ""),
            out quantity
        );

        quantity -= 1;

        if (quantity < 0)
            quantity = 0;

        quantityInput.text =
            quantity.ToString("N0");
    }


    public void IncreaseQuantity()
    {
        if (quantityInput == null)
            return;

        int quantity = 0;

        int.TryParse(
            quantityInput.text.Replace(",", ""),
            out quantity
        );

        quantity += 1;

        quantityInput.text =
            quantity.ToString("N0");
    }

    public void SetOrderPriceToCurrentPrice()
    {
        if (
            orderPriceInput == null ||
            currentPrice <= 0
        )
        {
            return;
        }

        orderPriceInput.text =
            currentPrice.ToString("N0");
    }

    [Serializable]
    private class CandleWindowResponse
    {
        public string symbol;
        public string interval;
        public List<CandlePoint> points;
        public bool has_more;
        public string next_cursor;
    }
}

/// <summary>
/// 차트 영역의 UI 입력이 부모 ScrollRect로 전달되지 않도록 막는다.
/// 실제 차트 이동과 확대/축소는 StockCandlestickLoader가 처리한다.
/// </summary>
public class ChartTouchInputBlocker : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IInitializePotentialDragHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler
{
    public void OnPointerDown(PointerEventData eventData) => eventData.Use();
    public void OnPointerUp(PointerEventData eventData) => eventData.Use();

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData) => eventData.Use();
    public void OnDrag(PointerEventData eventData) => eventData.Use();
    public void OnEndDrag(PointerEventData eventData) => eventData.Use();
    public void OnScroll(PointerEventData eventData) => eventData.Use();
}


