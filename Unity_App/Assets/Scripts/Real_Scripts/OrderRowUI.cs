using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderRowUI : MonoBehaviour
{
    public TMP_Text sideText, statusText, priceText, quantityText, remainingText;
    [Header("주문 날짜 / 시간 (한국시간)")]
    public TMP_Text orderDateText;
    public TMP_Text orderTimeText;
    public Button selectButton;
    public Image selectedBackground;
    public Color normalColor = Color.white;
    public Color selectedColor = new Color(1f, .94f, .86f);
    public TradeOrderData Order { get; private set; }

    public void Bind(TradeOrderData order, Action<TradeOrderData> select,
                     Action<TradeOrderData> amend, Action<TradeOrderData> cancel)
    {
        Order = order;
        Set(sideText, order.SideLabel);
        if (sideText != null) sideText.color = order.side == "BUY" ? Color.red : Color.blue;
        Set(statusText, order.StatusLabel);
        Set(priceText, $"{order.orderPrice:N0}원");
        Set(quantityText, $"{order.quantity:N0}주");
        Set(remainingText, $"{Math.Max(0, order.quantity - order.filledQuantity):N0}주");
        Set(orderDateText, OrderTimeFormat.Korea(order.createdAt, "yyyy-MM-dd"));
        Set(orderTimeText, OrderTimeFormat.Korea(order.createdAt, "HH:mm:ss"));
        Wire(selectButton, () => select(order));
        SetInteractable(true);
        SetSelected(false);
    }
    static void Set(TMP_Text text, string value) { if (text != null) text.text = value; }
    static void Wire(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callback);
    }
    public void SetInteractable(bool enabled)
    {
        if (selectButton != null) selectButton.interactable = enabled;
    }
    public void SetSelected(bool selected)
    {
        if (selectedBackground != null) selectedBackground.color = selected ? selectedColor : normalColor;
    }
}
