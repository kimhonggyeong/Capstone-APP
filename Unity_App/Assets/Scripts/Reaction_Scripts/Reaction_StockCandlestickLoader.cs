using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using XCharts.Runtime;

public class Reaction_StockCandlestickLoader : MonoBehaviour
{

    public string symbol = "005930";
    public string period = "all";
    public string interval = "1d";

    public CandlestickChart chart;

    [Header("View")]
    public int visibleCount = 60;
    public int minVisibleCount = 10;
    public int maxVisibleCount = 3000;

    [Header("Drag / Zoom")]
    public float dragSensitivity = 0.05f;
    public int wheelZoomStep = 5;

    [Header("Moving Average")]
    public bool showMA5 = true;
    public bool showMA20 = true;
    public bool showMA60 = true;
    public bool showMA120 = true;

    private List<CandlePoint> allPoints = new List<CandlePoint>();

    private int viewEndIndex = -1;
    private bool followLatest = true;

    private Vector2 lastMousePos;
    private bool isDragging = false;
    private float dragAccum = 0f;

    private Coroutine loadRoutine;

    private void Start()
    {
        InitChart();
    }

    private void Update()
    {
        HandleMouseDrag();
        HandleMouseWheel();
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
            serie.lineStyle.color = color;
    }

    private void SetCandleColor(int index)
    {
        var serie = chart.GetSerie(index);

        if (serie != null)
        {
            serie.itemStyle.color = new Color(1f, 0.5f, 0.5f);
            serie.itemStyle.color0 = new Color(0.5f, 0.7f, 1f);

            serie.itemStyle.borderColor = Color.red;
            serie.itemStyle.borderColor0 = Color.blue;
        }
    }

    public void LoadStock(string newSymbol)
    {
        if (string.IsNullOrEmpty(newSymbol))
            return;

        symbol = newSymbol;

        if (loadRoutine != null)
            StopCoroutine(loadRoutine);

        allPoints.Clear();
        followLatest = true;
        viewEndIndex = -1;

        loadRoutine = StartCoroutine(LoadCandlesOnce());
    }

    public void SetDailyChart()
    {
        period = "all";
        interval = "1d";
        visibleCount = 60;
        maxVisibleCount = 3000;
        ReloadFullChart();
    }

    public void SetWeeklyChart()
    {
        period = "all";
        interval = "1wk";
        visibleCount = 60;
        maxVisibleCount = 1000;
        ReloadFullChart();
    }

    public void SetMonthlyChart()
    {
        period = "all";
        interval = "1mo";
        visibleCount = 60;
        maxVisibleCount = 500;
        ReloadFullChart();
    }

    public void SetYearlyChart()
    {
        period = "all";
        interval = "1y";
        visibleCount = 30;
        maxVisibleCount = 100;
        ReloadFullChart();
    }

    private void ReloadFullChart()
    {
        if (loadRoutine != null)
            StopCoroutine(loadRoutine);

        followLatest = true;
        viewEndIndex = -1;
        allPoints.Clear();

        loadRoutine = StartCoroutine(LoadCandlesOnce());
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

    private IEnumerator LoadCandlesOnce()
    {
        string url = $"{ServerConfig.HttpBaseUrl}/candles?symbol={symbol}&period={period}&interval={interval}";

        Debug.Log("반응 시뮬 차트 요청: " + url);

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("차트 데이터 요청 실패: " + req.downloadHandler.text);
                yield break;
            }

            CandleResponse response =
                JsonConvert.DeserializeObject<CandleResponse>(req.downloadHandler.text);

            if (response == null || response.points == null || response.points.Count == 0)
            {
                Debug.LogError("차트 데이터 없음");
                yield break;
            }

            allPoints = response.points;
            viewEndIndex = allPoints.Count - 1;
            followLatest = true;

            DrawVisibleCandles();
        }
    }

    private void DrawVisibleCandles()
    {
        if (chart == null)
        {
            Debug.LogError("Chart 없음");
            return;
        }

        if (allPoints == null || allPoints.Count == 0)
            return;

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

            if (p.open <= 0 || p.high <= 0 || p.low <= 0 || p.close <= 0)
                continue;

            chart.AddXAxisData(p.date);

            float ma5 = CalculateMA(i, 5);
            float ma20 = CalculateMA(i, 20);
            float ma60 = CalculateMA(i, 60);
            float ma120 = CalculateMA(i, 120);

            if (showMA5 && !float.IsNaN(ma5))
            {
                chart.AddData(0, chartIndex, ma5);
                minPrice = Mathf.Min(minPrice, ma5);
                maxPrice = Mathf.Max(maxPrice, ma5);
            }

            if (showMA20 && !float.IsNaN(ma20))
            {
                chart.AddData(1, chartIndex, ma20);
                minPrice = Mathf.Min(minPrice, ma20);
                maxPrice = Mathf.Max(maxPrice, ma20);
            }

            if (showMA60 && !float.IsNaN(ma60))
            {
                chart.AddData(2, chartIndex, ma60);
                minPrice = Mathf.Min(minPrice, ma60);
                maxPrice = Mathf.Max(maxPrice, ma60);
            }

            if (showMA120 && !float.IsNaN(ma120))
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
            return;

        float padding = (maxPrice - minPrice) * 0.1f;

        if (padding <= 0f || float.IsNaN(padding) || float.IsInfinity(padding))
            padding = 1f;

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
            return float.NaN;

        float sum = 0f;

        for (int i = index - period + 1; i <= index; i++)
            sum += allPoints[i].close;

        return sum / period;
    }

    private void HandleMouseDrag()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isDragging = true;
            lastMousePos = Mouse.current.position.ReadValue();
            dragAccum = 0f;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            isDragging = false;

        if (!isDragging || allPoints == null || allPoints.Count == 0)
            return;

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
        }
    }

    private void HandleMouseWheel()
    {
        if (Mouse.current == null) return;

        if (allPoints == null || allPoints.Count == 0)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        int oldVisibleCount = visibleCount;

        if (scroll > 0)
            visibleCount -= wheelZoomStep;
        else
            visibleCount += wheelZoomStep;

        visibleCount = Mathf.Clamp(visibleCount, minVisibleCount, maxVisibleCount);
        visibleCount = Mathf.Min(visibleCount, allPoints.Count);

        if (followLatest)
            viewEndIndex = allPoints.Count - 1;
        else
            viewEndIndex = Mathf.Clamp(viewEndIndex, visibleCount - 1, allPoints.Count - 1);

        if (visibleCount != oldVisibleCount)
            DrawVisibleCandles();
    }
}

