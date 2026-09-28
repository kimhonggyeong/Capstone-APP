using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class MyPageController : MonoBehaviour
{
    [Header("서버")]
    [SerializeField] private int requestTimeoutSeconds = 30;

    [Header("사용자")]
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private string usernameSuffix = "님";

    [Header("모의투자 요약")]
    [SerializeField] private TMP_Text totalAssetsText;
    [SerializeField] private TMP_Text totalReturnRateText;
    [SerializeField] private TMP_Text totalProfitText;
    [SerializeField] private TMP_Text holdingCountText;
    [SerializeField] private TMP_Text holdingCountGuideText;

    [Header("보유자산 목록")]
    [SerializeField] private Transform holdingsContent;
    [SerializeField] private MyPageHoldingRowUI holdingRowPrefab;
    [SerializeField] private GameObject holdingsEmptyObject;

    [Header("최근 진행 시나리오")]
    [SerializeField] private GameObject scenarioContentObject;
    [SerializeField] private GameObject scenarioEmptyObject;
    [SerializeField] private TMP_Text scenarioTitleText;
    [SerializeField] private TMP_Text scenarioTurnText;
    [SerializeField] private Slider scenarioProgressSlider;
    [SerializeField] private TMP_Text scenarioProgressText;
    [SerializeField] private TMP_Text scenarioUpdatedAtText;

    [Header("상태")]
    [SerializeField] private Button refreshButton;
    [Tooltip("마이페이지 최초 진입 시 모든 정보를 불러오는 동안 표시합니다.")]
    [SerializeField] private GameObject loadingObject;
    [Tooltip("보유자산 재검색 버튼을 눌렀을 때만 표시합니다.")]
    [SerializeField] private GameObject holdingsLoadingObject;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Color profitColor = new Color(0.965f, 0.42f, 0.14f, 1f);
    [SerializeField] private Color lossColor = new Color(0.29f, 0.56f, 0.89f, 1f);
    [SerializeField] private Color neutralColor = new Color(0.44f, 0.44f, 0.44f, 1f);

    public MyPageScenarioSession LatestActiveScenario { get; private set; }
    public bool IsLoading { get; private set; }

    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    private void OnEnable()
    {
        ApplyUsername();

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshHoldings);
            refreshButton.onClick.AddListener(RefreshHoldings);
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(RefreshHoldings);
    }

    /// <summary>
    /// 기존 버튼 연결과의 호환을 위해 보유자산 재검색으로 처리합니다.
    /// </summary>
    public void Refresh()
    {
        RefreshHoldings();
    }

    public void RefreshAll()
    {
        if (!IsLoading)
            StartCoroutine(RefreshAllRoutine());
    }

    public void RefreshHoldings()
    {
        if (!IsLoading)
            StartCoroutine(RefreshHoldingsRoutine());
    }

    private IEnumerator RefreshAllRoutine()
    {
        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn())
        {
            SetMessage("로그인이 필요합니다.");
            yield break;
        }

        IsLoading = true;
        if (refreshButton != null) refreshButton.interactable = false;
        if (holdingsLoadingObject != null) holdingsLoadingObject.SetActive(false);
        if (loadingObject != null) loadingObject.SetActive(true);
        SetMessage("마이페이지 정보를 불러오는 중...");
        ClearHoldingRows();

        MyPagePortfolioResponse portfolio = null;
        string portfolioError = null;
        yield return LoadPortfolio(value => portfolio = value, error => portfolioError = error);

        if (portfolio != null)
            yield return BuildPortfolio(portfolio);
        else
            Debug.LogError("[MyPage] Portfolio: " + portfolioError);

        MyPageScenarioSession[] sessions = null;
        string scenarioError = null;
        yield return LoadScenarioSessions(value => sessions = value, error => scenarioError = error);
        ApplyLatestScenario(sessions);

        if (!string.IsNullOrWhiteSpace(portfolioError) && !string.IsNullOrWhiteSpace(scenarioError))
            SetMessage("마이페이지 정보를 불러오지 못했습니다.");
        else if (!string.IsNullOrWhiteSpace(portfolioError))
            SetMessage("모의투자 정보를 불러오지 못했습니다.");
        else if (!string.IsNullOrWhiteSpace(scenarioError))
            SetMessage("시나리오 진행 정보를 불러오지 못했습니다.");
        else
            SetMessage("");

        if (loadingObject != null) loadingObject.SetActive(false);
        if (refreshButton != null) refreshButton.interactable = true;
        IsLoading = false;
    }

    private IEnumerator RefreshHoldingsRoutine()
    {
        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn())
        {
            SetMessage("로그인이 필요합니다.");
            yield break;
        }

        IsLoading = true;
        if (refreshButton != null) refreshButton.interactable = false;
        if (loadingObject != null) loadingObject.SetActive(false);
        if (holdingsLoadingObject != null) holdingsLoadingObject.SetActive(true);
        SetMessage("보유자산을 다시 불러오는 중...");
        ClearHoldingRows();

        MyPagePortfolioResponse portfolio = null;
        string portfolioError = null;
        yield return LoadPortfolio(
            value => portfolio = value,
            error => portfolioError = error
        );

        if (portfolio != null)
            yield return BuildPortfolio(portfolio);
        else
            Debug.LogError("[MyPage] Portfolio refresh: " + portfolioError);

        SetMessage(
            string.IsNullOrWhiteSpace(portfolioError)
                ? ""
                : "보유자산 정보를 불러오지 못했습니다."
        );

        if (holdingsLoadingObject != null) holdingsLoadingObject.SetActive(false);
        if (refreshButton != null) refreshButton.interactable = true;
        IsLoading = false;
    }

    private IEnumerator LoadPortfolio(Action<MyPagePortfolioResponse> success, Action<string> failure)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(ServerConfig.HttpBaseUrl + "/portfolio"))
        {
            request.timeout = requestTimeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");
            AuthManager.Instance.AddAuthorizationHeader(request);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                failure?.Invoke(ReadError(request));
                yield break;
            }

            try
            {
                MyPagePortfolioResponse data = JsonConvert.DeserializeObject<MyPagePortfolioResponse>(request.downloadHandler.text);
                if (data == null) failure?.Invoke("포트폴리오 데이터가 없습니다.");
                else success?.Invoke(data);
            }
            catch (Exception e) { failure?.Invoke("포트폴리오 응답 오류: " + e.Message); }
        }
    }

    private IEnumerator BuildPortfolio(MyPagePortfolioResponse portfolio)
    {
        List<Holding> holdings = (portfolio.holdings ?? new List<Holding>())
            .Where(item => item != null && item.quantity > 0)
            .ToList();
        long marketValue = 0L;
        double holdingPrincipal = 0d;
        int profitableCount = 0;

        foreach (Holding holding in holdings)
        {
            StockInfoResponse quote = null;
            yield return LoadQuote(holding.symbol, value => quote = value);
            long valuationPrice = quote != null && quote.price > 0
                ? quote.price
                : (long)Math.Round(holding.avg_price);
            marketValue += valuationPrice * holding.quantity;
            holdingPrincipal += holding.avg_price * holding.quantity;
            if (quote != null && quote.price > holding.avg_price) profitableCount++;

            if (holdingsContent != null && holdingRowPrefab != null)
            {
                MyPageHoldingRowUI row = Instantiate(holdingRowPrefab, holdingsContent);
                row.Bind(holding, quote);
                spawnedRows.Add(row.gameObject);
            }
        }

        long totalAssets = portfolio.cash + marketValue;
        double basis = portfolio.totalDeposits > 0
            ? portfolio.totalDeposits
            : portfolio.initialCash > 0 ? portfolio.initialCash : portfolio.cash + holdingPrincipal;
        double profit = basis > 0d ? totalAssets - basis : 0d;
        double returnRate = basis > 0d ? profit / basis * 100d : 0d;

        SetText(totalAssetsText, $"₩{totalAssets:N0}");
        SetText(totalProfitText, profit > 0d ? $"+₩{profit:N0}" : profit < 0d ? $"-₩{Math.Abs(profit):N0}" : "₩0");
        SetText(holdingCountText, $"{holdings.Count:N0}개");
        SetText(holdingCountGuideText, holdings.Count > 0 ? $"수익 중 {profitableCount}개" : "보유 종목 없음");

        if (totalReturnRateText != null)
        {
            totalReturnRateText.text = returnRate > 0d ? $"+{returnRate:F2}%" : $"{returnRate:F2}%";
            totalReturnRateText.color = returnRate > 0d ? profitColor : returnRate < 0d ? lossColor : neutralColor;
        }

        if (holdingsEmptyObject != null) holdingsEmptyObject.SetActive(holdings.Count == 0);
    }

    private IEnumerator LoadQuote(string symbol, Action<StockInfoResponse> success)
    {
        string url = ServerConfig.HttpBaseUrl + "/stock-info?symbol=" + UnityWebRequest.EscapeURL(symbol);
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = requestTimeoutSeconds;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[MyPage] Quote " + symbol + ": " + ReadError(request));
                yield break;
            }
            try { success?.Invoke(JsonConvert.DeserializeObject<StockInfoResponse>(request.downloadHandler.text)); }
            catch (Exception e) { Debug.LogWarning("[MyPage] Quote JSON " + symbol + ": " + e.Message); }
        }
    }

    private IEnumerator LoadScenarioSessions(Action<MyPageScenarioSession[]> success, Action<string> failure)
    {
        string userId = AuthManager.Instance.UserId;
        string url = ServerConfig.HttpBaseUrl + "/api/users/" + UnityWebRequest.EscapeURL(userId) + "/sessions";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = requestTimeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");
            AuthManager.Instance.AddAuthorizationHeader(request);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                failure?.Invoke(ReadError(request));
                yield break;
            }
            try
            {
                MyPageScenarioSessionsResponse response = JsonConvert.DeserializeObject<MyPageScenarioSessionsResponse>(request.downloadHandler.text);
                if (response == null || response.status != "ok") failure?.Invoke(response?.message ?? "시나리오 응답이 올바르지 않습니다.");
                else success?.Invoke(response.data ?? Array.Empty<MyPageScenarioSession>());
            }
            catch (Exception e) { failure?.Invoke("시나리오 응답 오류: " + e.Message); }
        }
    }

    private void ApplyLatestScenario(MyPageScenarioSession[] sessions)
    {
        LatestActiveScenario = sessions?
            .Where(item => item != null && item.status == "ACTIVE")
            .OrderByDescending(item => ParseDate(item.updated_at))
            .FirstOrDefault();

        bool exists = LatestActiveScenario != null;
        if (scenarioContentObject != null) scenarioContentObject.SetActive(exists);
        if (scenarioEmptyObject != null) scenarioEmptyObject.SetActive(!exists);
        if (!exists) return;

        MyPageScenarioSession item = LatestActiveScenario;
        float progress = item.progress_pct > 0f
            ? item.progress_pct
            : item.total_turns > 0 ? item.completed_turns * 100f / item.total_turns : 0f;

        SetText(scenarioTitleText, item.title);
        SetText(scenarioTurnText, $"TURN {item.current_turn} / {item.total_turns}");
        SetText(scenarioProgressText, $"{progress:0}%");
        SetText(scenarioUpdatedAtText, "최근 진행 " + FormatDate(item.updated_at));

        if (scenarioProgressSlider != null)
        {
            scenarioProgressSlider.minValue = 0f;
            scenarioProgressSlider.maxValue = 100f;
            scenarioProgressSlider.SetValueWithoutNotify(Mathf.Clamp(progress, 0f, 100f));
            scenarioProgressSlider.interactable = false;
        }
    }

    private void ClearHoldingRows()
    {
        foreach (GameObject row in spawnedRows)
            if (row != null) Destroy(row);
        spawnedRows.Clear();
    }

    private void ApplyUsername()
    {
        string username = AuthManager.Instance != null
            ? AuthManager.Instance.Username
            : "";
        SetText(
            usernameText,
            string.IsNullOrWhiteSpace(username)
                ? "-"
                : username + (usernameSuffix ?? "")
        );
    }

    private void SetMessage(string value) { SetText(messageText, value); }
    private static void SetText(TMP_Text target, string value) { if (target != null) target.text = value ?? ""; }
    private static DateTimeOffset ParseDate(string value) => DateTimeOffset.TryParse(value, out var date) ? date : DateTimeOffset.MinValue;
    private static string FormatDate(string value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
        ? date.ToLocalTime().ToString("yyyy.MM.dd") : string.IsNullOrWhiteSpace(value) ? "-" : value;
    private static string ReadError(UnityWebRequest request) => !string.IsNullOrWhiteSpace(request.downloadHandler?.text)
        ? request.downloadHandler.text : request.error ?? "서버 연결 실패";
}

[Serializable]
public class MyPagePortfolioResponse
{
    public long cash;
    public long reservedCash;
    public long availableCash;
    public long initialCash;
    public long totalDeposits;
    public List<Holding> holdings;
}

[Serializable]
public class MyPageScenarioSessionsResponse
{
    public string status;
    public MyPageScenarioSession[] data;
    public string message;
}

[Serializable]
public class MyPageScenarioSession
{
    public string session_id;
    public string scenario_id;
    public string title;
    public string status;
    public int current_turn;
    public int completed_turns;
    public int total_turns;
    public float progress_pct;
    public string started_at;
    public string updated_at;
}



