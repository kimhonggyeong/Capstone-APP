using TMPro;
using UnityEngine;

public class ScenarioHoldingItem : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text stockNameText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text averagePriceText;
    [SerializeField] private TMP_Text currentPriceText;
    [SerializeField] private TMP_Text unrealizedPnlText;

    [Header("평가손익 색상")]
    [SerializeField]
    private Color profitColor =
        new Color32(245, 60, 60, 255);

    [SerializeField]
    private Color lossColor =
        new Color32(50, 105, 225, 255);

    [SerializeField] private Color neutralColor = Color.gray;

    public void SetData(PositionInfo position)
    {
        if (position == null)
            return;

        SetText(stockNameText, position.name);
        SetText(quantityText, $"{position.quantity:N0}주");
        SetText(averagePriceText, $"{position.avg_price:N0}원");
        SetText(currentPriceText, $"{position.current_price:N0}원");

        if (unrealizedPnlText != null)
        {
            unrealizedPnlText.text =
                FormatPnl(position.unrealized_pnl);

            unrealizedPnlText.color =
                GetPnlColor(position.unrealized_pnl);
        }
    }

    private static string FormatPnl(long pnl)
    {
        if (pnl > 0)
            return $"+{pnl:N0}원";

        return $"{pnl:N0}원";
    }

    private Color GetPnlColor(long pnl)
    {
        if (pnl > 0)
            return profitColor;

        if (pnl < 0)
            return lossColor;

        return neutralColor;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}