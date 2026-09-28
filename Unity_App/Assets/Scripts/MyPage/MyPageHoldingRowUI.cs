using TMPro;
using UnityEngine;

public class MyPageHoldingRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text stockNameText;
    [SerializeField] private TMP_Text stockCodeText;
    [SerializeField] private TMP_Text currentPriceText;
    [SerializeField] private TMP_Text changeRateText;
    [SerializeField] private TMP_Text evaluationAmountText;
    [SerializeField] private Color riseColor = new Color(0.965f, 0.42f, 0.14f, 1f);
    [SerializeField] private Color fallColor = new Color(0.29f, 0.56f, 0.89f, 1f);
    [SerializeField] private Color neutralColor = new Color(0.44f, 0.44f, 0.44f, 1f);

    public void Bind(Holding holding, StockInfoResponse quote)
    {
        string name = !string.IsNullOrWhiteSpace(quote?.name)
            ? quote.name
            : !string.IsNullOrWhiteSpace(holding?.name) ? holding.name : holding?.symbol;
        long price = quote != null ? quote.price : 0;
        double rate = quote != null ? quote.change_rate : 0d;
        long evaluation = holding != null ? price * holding.quantity : 0L;

        SetText(stockNameText, string.IsNullOrWhiteSpace(name) ? "-" : name);
        SetText(stockCodeText, holding?.symbol ?? "-");
        SetText(currentPriceText, price > 0 ? $"{price:N0}원" : "-");
        SetText(evaluationAmountText, price > 0 ? $"{evaluation:N0}원" : "-");

        if (changeRateText != null)
        {
            changeRateText.text = rate > 0d ? $"+{rate:F2}%" : $"{rate:F2}%";
            changeRateText.color = rate > 0d ? riseColor : rate < 0d ? fallColor : neutralColor;
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
