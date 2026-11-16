using Newtonsoft.Json;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class StockDetailLoader : MonoBehaviour
{


    [Header("Price Detail")]
    public TMP_Text openPriceText;
    public TMP_Text highPriceText;
    public TMP_Text lowPriceText;
    public TMP_Text volumeText;

    public TMP_Text week52HighText;
    public TMP_Text week52LowText;
    public TMP_Text upperLimitText;
    public TMP_Text lowerLimitText;

    [Header("Financial Summary")]
    public TMP_Text marketCapText;
    public TMP_Text perText;
    public TMP_Text pbrText;
    public TMP_Text epsText;
    public TMP_Text bpsText;
    public TMP_Text roeText;
    public TMP_Text revenueText;
    public TMP_Text operatingProfitText;

    [Header("Investor Supply")]
    public TMP_Text individualSupplyText;
    public TMP_Text foreignSupplyText;
    public TMP_Text institutionSupplyText;

    public bool IsLoading { get; private set; }
    public string LoadError { get; private set; }

    private Coroutine loadRoutine;
    private int requestGeneration;

    public void LoadDetail(string symbol)
    {
        StopDetail();
        LoadError = null;
        ClearDetail();

        if (string.IsNullOrWhiteSpace(symbol))
        {
            LoadError = "종목 상세 정보를 불러올 종목 코드가 없습니다.";
            Debug.LogError(LoadError);
            return;
        }

        IsLoading = true;
        loadRoutine = StartCoroutine(LoadDetailRoutine(symbol.Trim(), requestGeneration));
    }

    public void StopDetail()
    {
        requestGeneration++;

        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }

        IsLoading = false;
    }

    private void OnDisable()
    {
        StopDetail();
    }

    private IEnumerator LoadDetailRoutine(string symbol, int generation)
    {
        string url = $"{ServerConfig.HttpBaseUrl}/stock-detail?symbol=" +
                     UnityWebRequest.EscapeURL(symbol);

        Debug.Log("종목 상세 정보 요청 URL: " + url);

        try
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 30;
                yield return request.SendWebRequest();

                if (generation != requestGeneration)
                    yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LoadError = "종목 상세 정보 요청 실패: " + request.error;
                    Debug.LogError(LoadError + "\n" + request.downloadHandler.text);
                    yield break;
                }

                StockDetailResponse info = null;

                try
                {
                    info = JsonConvert.DeserializeObject<StockDetailResponse>(
                        request.downloadHandler.text
                    );
                }
                catch (JsonException error)
                {
                    LoadError = "종목 상세 정보 파싱 실패: " + error.Message;
                }

                if (info == null)
                {
                    if (string.IsNullOrEmpty(LoadError))
                        LoadError = "종목 상세 정보 응답이 비어 있습니다.";

                    Debug.LogError(LoadError);
                    yield break;
                }

                DrawDetail(info);
            }
        }
        finally
        {
            if (generation == requestGeneration)
            {
                IsLoading = false;
                loadRoutine = null;
            }
        }
    }

    private void ClearDetail()
    {
        TMP_Text[] fields =
        {
            openPriceText, highPriceText, lowPriceText, volumeText,
            week52HighText, week52LowText, upperLimitText, lowerLimitText,
            marketCapText, perText, pbrText, epsText, bpsText, roeText,
            revenueText, operatingProfitText,
            individualSupplyText, foreignSupplyText, institutionSupplyText
        };

        foreach (TMP_Text field in fields)
        {
            if (field != null)
                field.text = "-";
        }

        if (individualSupplyText != null)
            individualSupplyText.color = Color.black;
        if (foreignSupplyText != null)
            foreignSupplyText.color = Color.black;
        if (institutionSupplyText != null)
            institutionSupplyText.color = Color.black;
    }

    private void DrawDetail(StockDetailResponse info)
    {
        if (openPriceText != null)
            openPriceText.text = $"₩{info.open_price:N0}";

        if (highPriceText != null)
            highPriceText.text = $"₩{info.high_price:N0}";

        if (lowPriceText != null)
            lowPriceText.text = $"₩{info.low_price:N0}";

        if (volumeText != null)
            volumeText.text = $"{info.volume:N0}";

        if (week52HighText != null)
            week52HighText.text = info.week52_high > 0 ? $"₩{info.week52_high:N0}" : "-";

        if (week52LowText != null)
            week52LowText.text = info.week52_low > 0 ? $"₩{info.week52_low:N0}" : "-";

        if (upperLimitText != null)
            upperLimitText.text = info.upper_limit > 0 ? $"₩{info.upper_limit:N0}" : "-";

        if (lowerLimitText != null)
            lowerLimitText.text = info.lower_limit > 0 ? $"₩{info.lower_limit:N0}" : "-";

        if (marketCapText != null)
            marketCapText.text = info.market_cap > 0 ? $"{info.market_cap:N0}" : "-";

        if (perText != null)
            perText.text = info.per > 0 ? $"{info.per:F2}배" : "-";

        if (pbrText != null)
            pbrText.text = info.pbr > 0 ? $"{info.pbr:F2}배" : "-";

        if (epsText != null)
            epsText.text = info.eps > 0 ? $"{info.eps:N0}" : "-";

        if (bpsText != null)
            bpsText.text = info.bps > 0 ? $"{info.bps:N0}" : "-";

        if (roeText != null)
            roeText.text = info.roe > 0 ? $"{info.roe:F2}%" : "-";

        if (revenueText != null)
            revenueText.text = info.revenue > 0 ? $"{info.revenue:N0}" : "-";

        if (operatingProfitText != null)
            operatingProfitText.text = info.operating_profit > 0 ? $"{info.operating_profit:N0}" : "-";

        SetSupplyText(individualSupplyText, info.individual_supply);
        SetSupplyText(foreignSupplyText, info.foreign_supply);
        SetSupplyText(institutionSupplyText, info.institution_supply);
    }

    private string SafeSupplyText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "중립";

        return value;
    }

    private void SetSupplyText(TMP_Text targetText, string value)
    {
        if (targetText == null)
            return;

        string text = SafeSupplyText(value);
        targetText.text = text;

        if (text == "매수 우위")
        {
            targetText.color = Color.red;
        }
        else if (text == "매도 우위")
        {
            targetText.color = Color.blue;
        }
        else
        {
            targetText.color = Color.black;
        }
    }
}

