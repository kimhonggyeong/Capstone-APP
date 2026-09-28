using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Reaction_StockListItemButton : MonoBehaviour
{
    public TMP_Text rankText;
    public TMP_Text nameText;
    public TMP_Text volumeText;
    public TMP_Text trade_value;
    public Button button;

    private string symbol;
    private string stockName;

    private Reaction_StockCandlestickLoader loader;
    private Reaction_PanelManager panelManager;

    private Reaction_AIChatController chatController;

    public void SetData(
        int rank,
        string symbol,
        string name,
        long volume,
        long tradeValue,
        Reaction_StockCandlestickLoader loader,
        Reaction_PanelManager panelManager,
        Reaction_AIChatController chatController
    )
    {
        this.symbol = symbol;
        this.stockName = name;
        this.loader = loader;
        this.panelManager = panelManager;
        this.chatController = chatController;

        rankText.text = rank.ToString();
        nameText.text = $"{name} ({symbol})";

        volumeText.text = "거래량\n" + volume.ToString("N0");
        trade_value.text = "거래대금\n" + tradeValue.ToString("N0") + "원";

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClickStock);
    }

    private void OnClickStock()
    {
        chatController.SetStock(symbol, stockName);

        panelManager.ShowInput();

        Debug.Log($"반응 시뮬 종목 선택: {stockName} ({symbol})");
    }
}