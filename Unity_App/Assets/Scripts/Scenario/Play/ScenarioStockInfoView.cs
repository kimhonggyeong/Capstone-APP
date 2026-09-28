using System;
using System.Text;
using TMPro;
using UnityEngine;

public class ScenarioStockInfoView : MonoBehaviour
{
    [Header("종목 기본정보")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text codeText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text changeText;
    [SerializeField] private TMP_Text volumeText;
    [SerializeField] private TMP_Text previousCloseText;

    [Header("추가 가격정보")]
    [SerializeField] private TMP_Text openText;
    [SerializeField] private TMP_Text highText;
    [SerializeField] private TMP_Text lowText;

    [Header("추가 지표")]
    [SerializeField] private TMP_Text tradingValueText;
    [SerializeField] private TMP_Text marketCapText;
    [SerializeField] private TMP_Text perText;
    [SerializeField] private TMP_Text pbrText;

    [Header("시장정보와 뉴스")]
    [SerializeField] private TMP_Text marketInfoText;
    [SerializeField] private TMP_Text newsText;

    [Header("등락 색상")]
    [SerializeField] private Color riseColor = Color.red;
    [SerializeField] private Color fallColor = Color.blue;
    [SerializeField] private Color neutralColor = Color.gray;

    public void SetData(
        AssetInfo asset,
        CurrentTurnData turnData)
    {
        if (asset == null || turnData == null)
            return;

        SetText(nameText, asset.name);
        SetText(codeText, asset.asset_id);

        if (asset.data_available)
        {
            SetText(priceText, $"{asset.current_price:N0}원");

            string change =
                asset.change.ToString("+#,##0;-#,##0;0");

            string rate =
                asset.change_pct.ToString("+0.00;-0.00;0.00");

            SetText(changeText, $"{change}원 ({rate}%)");
            SetText(volumeText, $"{asset.volume:N0}주");
            SetText(previousCloseText, $"{asset.previous_close:N0}원");

            if (changeText != null)
            {
                changeText.color = asset.change > 0
                    ? riseColor
                    : asset.change < 0
                        ? fallColor
                        : neutralColor;
            }
        }
        else
        {
            SetText(priceText, "데이터 없음");
            SetText(changeText, "-");
            SetText(volumeText, "-");
            SetText(previousCloseText, "-");

            if (changeText != null)
                changeText.color = neutralColor;
        }

        // /turn 응답에는 시가·고가·저가가 없습니다.
        // 이후 차트 API를 연결하면 이 항목을 갱신합니다.
        SetText(openText, "-");
        SetText(highText, "-");
        SetText(lowText, "-");

        // 현재 API에서 제공하지 않는 지표
        SetText(tradingValueText, "데이터 없음");
        SetText(marketCapText, "데이터 없음");
        SetText(perText, "데이터 없음");
        SetText(pbrText, "데이터 없음");

        ShowMarketInfo(asset, turnData.market_state);
        ShowNews(asset.asset_id, turnData.news);
    }

    private void ShowMarketInfo(
        AssetInfo asset,
        MarketState market)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine($"상장시장: {asset.market}");
        builder.AppendLine($"종목 업종: {asset.industry_label}");

        if (market != null)
        {
            builder.AppendLine($"투자심리: {market.sentiment}");
            builder.AppendLine($"심리지수: {market.sentiment_score}");
            builder.AppendLine($"시나리오 업종 상태: {market.sector_state}");

            if (market.risk_factors != null &&
                market.risk_factors.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("주요 위험요인");

                foreach (string risk in market.risk_factors)
                    builder.AppendLine($"• {risk}");
            }
        }

        SetText(marketInfoText, builder.ToString().TrimEnd());
    }

    private void ShowNews(
        string assetId,
        NewsInfo[] newsItems)
    {
        StringBuilder builder = new StringBuilder();

        if (newsItems != null)
        {
            foreach (NewsInfo news in newsItems)
            {
                if (news == null ||
                    news.related_assets == null ||
                    Array.IndexOf(news.related_assets, assetId) < 0)
                {
                    continue;
                }

                builder.AppendLine(news.title);
                builder.AppendLine(news.summary);
                builder.AppendLine($"출처: {news.source_name}");
                builder.AppendLine();
            }
        }

        SetText(
            newsText,
            builder.Length > 0
                ? builder.ToString().TrimEnd()
                : "현재 턴에 등록된 관련 뉴스가 없습니다."
        );
    }

    // 이후 차트 API의 마지막 캔들로 호출할 함수
    public void SetOhlc(long open, long high, long low)
    {
        SetText(openText, $"{open:N0}원");
        SetText(highText, $"{high:N0}원");
        SetText(lowText, $"{low:N0}원");
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}