using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnEndOrderItem : MonoBehaviour
{
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text sideText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Image sideBackground;

    [SerializeField]
    private Color buyColor =
        new Color32(245, 60, 60, 255);

    [SerializeField]
    private Color sellColor =
        new Color32(50, 105, 225, 255);

    private void Awake()
    {
        FindTemplateTexts();
    }

    public void SetData(ScenarioOrderSummary order)
    {
        if (order == null)
            return;

        FindTemplateTexts();

        SetText(stockText,
            $"{order.asset_name} ({order.asset_id})");

        bool isSell = order.side == "SELL";

        SetText(sideText, isSell ? "매도" : "매수");
        SetText(quantityText, $"{order.quantity:N0}주");
        SetText(amountText, $"{order.Amount:N0}원");

        if (sideText != null)
            sideText.color = isSell ? sellColor : buyColor;
    }

    private void FindTemplateTexts()
    {
        TMP_Text[] texts =
            GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            string value = text.text.Trim();

            if (sideText == null &&
                (value == "매수" || value == "매도"))
            {
                sideText = text;
            }
            else if (quantityText == null &&
                     value.EndsWith("주"))
            {
                quantityText = text;
            }
            else if (amountText == null &&
                     value.EndsWith("원"))
            {
                amountText = text;
            }
            else if (stockText == null)
            {
                stockText = text;
            }
        }

        if (sideBackground == null && sideText != null)
            sideBackground =
                sideText.GetComponentInParent<Image>();
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}