using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class TradeMyData : MonoBehaviour
{
    [Header("FastAPI 서버")]

    [Header("User UI")]
    [SerializeField]
    private TMP_Text usernameText;

    [SerializeField]
    private TMP_Text userIdText;

    [Header("Portfolio UI")]
    [SerializeField]
    private TMP_Text cashText;

    [SerializeField]
    private TMP_Text holdingsText;

    [Header("선택 종목 보유 정보")]
    [Tooltip("현재가 x 보유수량")]
    [SerializeField] private TMP_Text selectedTotalValueText;
    [Tooltip("평균 매수가 x 보유수량")]
    [SerializeField] private TMP_Text selectedPrincipalText;
    [SerializeField] private TMP_Text selectedQuantityText;
    [SerializeField] private TMP_Text selectedReturnRateText;
    [SerializeField] private Color profitColor = new Color(0.965f, 0.42f, 0.14f, 1f);
    [SerializeField] private Color lossColor = new Color(0.29f, 0.56f, 0.89f, 1f);
    [SerializeField] private Color neutralColor = new Color(0.44f, 0.44f, 0.44f, 1f);

    [Header("Status UI")]
    [SerializeField]
    private TMP_Text messageText;

    [Header("Scene")]
    [SerializeField]
    private string loginSceneName = "Login";

    public string UserId { get; private set; }
    public string Username { get; private set; }

    public long Cash { get; private set; }
    public long ReservedCash { get; private set; }
    public long AvailableCash { get; private set; }
    private StockCandlestickLoader observedLoader;

    public List<TradeHolding> Holdings
    {
        get;
        private set;
    } = new List<TradeHolding>();

    public bool IsLoading { get; private set; }

    private void Start()
    {
        observedLoader = FindFirstObjectByType<StockCandlestickLoader>(FindObjectsInactive.Include);
        if (observedLoader != null) observedLoader.TradeCompleted += ReloadPortfolio;
        InitializeTradeData();
    }
    private void Update()
    {
        UpdateSelectedStockUI();
    }
    private void OnDestroy()
    {
        if (observedLoader != null) observedLoader.TradeCompleted -= ReloadPortfolio;
    }

    public void InitializeTradeData()
    {
        if (AuthManager.Instance == null)
        {
            HandleAuthenticationFailure(
                "AuthManager가 없습니다."
            );

            return;
        }

        if (!AuthManager.Instance.IsLoggedIn())
        {
            HandleAuthenticationFailure(
                "로그인이 필요합니다."
            );

            return;
        }

        UserId = AuthManager.Instance.UserId;
        Username = AuthManager.Instance.Username;

        UpdateUserUI();

        StartCoroutine(
            LoadPortfolioCoroutine()
        );
    }

    private void UpdateUserUI()
    {
        if (usernameText != null)
        {
            usernameText.text =
                string.IsNullOrWhiteSpace(Username)
                    ? "-"
                    : Username;
        }

        if (userIdText != null)
        {
            userIdText.text =
                string.IsNullOrWhiteSpace(UserId)
                    ? "-"
                    : UserId;
        }

        Debug.Log(
            "[TradeMyData] Username: " +
            Username
        );

        Debug.Log(
            "[TradeMyData] UserId: " +
            UserId
        );
    }

    public void ReloadPortfolio()
    {
        if (IsLoading)
            return;

        StartCoroutine(
            LoadPortfolioCoroutine()
        );
    }

    private IEnumerator LoadPortfolioCoroutine()
    {
        if (IsLoading)
            yield break;
        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn())
        {
            HandleAuthenticationFailure("로그인이 필요합니다.");
            yield break;
        }

        IsLoading = true;
        SetMessage(
            "플레이어 정보를 불러오는 중..."
        );

        string url =
            ServerConfig.HttpBaseUrl +
            "/portfolio";

        using UnityWebRequest request =
            UnityWebRequest.Get(url);

        AuthManager.Instance
            .AddAuthorizationHeader(request);

        request.timeout = 15;

        yield return request.SendWebRequest();

        IsLoading = false;

        if (request.responseCode == 401)
        {
            AuthManager.Instance.Logout();

            HandleAuthenticationFailure(
                "로그인이 만료되었습니다."
            );

            yield break;
        }

        if (
            request.responseCode < 200 ||
            request.responseCode >= 300
        )
        {
            string errorMessage =
                ParseServerError(
                    request.downloadHandler?.text,
                    request.error
                );

            SetMessage(
                "정보 조회 실패: " +
                errorMessage
            );

            Debug.LogError(
                "[TradeMyData] Portfolio 실패: " +
                errorMessage
            );

            yield break;
        }

        TradePortfolioResponse response;

        try
        {
            response =
                JsonConvert.DeserializeObject<
                    TradePortfolioResponse
                >(
                    request.downloadHandler.text
                );
        }
        catch (Exception error)
        {
            SetMessage(
                "포트폴리오 응답을 읽지 못했습니다."
            );

            Debug.LogError(
                "[TradeMyData] JSON 오류: " +
                error.Message
            );

            yield break;
        }

        if (response == null)
        {
            SetMessage(
                "포트폴리오 데이터가 없습니다."
            );

            yield break;
        }

        Cash = response.cash;
        ReservedCash = response.reservedCash;
        AvailableCash = response.availableCash;

        Holdings =
            response.holdings ??
            new List<TradeHolding>();

        UpdatePortfolioUI();
        UpdateSelectedStockUI();

        SetMessage(
            "플레이어 정보 조회 완료"
        );

        Debug.Log(
            "[TradeMyData] 포트폴리오 로드 완료"
        );
    }

    private void UpdatePortfolioUI()
    {
        if (cashText != null)
        {
            cashText.text =
                $"₩{Cash:N0}";
        }

        if (holdingsText == null)
            return;

        if (
            Holdings == null ||
            Holdings.Count == 0
        )
        {
            holdingsText.text =
                "보유 종목 없음";

            return;
        }

        List<string> lines =
            new List<string>();

        foreach (TradeHolding holding in Holdings)
        {
            string stockName =
                string.IsNullOrWhiteSpace(
                    holding.name
                )
                    ? holding.symbol
                    : holding.name;

            lines.Add(
                $"{stockName} ({holding.symbol})\n" +
                $"{holding.quantity:N0}주 / " +
                $"평균 {holding.avg_price:N0}원"
            );
        }

        holdingsText.text =
            string.Join("\n\n", lines);
    }

    private void UpdateSelectedStockUI()
    {
        if (observedLoader == null)
            observedLoader = FindFirstObjectByType<StockCandlestickLoader>(FindObjectsInactive.Include);

        string selectedSymbol = observedLoader != null ? observedLoader.symbol : "";
        TradeHolding selectedHolding = null;

        if (!string.IsNullOrWhiteSpace(selectedSymbol) && Holdings != null)
        {
            selectedHolding = Holdings.Find(
                holding => holding != null && holding.symbol == selectedSymbol
            );
        }

        int quantity = selectedHolding != null ? selectedHolding.quantity : 0;
        double principal = selectedHolding != null
            ? selectedHolding.avg_price * quantity
            : 0d;
        int currentPrice = observedLoader != null ? observedLoader.CurrentPrice : 0;
        double totalValue = currentPrice > 0 ? (double)currentPrice * quantity : 0d;

        if (selectedQuantityText != null)
            selectedQuantityText.text = $"{quantity:N0}주";

        if (selectedPrincipalText != null)
            selectedPrincipalText.text = $"₩{principal:N0}";

        if (selectedTotalValueText != null)
            selectedTotalValueText.text = currentPrice > 0 || quantity == 0
                ? $"₩{totalValue:N0}"
                : "-";

        if (selectedReturnRateText == null)
            return;

        if (quantity <= 0)
        {
            selectedReturnRateText.text = "0.00%";
            selectedReturnRateText.color = neutralColor;
            return;
        }

        if (currentPrice <= 0 || principal <= 0d)
        {
            selectedReturnRateText.text = "-";
            selectedReturnRateText.color = neutralColor;
            return;
        }

        double returnRate = (totalValue - principal) / principal * 100d;
        selectedReturnRateText.text = returnRate > 0d
            ? $"+{returnRate:F2}%"
            : $"{returnRate:F2}%";
        selectedReturnRateText.color = returnRate > 0d
            ? profitColor
            : returnRate < 0d ? lossColor : neutralColor;
    }

    private string ParseServerError(
        string responseBody,
        string fallback)
    {
        if (
            !string.IsNullOrWhiteSpace(
                responseBody
            )
        )
        {
            try
            {
                TradeApiError error =
                    JsonConvert.DeserializeObject<
                        TradeApiError
                    >(responseBody);

                if (
                    error != null &&
                    !string.IsNullOrWhiteSpace(
                        error.detail
                    )
                )
                {
                    return error.detail;
                }
            }
            catch
            {
                return responseBody;
            }
        }

        return string.IsNullOrWhiteSpace(
            fallback
        )
            ? "서버에 연결하지 못했습니다."
            : fallback;
    }

    private void HandleAuthenticationFailure(
        string message)
    {
        Debug.LogWarning(
            "[TradeMyData] " + message
        );

        SetMessage(message);

        if (
            !string.IsNullOrWhiteSpace(
                loginSceneName
            )
        )
        {
            SceneManager.LoadScene(
                loginSceneName
            );
        }
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text =
                message ?? "";
        }
    }
}

[Serializable]
public class TradePortfolioResponse
{
    public long cash;
    public long reservedCash;
    public long availableCash;

    public List<TradeHolding> holdings;
}

[Serializable]
public class TradeHolding
{
    public string symbol;
    public string name;
    public int quantity;
    public double avg_price;
    public int reservedQuantity;
    public int availableQuantity;
}

[Serializable]
public class TradeApiError
{
    public string detail;
}


