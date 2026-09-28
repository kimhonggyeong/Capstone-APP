using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class Reaction_AIChatController : MonoBehaviour
{
    public int requestTimeoutSeconds = 120;
    [Header("연결")]
    public Reaction_PanelManager panelManager;
    public Reaction_ResultPanel resultPanel;
    public StockCandlestickLoader stockLoader;
    [Header("입력 화면")]
    public TMP_InputField inputField;
    public TMP_Text stockText, characterCountText;
    [Header("입력 안내 표시 - Inspector에서 직접 연결")]
    public TMP_Text validationText;
    public Image validationIconImage;
    public Image validationBackgroundImage;
    public Button analyzeButton;
    public TMP_Text analyzeButtonText;
    public GameObject analysisLoadingObject;
    [Header("입력 유형 버튼 이미지")]
    public Image newsButtonImage, disclosureButtonImage, industryButtonImage, scenarioButtonImage;
    public Color selectedColor = new Color(1f, .39f, .08f, 1f);
    public Color normalColor = Color.white;
    [Header("안내 / 경고 색상")]
    public Color warningTextColor = new Color32(198, 40, 40, 255);
    public Color warningIconColor = new Color32(198, 40, 40, 255);
    public Color warningBackgroundColor = new Color32(255, 235, 238, 255);
    public Color successTextColor = new Color32(46, 125, 50, 255);
    public Color successIconColor = new Color32(46, 125, 50, 255);
    public Color successBackgroundColor = new Color32(232, 245, 233, 255);
    string selectedType = "real_news";
    bool requesting;
    bool resultReady;
    string analyzedSignature;
    string requestErrorMessage;
    string requestErrorSignature;
    SimulationResponseDto latestResult;
    Color normalValidationTextColor;
    Color normalValidationIconColor;
    Color normalValidationBackgroundColor;

    void Awake()
    {
        normalValidationTextColor = validationText != null
            ? validationText.color
            : new Color32(102, 102, 102, 255);
        normalValidationIconColor = validationIconImage != null
            ? validationIconImage.color
            : Color.white;
        normalValidationBackgroundColor = validationBackgroundImage != null
            ? validationBackgroundImage.color
            : Color.clear;
    }
    void Start() { SelectNewsEvent(); RefreshUI(); }
    void Update() { RefreshUI(); }
    void RefreshUI()
    {
        string text = inputField != null ? inputField.text : "";
        if (characterCountText != null) characterCountText.text = $"{text.Length}/1500";
        string symbol = stockLoader != null ? stockLoader.symbol : "";
        string name = stockLoader != null && stockLoader.stockNameText != null ? stockLoader.stockNameText.text : symbol;
        if (stockText != null) stockText.text = string.IsNullOrWhiteSpace(name) ? "종목을 선택해 주세요" : $"{name} ({symbol})";
        bool valid = !requesting && !string.IsNullOrWhiteSpace(symbol) && text.Trim().Length >= 10 && text.Length <= 1500;
        if (!string.IsNullOrEmpty(requestErrorMessage) && CurrentSignature() != requestErrorSignature)
        {
            requestErrorMessage = null;
            requestErrorSignature = null;
        }
        if (resultReady && CurrentSignature() != analyzedSignature) InvalidateResult();
        if (analyzeButton != null) analyzeButton.interactable = valid;
        if (analyzeButtonText != null) analyzeButtonText.text = requesting ? "AI 분석 중..." : resultReady ? "결과 보러가기" : "AI 분석 시작";
        if (!requesting)
        {
            if (!string.IsNullOrEmpty(requestErrorMessage))
                SetValidation(requestErrorMessage, ValidationTone.Warning);
            else if (string.IsNullOrWhiteSpace(symbol))
                SetValidation("종목을 선택해 주세요.", ValidationTone.Warning);
            else if (text.Length > 1500)
                SetValidation("1,500자 이하로 입력해 주세요.", ValidationTone.Warning);
            else if (text.Trim().Length > 0 && text.Trim().Length < 10)
                SetValidation("10자 이상 입력해 주세요.", ValidationTone.Warning);
            else if (resultReady)
                SetValidation("분석이 완료되었습니다. 결과 보러가기를 눌러 주세요.", ValidationTone.Success);
            else
                SetValidation("입력 내용과 선택 종목을 바탕으로 분석합니다.", ValidationTone.Normal);
        }
    }
    public void SelectNewsEvent() { SelectType("real_news", newsButtonImage); }
    public void SelectCompanyDisclosure() { SelectType("company_information", disclosureButtonImage); }
    public void SelectIndustryInformation() { SelectType("industry_information", industryButtonImage); }
    public void SelectHypotheticalScenario() { SelectType("hypothetical_scenario", scenarioButtonImage); }
    void SelectType(string type, Image selected)
    {
        if (selectedType != type) InvalidateResult();
        selectedType = type;
        Paint(newsButtonImage, newsButtonImage == selected); Paint(disclosureButtonImage, disclosureButtonImage == selected);
        Paint(industryButtonImage, industryButtonImage == selected); Paint(scenarioButtonImage, scenarioButtonImage == selected);
    }
    void Paint(Image image, bool selected) { if (image != null) image.color = selected ? selectedColor : normalColor; }
    // Legacy stock-list compatibility. The new flow reads the currently open mock-investment stock.
    public void SetStock(string symbol, string stockName) { RefreshUI(); }
    public void OnClickPrimaryButton()
    {
        if (requesting) return;
        if (resultReady && CurrentSignature() == analyzedSignature)
        {
            if (panelManager == null)
            {
                Debug.LogError("[Reaction] Panel Manager is not assigned.");
                return;
            }
            panelManager.ShowResult();
            if (resultPanel != null) resultPanel.RefreshLayout();
            return;
        }
        StartCoroutine(RequestAnalysis());
    }
    public void StartAnalysis() { OnClickPrimaryButton(); }
    IEnumerator RequestAnalysis()
    {
        string text = inputField != null ? inputField.text.Trim() : "";
        if (stockLoader == null || string.IsNullOrWhiteSpace(stockLoader.symbol) || text.Length < 10) yield break;
        requesting = true;
        resultReady = false;
        if (analysisLoadingObject != null) analysisLoadingObject.SetActive(true);
        if (panelManager != null) panelManager.ShowLoadingOnly();
        string name = stockLoader.stockNameText != null ? stockLoader.stockNameText.text : stockLoader.symbol;
        var body = new SimulationRequestDto {
            user_id = AuthManager.Instance != null && !string.IsNullOrWhiteSpace(AuthManager.Instance.UserId) ? AuthManager.Instance.UserId : "unity_guest",
            selected_stock = new SelectedStockDto { code = stockLoader.symbol, name = name }, input_text = text, input_type_hint = selectedType };
        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
        using (var request = new UnityWebRequest(ServerConfig.HttpBaseUrl + "/simulate", "POST"))
        {
            request.timeout = requestTimeoutSeconds; request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer(); request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest(); requesting = false;
            if (analysisLoadingObject != null) analysisLoadingObject.SetActive(false);
            if (panelManager != null) panelManager.HideLoading();
            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = ReadError(request.downloadHandler.text, request.error);
                Debug.LogError($"[Reaction] Request failed: {request.responseCode} / {message}\n{request.downloadHandler.text}");
                SetRequestError(message);
                yield break;
            }
            SimulationResponseDto response = null;
            try { response = JsonConvert.DeserializeObject<SimulationResponseDto>(request.downloadHandler.text); }
            catch (System.Exception e)
            {
                Debug.LogError("[Reaction] Response parsing failed: " + e + "\n" + request.downloadHandler.text);
                SetRequestError("서버 응답을 처리하지 못했습니다.");
                yield break;
            }
            if (response == null || !string.Equals(response.status, "ok", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[Reaction] Unexpected response: " + request.downloadHandler.text);
                SetRequestError("서버 응답이 올바르지 않습니다.");
                yield break;
            }

            // Complete the request state before binding optional result widgets.
            // A chart/UI binding error must not discard a successfully received result.
            latestResult = response;
            analyzedSignature = CurrentSignature();
            requestErrorMessage = null;
            requestErrorSignature = null;
            resultReady = true;
            try
            {
                if (resultPanel != null) resultPanel.SetResult(response);
                else Debug.LogError("[Reaction] Result Panel is not assigned.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Reaction] Failed while binding result UI: " + e);
            }
            SetValidation("분석이 완료되었습니다. 결과 보러가기를 눌러 주세요.", ValidationTone.Success);
        }
    }
    string CurrentSignature()
    {
        string text = inputField != null ? inputField.text.Trim() : "";
        string symbol = stockLoader != null ? stockLoader.symbol : "";
        return symbol + "\n" + selectedType + "\n" + text;
    }
    void InvalidateResult()
    {
        resultReady = false;
        analyzedSignature = null;
        latestResult = null;
        requestErrorMessage = null;
        requestErrorSignature = null;
    }
    void SetRequestError(string message)
    {
        requestErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "시장 반응 서버에 연결하지 못했습니다."
            : message;
        requestErrorSignature = CurrentSignature();
        SetValidation(requestErrorMessage, ValidationTone.Warning);
    }
    void SetValidation(string message, ValidationTone tone)
    {
        if (validationText != null)
        {
            validationText.text = message;
            validationText.color = tone == ValidationTone.Warning
                ? warningTextColor
                : tone == ValidationTone.Success
                    ? successTextColor
                    : normalValidationTextColor;
        }

        if (validationBackgroundImage != null)
        {
            validationBackgroundImage.color = tone == ValidationTone.Warning
                ? warningBackgroundColor
                : tone == ValidationTone.Success
                    ? successBackgroundColor
                    : normalValidationBackgroundColor;
        }

        if (validationIconImage != null)
        {
            validationIconImage.color = tone == ValidationTone.Warning
                ? warningIconColor
                : tone == ValidationTone.Success
                    ? successIconColor
                    : normalValidationIconColor;
        }
    }
    enum ValidationTone
    {
        Normal,
        Warning,
        Success
    }
    static string ReadError(string json, string fallback)
    {
        try { var root = JToken.Parse(json); return (root["message"] ?? root["detail"]?["message"] ?? root["detail"])?.ToString() ?? fallback; }
        catch { return string.IsNullOrWhiteSpace(fallback) ? "시장 반응 서버에 연결하지 못했습니다." : fallback; }
    }
}


