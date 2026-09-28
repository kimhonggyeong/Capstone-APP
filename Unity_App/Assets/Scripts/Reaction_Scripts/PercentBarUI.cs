using UnityEngine;
using UnityEngine.UI;

public class PercentBarUI : MonoBehaviour
{
    [Header("Segments")]
    public Image buyImage;
    public Image sellImage;
    public Image neutralImage;

    public LayoutElement buyLayout;
    public LayoutElement sellLayout;
    public LayoutElement neutralLayout;

    [Header("Values")]
    public float buyValue = 50f;
    public float sellValue = 20f;
    public float neutralValue = 30f;

    [Header("Colors")]
    public Color buyColor = new Color(0.2f, 0.7f, 0.35f);
    public Color sellColor = new Color(1f, 0.25f, 0.2f);
    public Color neutralColor = new Color(0.75f, 0.78f, 0.82f);

    private void Start()
    {
        DrawBar();
    }

    public void SetValues(float buy, float sell, float neutral)
    {
        buyValue = buy;
        sellValue = sell;
        neutralValue = neutral;

        DrawBar();
    }

    public void DrawBar()
    {
        float total = buyValue + sellValue + neutralValue;

        if (total <= 0f)
        {
            SetSegmentVisible(buyImage, buyLayout, false);
            SetSegmentVisible(sellImage, sellLayout, false);
            SetSegmentVisible(neutralImage, neutralLayout, false);
            return;
        }

        buyImage.color = buyColor;
        sellImage.color = sellColor;
        neutralImage.color = neutralColor;

        ApplySegment(buyImage, buyLayout, buyValue);
        ApplySegment(sellImage, sellLayout, sellValue);
        ApplySegment(neutralImage, neutralLayout, neutralValue);
    }

    private void ApplySegment(Image image, LayoutElement layout, float value)
    {
        bool visible = value > 0f;

        SetSegmentVisible(image, layout, visible);

        if (!visible)
            return;

        layout.flexibleWidth = value;
        layout.flexibleHeight = 1f;
    }

    private void SetSegmentVisible(Image image, LayoutElement layout, bool visible)
    {
        if (image != null)
            image.gameObject.SetActive(visible);

        if (layout != null)
        {
            layout.flexibleWidth = visible ? layout.flexibleWidth : 0f;
            layout.flexibleHeight = visible ? 1f : 0f;
        }
    }
}