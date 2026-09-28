using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Past_StockListItemButton : MonoBehaviour
{
    //public TMP_Text rankText;
    //public TMP_Text nameText;
    //public TMP_Text volumeText;
    //public TMP_Text trade_value;
    public Button button;

    private string symbol;
    private string stockName;

    //private StockCandlestickLoader loader;

    private Past_StockPanelManager panelManager;


    public void SetData(
        int rank,
        string symbol,
        string name,
        long volume,
        long tradeValue,
        //StockCandlestickLoader loader,
        Past_StockPanelManager panelManager
    )
    {
        this.symbol = symbol;
        this.stockName = name;

        //this.loader = loader;
        this.panelManager = panelManager;

        //rankText.text = rank.ToString();
        //nameText.text = $"{name} ({symbol})";

        //volumeText.text =
        //    "거래량\n" + volume.ToString("N0");
        //trade_value.text =
        //    "거래대금\n" + tradeValue.ToString("N0") + "원";

        //button.onClick.RemoveAllListeners();
        //button.onClick.AddListener(OnClickStock);
    }

    public void OnClickStock()
    {
        //if (panelManager != null)
        //    panelManager.SetCurrentSymbol(symbol);

        //if (panelManager != null)
        //    panelManager.ShowLoading();

        //if (loader != null)
        //    loader.LoadStock(symbol, stockName);

        panelManager = FindFirstObjectByType<Past_StockPanelManager>();
        panelManager.ShowChartTest();
    }
}