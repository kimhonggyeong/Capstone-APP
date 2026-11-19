using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Networking;
using UnityEngine.UI;
using XCharts.Runtime;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class ScenarioCandlestickLoader : MonoBehaviour
{
    public enum ViewPeriod
    {
        OneDay,
        OneWeek,
        OneMonth,
        ThreeMonths,
        SixMonths,
        OneYear
    }

    [Header("시나리오 서버")]

    [Header("차트")]
    [SerializeField]
    private CandlestickChart chart;

    [Tooltip("조회 기간이 아니라 캔들 하나의 단위입니다.")]
    [SerializeField]
    private ViewPeriod period = ViewPeriod.OneDay;

    [Header("봉 변경 버튼의 TMP 텍스트")]
    [SerializeField] private TMP_Text oneDayButtonText;
    [SerializeField] private TMP_Text oneWeekButtonText;
    [SerializeField] private TMP_Text oneMonthButtonText;
    [SerializeField] private TMP_Text threeMonthsButtonText;
    [SerializeField] private TMP_Text sixMonthsButtonText;
    [SerializeField] private TMP_Text oneYearButtonText;

    [Header("상태 표시 - 선택")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text periodText;

    [Header("선택 종목 정보 패널 - 선택")]
    [SerializeField]
    private ScenarioStockInfoView stockInfoView;

    [Header("캔들 색상")]
    [SerializeField]
    private Color riseColor = new Color32(246, 107, 36, 255);

    [SerializeField]
    private Color fallColor = new Color32(74, 144, 226, 255);

    [Header("차트 드래그 / 확대·축소")]
    [Tooltip("차트 위에 만든 투명 Image의 RectTransform을 연결")]
    [SerializeField] private RectTransform chartTouchArea;
    [SerializeField] private int initialVisibleCount = 60;
    [SerializeField] private int minVisibleCount = 10;
    [SerializeField] private int maxVisibleCount = 300;
    [SerializeField] private float dragSensitivity = 0.05f;
    [SerializeField] private int wheelZoomStep = 5;
    [SerializeField] private float touchDragSensitivity = 0.05f;
    [SerializeField] private float pinchThreshold = 12f;
    [SerializeField] private int pinchZoomStep = 5;

    // 서버에서 받은 전체 일봉을 보관합니다.
    private readonly List<Candle> dailyCandles = new List<Candle>();
    private readonly List<Candle> displayCandles = new List<Candle>();

    private int visibleCount;
    private int viewEndIndex = -1;
    private bool isTouchingChart;
    private Vector2 lastTouchPosition;
    private float touchDragAccum;
    private float lastPinchDistance = -1f;
    private float pinchAccum;
    private bool isMouseDragging;
    private Vector2 lastMousePosition;
    private float mouseDragAccum;

    private string sessionId;
    private string assetId;
    private DateTime marketDate;

    private bool hasSelection;
    private bool initialized;
    private bool dataLoaded;

    private int requestVersion;
    private UnityWebRequest activeRequest;

    private void Start()
    {
        EnsureChartInputBlocker();
    }

    private void Update()
    {
        HandleMobileTouch();

        if (!isTouchingChart)
        {
            HandleMouseDrag();
            HandleMouseWheel();
        }
    }

    private void EnsureChartInputBlocker()
    {
        if (chartTouchArea == null)
        {
            Debug.LogWarning("[Scenario Chart] Chart Touch Area가 연결되지 않았습니다.");
            return;
        }

        Image image = chartTouchArea.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;

        if (chartTouchArea.GetComponent<ChartTouchInputBlocker>() == null)
            chartTouchArea.gameObject.AddComponent<ChartTouchInputBlocker>();
    }

    public void LoadStock(
        ScenarioRuntimeData scenario,
        AssetInfo asset)
    {
        CancelRequest();

        hasSelection = false;
        dataLoaded = false;
        dailyCandles.Clear();

        ClearChart();
        UpdateButtonStyles();

        if (periodText != null)
            periodText.text = "";

        if (scenario == null ||
            scenario.session == null ||
            scenario.turnData == null ||
            scenario.turnData.progress == null ||
            asset == null)
        {
            SetStatus("시나리오 또는 종목 데이터가 없습니다.");
            return;
        }

        if (!TryParseDate(
                scenario.turnData.progress.market_date,
                out marketDate))
        {
            SetStatus("시장 기준 날짜를 확인할 수 없습니다.");
            return;
        }

        sessionId = scenario.session.session_id;
        assetId = asset.asset_id;

        hasSelection =
            !string.IsNullOrEmpty(sessionId) &&
            !string.IsNullOrEmpty(assetId);

        if (!hasSelection)
        {
            SetStatus("세션 ID 또는 종목코드가 없습니다.");
            return;
        }

        Reload();
    }

    // 기존 버튼 OnClick 연결을 그대로 사용합니다.
    public void ShowOneDay() =>
        SetPeriod(ViewPeriod.OneDay);

    public void ShowOneWeek() =>
        SetPeriod(ViewPeriod.OneWeek);

    public void ShowOneMonth() =>
        SetPeriod(ViewPeriod.OneMonth);

    public void ShowThreeMonths() =>
        SetPeriod(ViewPeriod.ThreeMonths);

    public void ShowSixMonths() =>
        SetPeriod(ViewPeriod.SixMonths);

    public void ShowOneYear() =>
        SetPeriod(ViewPeriod.OneYear);

    private void SetPeriod(ViewPeriod value)
    {
        period = value;
        UpdateButtonStyles();

        if (!hasSelection || !isActiveAndEnabled)
            return;

        // 이미 받은 일봉으로 다시 집계합니다.
        // 봉 변경마다 서버에 요청하지 않습니다.
        if (dataLoaded)
        {
            RenderCurrentPeriod();
        }
        else if (activeRequest == null)
        {
            Reload();
        }

        // 요청 중이라면 응답이 도착했을 때
        // 가장 최근에 선택한 period로 그립니다.
    }

    private void UpdateButtonStyles()
    {
        SetBold(oneDayButtonText, period == ViewPeriod.OneDay);
        SetBold(oneWeekButtonText, period == ViewPeriod.OneWeek);
        SetBold(oneMonthButtonText, period == ViewPeriod.OneMonth);
        SetBold(threeMonthsButtonText, period == ViewPeriod.ThreeMonths);
        SetBold(sixMonthsButtonText, period == ViewPeriod.SixMonths);
        SetBold(oneYearButtonText, period == ViewPeriod.OneYear);
    }

    private static void SetBold(TMP_Text text, bool selected)
    {
        if (text == null)
            return;

        // 이탤릭 등 다른 스타일은 유지하고 Bold만 변경합니다.
        if (selected)
            text.fontStyle |= FontStyles.Bold;
        else
            text.fontStyle &= ~FontStyles.Bold;
    }

    private void Reload()
    {
        CancelRequest();

        if (!hasSelection || !isActiveAndEnabled)
            return;

        if (!ConfigureChart())
            return;

        dataLoaded = false;
        dailyCandles.Clear();
        ClearChart();

        if (periodText != null)
            periodText.text = "";

        StartCoroutine(
            FetchCandles(
                sessionId,
                assetId,
                marketDate,
                requestVersion
            )
        );
    }

    private bool ConfigureChart()
    {
        if (chart == null)
        {
            SetStatus("CandlestickChart를 연결하세요.");
            return false;
        }

        if (initialized)
            return true;

        chart.RemoveData();

        var serie = chart.AddSerie<Candlestick>("주가");

        serie.itemStyle.color = riseColor;
        serie.itemStyle.color0 = fallColor;
        serie.itemStyle.borderColor = riseColor;
        serie.itemStyle.borderColor0 = fallColor;
        serie.itemStyle.borderWidth = 0;
        serie.animation.enable = false;

        var xAxis = chart.EnsureChartComponent<XAxis>();
        xAxis.type = Axis.AxisType.Category;
        xAxis.boundaryGap = true;
        xAxis.show = true;

        var yAxis = chart.EnsureChartComponent<YAxis>();
        yAxis.type = Axis.AxisType.Value;
        yAxis.show = true;

        var tooltip = chart.EnsureChartComponent<Tooltip>();
        tooltip.show = true;
        tooltip.trigger = Tooltip.Trigger.Axis;

        // 표시 구간은 아래의 모바일/마우스 입력 코드에서 직접 관리합니다.
        var dataZoom = chart.EnsureChartComponent<DataZoom>();
        dataZoom.enable = false;

        chart.RefreshAllComponent();

        initialized = true;
        return true;
    }

    private IEnumerator FetchCandles(
        string requestedSession,
        string requestedAsset,
        DateTime endDate,
        int version)
    {
        SetStatus("전체 차트 데이터를 불러오는 중...");

        // start_date를 넣지 않습니다.
        // 서버가 제공하는 현재 턴까지의 전체 일봉을 가져옵니다.
        string url =
            ServerConfig.HttpBaseUrl +
            "/api/sessions/" +
            UnityWebRequest.EscapeURL(requestedSession) +
            "/chart/" +
            UnityWebRequest.EscapeURL(requestedAsset);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            activeRequest = request;

            request.timeout = 15;
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (activeRequest == request)
                activeRequest = null;

            // 다른 종목을 선택한 뒤 도착한 이전 응답은 무시합니다.
            if (version != requestVersion)
                yield break;

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetStatus("차트 불러오기 실패");

                Debug.LogError(
                    $"[Scenario Chart] {request.responseCode}\n" +
                    request.downloadHandler.text
                );

                yield break;
            }

            Response response = null;
            string parseError = null;

            try
            {
                response = JsonUtility.FromJson<Response>(
                    request.downloadHandler.text
                );
            }
            catch (Exception exception)
            {
                parseError = exception.Message;
            }

            if (parseError != null)
            {
                SetStatus("차트 응답 형식 오류");
                Debug.LogError(parseError);
                yield break;
            }

            if (response == null ||
                response.status != "ok" ||
                response.data == null ||
                response.data.candles == null)
            {
                SetStatus("차트 데이터 없음");
                yield break;
            }

            dailyCandles.Clear();

            foreach (Candle candle in response.data.candles)
            {
                if (candle == null)
                    continue;

                if (!TryParseDate(candle.date, out DateTime date))
                    continue;

                // 현재 턴 이후의 미래 데이터는 제외합니다.
                if (date <= endDate)
                    dailyCandles.Add(candle);
            }

            dailyCandles.Sort(
                (a, b) => string.CompareOrdinal(a.date, b.date)
            );

            dataLoaded = true;

            // 상세정보의 OHLC는 집계 봉이 아닌 당일 일봉입니다.
            if (dailyCandles.Count > 0)
            {
                Candle latest = dailyCandles[dailyCandles.Count - 1];

                if (stockInfoView != null &&
                    latest.date == endDate.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture))
                {
                    stockInfoView.SetOhlc(
                        latest.open,
                        latest.high,
                        latest.low
                    );
                }
            }

            RenderCurrentPeriod();
        }
    }

    private void RenderCurrentPeriod()
    {
        if (!isActiveAndEnabled || !ConfigureChart())
            return;

        string label = GetPeriodLabel();

        if (dailyCandles.Count == 0)
        {
            ClearChart();

            if (periodText != null)
                periodText.text = label;

            SetStatus("현재 턴까지 저장된 차트 데이터가 없습니다.");
            return;
        }

        List<Candle> candles = AggregateCandles();
        ResetView(candles);
        DrawVisibleCandles();

        string firstDate = dailyCandles[0].date;
        string lastDate = dailyCandles[dailyCandles.Count - 1].date;

        // 집계 단위를 바꾸더라도 실제 데이터 범위는 같습니다.
        if (periodText != null)
        {
            periodText.text =
                $"{firstDate} ~ {lastDate} · {label}";
        }

        SetStatus(
            $"{label} {candles.Count}개 · " +
            $"{firstDate} ~ {lastDate}"
        );
    }

    private List<Candle> AggregateCandles()
    {
        List<Candle> result = new List<Candle>();

        Candle current = null;
        DateTime currentBucket = DateTime.MinValue;

        foreach (Candle daily in dailyCandles)
        {
            if (!TryParseDate(daily.date, out DateTime date))
                continue;

            DateTime bucket = GetBucketStart(date);

            if (current == null || bucket != currentBucket)
            {
                currentBucket = bucket;

                current = new Candle
                {
                    // X축은 해당 집계 구간의 시작일을 표시합니다.
                    date = bucket.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture
                    ),
                    open = daily.open,
                    high = daily.high,
                    low = daily.low,
                    close = daily.close,
                    volume = daily.volume
                };

                result.Add(current);
            }
            else
            {
                // 시가: 구간 첫 거래일 시가 유지
                // 고가: 구간 최고가
                // 저가: 구간 최저가
                // 종가: 구간 마지막 거래일 종가
                // 거래량: 구간 거래량 합계
                current.high = Math.Max(current.high, daily.high);
                current.low = Math.Min(current.low, daily.low);
                current.close = daily.close;
                current.volume += daily.volume;
            }
        }

        return result;
    }

    private DateTime GetBucketStart(DateTime date)
    {
        switch (period)
        {
            case ViewPeriod.OneDay:
                return date.Date;

            case ViewPeriod.OneWeek:
                {
                    // 월요일 시작 ~ 일요일 종료
                    int daysSinceMonday =
                        ((int)date.DayOfWeek + 6) % 7;

                    return date.Date.AddDays(-daysSinceMonday);
                }

            case ViewPeriod.OneMonth:
                return new DateTime(date.Year, date.Month, 1);

            case ViewPeriod.ThreeMonths:
                {
                    // 1~3월 / 4~6월 / 7~9월 / 10~12월
                    int firstMonth =
                        ((date.Month - 1) / 3) * 3 + 1;

                    return new DateTime(date.Year, firstMonth, 1);
                }

            case ViewPeriod.SixMonths:
                {
                    // 1~6월 / 7~12월
                    int firstMonth = date.Month <= 6 ? 1 : 7;

                    return new DateTime(date.Year, firstMonth, 1);
                }

            case ViewPeriod.OneYear:
                return new DateTime(date.Year, 1, 1);

            default:
                return date.Date;
        }
    }

    private string GetPeriodLabel()
    {
        switch (period)
        {
            case ViewPeriod.OneDay:
                return "일봉";

            case ViewPeriod.OneWeek:
                return "주봉";

            case ViewPeriod.OneMonth:
                return "월봉";

            case ViewPeriod.ThreeMonths:
                return "분기봉";

            case ViewPeriod.SixMonths:
                return "반기봉";

            case ViewPeriod.OneYear:
                return "연봉";

            default:
                return "일봉";
        }
    }

    private void Draw(List<Candle> candles)
    {
        chart.ClearData();

        for (int i = 0; i < candles.Count; i++)
        {
            Candle candle = candles[i];

            chart.AddXAxisData(candle.date);

            // XCharts 캔들 데이터 순서:
            // index, open, close, low, high
            chart.AddData(
                0,
                i,
                candle.open,
                candle.close,
                candle.low,
                candle.high
            );
        }

        chart.RefreshChart();
    }

    private void ResetView(List<Candle> candles)
    {
        displayCandles.Clear();
        displayCandles.AddRange(candles);

        visibleCount = Mathf.Min(
            Mathf.Clamp(initialVisibleCount, minVisibleCount, maxVisibleCount),
            displayCandles.Count
        );

        viewEndIndex = displayCandles.Count - 1;
        ResetInputState();
    }

    private void DrawVisibleCandles()
    {
        if (displayCandles.Count == 0)
        {
            ClearChart();
            return;
        }

        visibleCount = Mathf.Clamp(
            visibleCount,
            Mathf.Min(minVisibleCount, displayCandles.Count),
            Mathf.Min(maxVisibleCount, displayCandles.Count)
        );

        viewEndIndex = Mathf.Clamp(
            viewEndIndex,
            visibleCount - 1,
            displayCandles.Count - 1
        );

        int startIndex = Mathf.Max(0, viewEndIndex - visibleCount + 1);
        int count = viewEndIndex - startIndex + 1;
        Draw(displayCandles.GetRange(startIndex, count));
    }

    private void HandleMobileTouch()
    {
        isTouchingChart = false;
        var touches = Touch.activeTouches;

        if (touches.Count == 0)
        {
            ResetMobileTouch();
            return;
        }

        if (displayCandles.Count == 0)
            return;

        Vector2 firstPosition = touches[0].screenPosition;
        if (!IsInsideChartArea(firstPosition))
        {
            ResetMobileTouch();
            return;
        }

        isTouchingChart = true;

        if (touches.Count >= 2)
        {
            Vector2 secondPosition = touches[1].screenPosition;
            if (!IsInsideChartArea(secondPosition))
            {
                ResetPinch();
                return;
            }

            HandleMobilePinch(firstPosition, secondPosition);
            return;
        }

        ResetPinch();
        HandleMobileDrag(touches[0], firstPosition);
    }

    private void HandleMobileDrag(Touch touch, Vector2 position)
    {
        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            lastTouchPosition = position;
            touchDragAccum = 0f;
            return;
        }

        float deltaX = position.x - lastTouchPosition.x;
        lastTouchPosition = position;
        touchDragAccum += deltaX * touchDragSensitivity;

        if (Mathf.Abs(touchDragAccum) < 1f)
            return;

        MoveView(Mathf.RoundToInt(touchDragAccum));
        touchDragAccum = 0f;
    }

    private void HandleMobilePinch(Vector2 first, Vector2 second)
    {
        float distance = Vector2.Distance(first, second);

        if (lastPinchDistance < 0f)
        {
            lastPinchDistance = distance;
            pinchAccum = 0f;
            return;
        }

        pinchAccum += distance - lastPinchDistance;
        lastPinchDistance = distance;

        if (Mathf.Abs(pinchAccum) < pinchThreshold)
            return;

        ZoomView(pinchAccum > 0f ? -pinchZoomStep : pinchZoomStep);
        pinchAccum = 0f;
    }

    private void HandleMouseDrag()
    {
        if (Mouse.current == null)
            return;

        Vector2 position = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isMouseDragging = IsInsideChartArea(position);
            lastMousePosition = position;
            mouseDragAccum = 0f;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            isMouseDragging = false;

        if (!isMouseDragging || displayCandles.Count == 0)
            return;

        mouseDragAccum +=
            (position.x - lastMousePosition.x) * dragSensitivity;
        lastMousePosition = position;

        if (Mathf.Abs(mouseDragAccum) < 1f)
            return;

        MoveView(Mathf.RoundToInt(mouseDragAccum));
        mouseDragAccum = 0f;
    }

    private void HandleMouseWheel()
    {
        if (Mouse.current == null || displayCandles.Count == 0)
            return;

        if (!IsInsideChartArea(Mouse.current.position.ReadValue()))
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f)
            return;

        ZoomView(scroll > 0f ? -wheelZoomStep : wheelZoomStep);
    }

    private void MoveView(int moveCount)
    {
        int previous = viewEndIndex;
        viewEndIndex -= moveCount;
        viewEndIndex = Mathf.Clamp(
            viewEndIndex,
            visibleCount - 1,
            displayCandles.Count - 1
        );

        if (viewEndIndex != previous)
            DrawVisibleCandles();
    }

    private void ZoomView(int change)
    {
        int previous = visibleCount;
        int maximum = Mathf.Min(maxVisibleCount, displayCandles.Count);
        int minimum = Mathf.Min(minVisibleCount, maximum);
        visibleCount = Mathf.Clamp(visibleCount + change, minimum, maximum);

        viewEndIndex = Mathf.Clamp(
            viewEndIndex,
            visibleCount - 1,
            displayCandles.Count - 1
        );

        if (visibleCount != previous)
            DrawVisibleCandles();
    }

    private bool IsInsideChartArea(Vector2 screenPosition)
    {
        if (chartTouchArea == null)
            return false;

        Canvas canvas = chartTouchArea.GetComponentInParent<Canvas>();
        Camera eventCamera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(
            chartTouchArea,
            screenPosition,
            eventCamera
        );
    }

    private void ResetMobileTouch()
    {
        isTouchingChart = false;
        touchDragAccum = 0f;
        ResetPinch();
    }

    private void ResetPinch()
    {
        lastPinchDistance = -1f;
        pinchAccum = 0f;
    }

    private void ResetInputState()
    {
        ResetMobileTouch();
        isMouseDragging = false;
        mouseDragAccum = 0f;
    }

    private static bool TryParseDate(
        string value,
        out DateTime date)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date
        );
    }

    private void CancelRequest()
    {
        requestVersion++;

        if (activeRequest != null)
        {
            activeRequest.Abort();
            activeRequest = null;
        }

        StopAllCoroutines();
    }

    private void ClearChart()
    {
        if (chart == null)
            return;

        chart.ClearData();
        chart.RefreshChart();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        ResetInputState();
        CancelRequest();
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        UpdateButtonStyles();

        if (!hasSelection)
            return;

        if (dataLoaded)
            RenderCurrentPeriod();
        else
            Reload();
    }

    [Serializable]
    private class Response
    {
        public string status;
        public Payload data;
        public string message;
    }

    [Serializable]
    private class Payload
    {
        public string end_date;
        public bool data_available;
        public Candle[] candles;
    }

    [Serializable]
    private class Candle
    {
        public string date;
        public long open;
        public long high;
        public long low;
        public long close;
        public long volume;
    }
}

