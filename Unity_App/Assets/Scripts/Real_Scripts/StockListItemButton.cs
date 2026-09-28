using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StockListItemButton : MonoBehaviour
{
    [Header("텍스트")]
    public TMP_Text rankText;
    public TMP_Text nameText;

    // 기존 volumeText 오브젝트를 현재가 표시용으로 사용
    public TMP_Text volumeText;

    // 기존 trade_value 오브젝트를 선택 순위 값 표시용으로 사용
    public TMP_Text trade_value;

    [Header("버튼")]
    public Button button;

    private string symbol;
    private string stockName;

    private StockCandlestickLoader loader;
    private StockPanelManager panelManager;


    public void SetData(
        int rank,
        StockRankItem data,
        StockRankType rankType,
        bool isSearchResult,
        StockCandlestickLoader loader,
        StockPanelManager panelManager
    )
    {
        if (data == null)
            return;

        symbol = data.symbol;
        stockName = data.name;

        this.loader = loader;
        this.panelManager = panelManager;

        if (rankText != null)
        {
            rankText.text = isSearchResult
                ? "-"
                : rank.ToString();
        }

        if (nameText != null)
        {
            nameText.text =
                $"{stockName}\n({symbol})";
        }

        if (volumeText != null)
        {
            volumeText.text =
                data.price > 0
                    ? $"현재가\n{FormatWon(data.price)}"
                    : "현재가\n-";
        }

        if (trade_value != null)
        {
            trade_value.text = isSearchResult
                ? MakeSearchResultText(data)
                : MakeRankValueText(data, rankType);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickStock);
        }
    }


    private string MakeSearchResultText(StockRankItem data)
    {
        if (data.change_rate != 0f)
        {
            return
                "등락률\n" +
                FormatPercent(data.change_rate);
        }

        return "검색 결과";
    }


    private string MakeRankValueText(
        StockRankItem data,
        StockRankType rankType
    )
    {
        switch (rankType)
        {
            case StockRankType.ChangeRate:
                return
                    "등락률\n" +
                    FormatPercent(data.change_rate);

            case StockRankType.MarketCap:
                return
                    "시가총액\n" +
                    FormatMarketCap(data.market_cap);

            case StockRankType.ExecutionStrength:
                return
                    "체결강도\n" +
                    data.execution_strength.ToString("N2") +
                    "%";

            case StockRankType.Volume:
            default:
                return
                    "거래량\n" +
                    data.volume.ToString("N0") +
                    "주";
        }
    }


    private string FormatPercent(float value)
    {
        if (value > 0f)
            return $"+{value:N2}%";

        return $"{value:N2}%";
    }


    private string FormatWon(long value)
    {
        return value.ToString("N0") + "원";
    }


    private string FormatMarketCap(long value)
    {
        if (value <= 0)
            return "-";

        /*
         * KIS 시가총액 순위 응답의 시가총액 단위는
         * 응답 필드에 따라 억원 단위로 제공되는 경우가 있다.
         *
         * 값이 매우 큰 경우 원 단위로 판단하여 조/억으로 변환하고,
         * 그보다 작으면 억원 단위 값으로 표시한다.
         */

        if (value >= 1_000_000_000_000L)
        {
            double trillion =
                value / 1_000_000_000_000d;

            return $"{trillion:N1}조원";
        }

        if (value >= 100_000_000L)
        {
            double billion =
                value / 100_000_000d;

            return $"{billion:N0}억원";
        }

        // KIS에서 이미 억원 단위로 반환한 경우
        if (value >= 10_000)
        {
            double trillionByHundredMillion =
                value / 10_000d;

            return $"{trillionByHundredMillion:N1}조원";
        }

        return $"{value:N0}억원";
    }


    public void OnClickStock()
    {
        if (string.IsNullOrEmpty(symbol))
            return;

        if (panelManager != null)
        {
            panelManager.SetCurrentSymbol(symbol);
            panelManager.ShowLoading();
        }

        if (loader != null)
        {
            loader.LoadStock(
                symbol,
                stockName
            );
        }
    }
}