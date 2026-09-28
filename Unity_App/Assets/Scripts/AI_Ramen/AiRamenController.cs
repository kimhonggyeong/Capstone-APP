using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class AiRamenController : MonoBehaviour
{
    [Header("연결")]
    public StockCandlestickLoader stockLoader;

    [Header("공통 상태")]
    public GameObject loadingPanel;
    public TMP_Text loadingText;
    public TMP_Text errorText;

    [Header("판단근거")]
    public TMP_Text judgmentText;
    public Image judgmentBackground;
    public Color buyJudgmentColor = new Color32(255, 234, 231, 255);
    public Color holdJudgmentColor = new Color32(255, 242, 232, 255);
    public Color sellJudgmentColor = new Color32(232, 239, 255, 255);
    public TMP_Text confidenceText;
    public TMP_Text summaryText;
    public Transform factorContent;
    public AiFactorRowUI factorRowPrefab;
    [Tooltip("판단근거 화면 전체 또는 Layout Group이 붙은 최상위 RectTransform")]
    public RectTransform reasoningLayoutRoot;

    [Header("히스토리")]
    public Transform historyContent;
    public AiHistoryRowUI historyRowPrefab;

    [Header("비교")]
    public TMP_Text userJudgmentText;
    public TMP_Text aiJudgmentText;
    public TMP_Text compareConfidenceText;
    public TMP_Text comparisonText;
    public TMP_Text directFactorsText;
    public TMP_Text indirectFactorsText;
    public AiCompareView compareBuyView;
    public AiCompareView compareHoldView;
    public AiCompareView compareSellView;

    private readonly List<GameObject> factorRows = new List<GameObject>();
    private readonly List<GameObject> historyRows = new List<GameObject>();
    private AiJudgmentResponse latest;
    private string analyzedSymbol = "";
    private bool isBusy;
    private Coroutine reasoningRefreshRoutine;

    public void AnalyzeSelectedStock()
    {
        if (isBusy) return;
        if (!EnsureLoggedIn()) return;
        string symbol = SelectedSymbol();
        if (string.IsNullOrWhiteSpace(symbol))
        {
            SetError("선택된 종목이 없습니다.");
            return;
        }
        StartCoroutine(Analyze(symbol));
    }

    public void ShowReasoning()
    {
        string selected = SelectedSymbol();
        if (latest == null || analyzedSymbol != selected) AnalyzeSelectedStock();
        else ApplyJudgment(latest);
    }

    public void ShowHistory()
    {
        if (!EnsureLoggedIn()) return;
        string symbol = SelectedSymbol();
        if (string.IsNullOrWhiteSpace(symbol))
        {
            SetError("선택된 종목이 없습니다.");
            return;
        }
        StartCoroutine(LoadHistory(symbol));
    }

    public void CompareBuy() { Compare("매수", compareBuyView); }
    public void CompareHold() { Compare("관망", compareHoldView); }
    public void CompareSell() { Compare("매도", compareSellView); }

    private void Compare(string userJudgment, AiCompareView targetView)
    {
        if (!EnsureLoggedIn()) return;
        string symbol = SelectedSymbol();
        if (string.IsNullOrWhiteSpace(symbol))
        {
            SetError("선택된 종목이 없습니다.");
            return;
        }
        StartCoroutine(LoadComparison(symbol, userJudgment, targetView));
    }

    private IEnumerator Analyze(string symbol)
    {
        SetBusy(true, "AI가 종목 데이터를 분석하고 있어요...");
        var body = new AiAnalyzeRequest
        {
            symbol = symbol,
            stock_name = stockLoader != null && stockLoader.stockNameText != null
                ? stockLoader.stockNameText.text.Trim()
                : ""
        };
        yield return SendJson(UnityWebRequest.kHttpVerbPOST, "/judgment/analyze", body,
            json =>
            {
                latest = JsonConvert.DeserializeObject<AiJudgmentResponse>(json);
                analyzedSymbol = symbol;
                ApplyJudgment(latest);
            });
        SetBusy(false, "");
    }

    private IEnumerator LoadHistory(string symbol)
    {
        SetBusy(true, "판단 히스토리를 불러오고 있어요...");
        yield return SendJson(UnityWebRequest.kHttpVerbGET,
            "/judgment/" + UnityWebRequest.EscapeURL(symbol) + "/history?limit=20", null,
            json => ApplyHistory(JsonConvert.DeserializeObject<List<AiHistoryEntry>>(json)));
        SetBusy(false, "");
    }

    private IEnumerator LoadComparison(string symbol, string userJudgment, AiCompareView targetView)
    {
        SetBusy(true, "내 판단과 AI 판단을 비교하고 있어요...");
        var body = new AiCompareRequest { symbol = symbol, user_judge = userJudgment };
        yield return SendJson(UnityWebRequest.kHttpVerbPOST, "/judgment/compare", body,
            json => ApplyComparison(JsonConvert.DeserializeObject<AiCompareResponse>(json), targetView));
        SetBusy(false, "");
    }

    private IEnumerator SendJson(string method, string path, object body, Action<string> onSuccess)
    {
        SetError("");
        using (var request = new UnityWebRequest(ServerConfig.HttpBaseUrl + path, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 120;
            if (body != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }
            if (AuthManager.Instance != null)
                AuthManager.Instance.AddAuthorizationHeader(request);

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                SetError(ParseError(request));
                yield break;
            }
            try { onSuccess(request.downloadHandler.text); }
            catch (Exception error)
            {
                Debug.LogError("[AI_Ramen] 응답 처리 실패: " + error);
                SetError("AI 응답 형식을 읽지 못했습니다.");
            }
        }
    }

    private void ApplyJudgment(AiJudgmentResponse data)
    {
        if (data == null) return;
        if (judgmentText != null) judgmentText.text = data.judge ?? "-";
        ApplyJudgmentColor(judgmentBackground, data.judge);
        if (confidenceText != null) confidenceText.text = $"{data.confidence:0.#}%";
        if (summaryText != null) summaryText.text = data.summary ?? "";
        ClearRows(factorRows);
        if (factorContent == null || factorRowPrefab == null || data.factors == null) return;
        foreach (AiFactor factor in data.factors)
        {
            AiFactorRowUI row = Instantiate(factorRowPrefab, factorContent);
            row.SetData(factor);
            factorRows.Add(row.gameObject);
        }
        RefreshReasoningLayout();
    }

    private void RefreshReasoningLayout()
    {
        if (reasoningRefreshRoutine != null) StopCoroutine(reasoningRefreshRoutine);
        reasoningRefreshRoutine = StartCoroutine(RefreshReasoningLayoutRoutine());
    }

    private IEnumerator RefreshReasoningLayoutRoutine()
    {
        yield return null;
        ResizeSummaryText();
        if (judgmentText != null) judgmentText.ForceMeshUpdate(true, true);
        if (confidenceText != null) confidenceText.ForceMeshUpdate(true, true);
        if (summaryText != null) summaryText.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases();
        RebuildReasoningLayout();

        yield return new WaitForEndOfFrame();
        ResizeSummaryText();
        Canvas.ForceUpdateCanvases();
        RebuildReasoningLayout();
        reasoningRefreshRoutine = null;
    }

    private void ResizeSummaryText()
    {
        if (summaryText == null) return;

        RectTransform textRect = summaryText.rectTransform;
        float width = textRect.rect.width;
        if (width <= 0f) width = textRect.sizeDelta.x;
        if (width <= 0f) return;

        summaryText.ForceMeshUpdate(true, true);
        float preferredHeight = summaryText.GetPreferredValues(
            summaryText.text,
            width,
            0f
        ).y;
        preferredHeight = Mathf.Max(preferredHeight, summaryText.fontSize + 2f);
        textRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            preferredHeight
        );
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);

        // 현재 씬에서 summaryText의 직계 부모가 Content이며 위아래 Padding이 5씩이다.
        RectTransform parent = textRect.parent as RectTransform;
        if (parent != null)
        {
            parent.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                preferredHeight + 10f
            );
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }
    }
    private void RebuildReasoningLayout()
    {
        RectTransform factors = factorContent as RectTransform;
        if (factors != null) LayoutRebuilder.ForceRebuildLayoutImmediate(factors);
        if (reasoningLayoutRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(reasoningLayoutRoot);
    }
    private void ApplyHistory(List<AiHistoryEntry> items)
    {
        ClearRows(historyRows);
        if (historyContent == null || historyRowPrefab == null || items == null) return;
        foreach (AiHistoryEntry item in items)
        {
            AiHistoryRowUI row = Instantiate(historyRowPrefab, historyContent);
            row.SetData(item);
            historyRows.Add(row.gameObject);
        }
    }

    private void ApplyComparison(AiCompareResponse data, AiCompareView targetView)
    {
        if (data == null) return;
        if (targetView != null) targetView.SetData(data, latest);
        if (userJudgmentText != null) userJudgmentText.text = data.user_judge ?? "-";
        if (aiJudgmentText != null) aiJudgmentText.text = data.ai_judge ?? "-";
        if (compareConfidenceText != null && latest != null)
            compareConfidenceText.text = $"신뢰도 {latest.confidence:0.#}%";
        if (comparisonText != null) comparisonText.text = data.explanation ?? "";
        if (directFactorsText != null)
            directFactorsText.text = JoinFactors(data.highlighted_factors, "직접요인");
        if (indirectFactorsText != null)
            indirectFactorsText.text = JoinFactors(data.highlighted_factors, "간접요인");
    }

    private void ApplyJudgmentColor(Image target, string judgment)
    {
        if (target == null) return;
        switch (judgment)
        {
            case "매수": target.color = buyJudgmentColor; break;
            case "매도": target.color = sellJudgmentColor; break;
            case "관망":
            default: target.color = holdJudgmentColor; break;
        }
    }
    private bool EnsureLoggedIn()
    {
        if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn())
            return true;
        SetError("로그인이 필요합니다.");
        return false;
    }
    private string SelectedSymbol()
    {
        return stockLoader != null && stockLoader.symbol != null ? stockLoader.symbol.Trim() : "";
    }

    private static string JoinFactors(Dictionary<string, List<string>> groups, string key)
    {
        if (groups == null || !groups.ContainsKey(key) || groups[key] == null || groups[key].Count == 0)
            return "• 해당 요인 없음";
        return "• " + string.Join("\n• ", groups[key]);
    }

    private static void ClearRows(List<GameObject> rows)
    {
        foreach (GameObject row in rows) if (row != null) Destroy(row);
        rows.Clear();
    }

    private void SetBusy(bool busy, string message)
    {
        isBusy = busy;
        if (loadingPanel != null) loadingPanel.SetActive(busy);
        if (loadingText != null)
        {
            loadingText.text = message ?? "";
            loadingText.gameObject.SetActive(busy);
        }
    }

    private void SetError(string message)
    {
        if (errorText == null) return;
        errorText.text = message ?? "";
        errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
    }

    private static string ParseError(UnityWebRequest request)
    {
        string response = request.downloadHandler != null ? request.downloadHandler.text : "";
        try
        {
            AiApiError parsed = JsonConvert.DeserializeObject<AiApiError>(response);
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.detail)) return parsed.detail;
        }
        catch { }
        return string.IsNullOrWhiteSpace(request.error) ? "AI 서버에 연결하지 못했습니다." : request.error;
    }
}

