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

    private Coroutine loadRoutine;

    public void LoadDetail(string symbol)
    {
        if (loadRoutine != null)
            StopCoroutine(loadRoutine);

        loadRoutine = StartCoroutine(LoadDetailRoutine(symbol));
    }

    private IEnumerator LoadDetailRoutine(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
            yield break;

        string url = $"{ServerConfig.HttpBaseUrl}/stock-detail?symbol={symbol}";

        Debug.Log("종목 상세 정보 요청 URL: " + url);

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            Debug.Log("종목 상세 정보 응답 코드: " + req.responseCode);
            Debug.Log("종목 상세 정보 응답 내용: " + req.downloadHandler.text);

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("종목 상세 정보 요청 실패: " + req.downloadHandler.text);
                yield break;
            }

            StockDetailResponse info =
                JsonConvert.DeserializeObject<StockDetailResponse>(req.downloadHandler.text);

            if (info == null)
            {
                Debug.LogError("종목 상세 정보 파싱 실패");
                yield break;
            }

            DrawDetail(info);
        }
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

