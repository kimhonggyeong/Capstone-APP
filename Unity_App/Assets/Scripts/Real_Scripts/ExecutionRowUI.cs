using TMPro;
using UnityEngine;

public class ExecutionRowUI : MonoBehaviour
{
    [Header("텍스트 연결")]
    public TMP_Text timeText;
    public TMP_Text priceText;
    public TMP_Text sideText;
    public TMP_Text quantityText;

    [Header("색상")]
    public Color buyColor =
        new Color(1f, 0.25f, 0.25f);

    public Color sellColor =
        new Color(0.25f, 0.45f, 1f);

    public Color neutralColor =
        Color.black;

    public ExecutionItem CurrentItem
    {
        get;
        private set;
    }


    public void SetData(ExecutionItem item)
    {
        CurrentItem = item;

        if (item == null)
        {
            Clear();
            return;
        }

        gameObject.SetActive(true);

        if (timeText != null)
        {
            timeText.text =
                FormatTime(item.time);
        }

        if (priceText != null)
        {
            priceText.text =
                item.price.ToString("N0");
        }

        if (sideText != null)
        {
            sideText.text =
                item.side == "buy"
                    ? "매수"
                    : item.side == "sell"
                        ? "매도"
                        : "-";
        }

        if (quantityText != null)
        {
            quantityText.text =
                $"{item.quantity:N0}주";
        }

        Color targetColor =
            item.side == "buy"
                ? buyColor
                : item.side == "sell"
                    ? sellColor
                    : neutralColor;

        if (priceText != null)
        {
            priceText.color =
                targetColor;
        }

        if (sideText != null)
        {
            sideText.color =
                targetColor;
        }
    }


    private string FormatTime(string rawTime)
    {
        if (string.IsNullOrEmpty(rawTime))
            return "-";

        string numbersOnly =
            rawTime.Replace(":", "");

        if (numbersOnly.Length < 6)
            return rawTime;

        return
            $"{numbersOnly.Substring(0, 2)}:" +
            $"{numbersOnly.Substring(2, 2)}:" +
            $"{numbersOnly.Substring(4, 2)}";
    }


    public void Clear()
    {
        CurrentItem = null;

        if (timeText != null)
            timeText.text = string.Empty;

        if (priceText != null)
            priceText.text = string.Empty;

        if (sideText != null)
            sideText.text = string.Empty;

        if (quantityText != null)
            quantityText.text = string.Empty;
    }
}