using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderBookRowUI : MonoBehaviour
{
    [Header("UI 연결")]
    public TMP_Text priceText;
    public TMP_Text quantityText;

    public Slider quantitySlider;
    public Image sliderFillImage;

    [Header("주문 패널 가격 배경 - 선택")]
    [Tooltip("매수/매도 주문 패널의 TextImage를 연결")]
    public Image priceBackgroundImage;


    // =========================================================
    // 메인 호가 가격 색상
    // =========================================================

    [Header("메인 호가 가격 색상")]

    public Color risePriceColor =
        new Color(1f, 0.25f, 0.25f);

    public Color fallPriceColor =
        new Color(0.25f, 0.45f, 1f);

    public Color equalPriceColor =
        Color.black;


    // =========================================================
    // 잔량 슬라이더 색상
    // =========================================================

    [Header("잔량 슬라이더 색상")]

    // 매도 = 파랑 계열
    public Color askSliderColor =
        new Color(0.55f, 0.72f, 1f, 0.8f);

    // 매수 = 빨강 계열
    public Color bidSliderColor =
        new Color(1f, 0.55f, 0.55f, 0.8f);


    // =========================================================
    // 주문 패널 가격 배경 색상
    // =========================================================

    [Header("주문 패널 가격 배경 색상")]

    // 매도
    public Color askPriceBackgroundColor =
        new Color32(
            220,
            235,
            255,
            255
        );

    // 매수
    public Color bidPriceBackgroundColor =
        new Color32(
            255,
            225,
            225,
            255
        );


    // =========================================================
    // 주문 패널 가격 글자 색상
    // =========================================================

    [Header("주문 패널 가격 글자 색상")]

    // 매도
    public Color askTradePriceColor =
        new Color32(
            70,
            120,
            255,
            255
        );

    // 매수
    public Color bidTradePriceColor =
        new Color32(
            255,
            80,
            80,
            255
        );


    // =========================================================
    // 데이터 적용
    // =========================================================

    public void SetData(
        OrderBookRowData row,
        long maxQuantity,
        long previousClose,
        bool isTradeRow
    )
    {
        if (row == null)
        {
            Clear();
            return;
        }

        gameObject.SetActive(true);


        // =====================================================
        // 가격 텍스트
        // =====================================================

        if (priceText != null)
        {
            priceText.text =
                row.price.ToString("N0");


            if (isTradeRow)
            {
                /*
                 * 매수/매도 주문 패널
                 *
                 * 매도 = 파랑
                 * 매수 = 빨강
                 */
                priceText.color =
                    row.side == "ask"
                        ? askTradePriceColor
                        : bidTradePriceColor;
            }
            else
            {
                /*
                 * 기존 메인 호가창
                 *
                 * 전일 종가 기준으로
                 * 상승/하락 색상 적용
                 */
                priceText.color =
                    GetPriceColor(
                        row.price,
                        previousClose
                    );
            }
        }


        // =====================================================
        // 가격 뒤 배경 이미지
        // =====================================================

        if (priceBackgroundImage != null)
        {
            if (isTradeRow)
            {
                priceBackgroundImage.color =
                    row.side == "ask"
                        ? askPriceBackgroundColor
                        : bidPriceBackgroundColor;
            }
        }


        // =====================================================
        // 잔량 텍스트
        // =====================================================

        if (quantityText != null)
        {
            quantityText.text =
                row.quantity.ToString("N0");

            quantityText.color =
                Color.black;
        }


        // =====================================================
        // 잔량 비율
        // =====================================================

        float ratio = 0f;

        if (maxQuantity > 0)
        {
            ratio =
                (float)row.quantity /
                maxQuantity;
        }

        ratio =
            Mathf.Clamp01(
                ratio
            );


        // =====================================================
        // Slider
        // =====================================================

        if (quantitySlider != null)
        {
            quantitySlider.minValue =
                0f;

            quantitySlider.maxValue =
                1f;

            quantitySlider.value =
                ratio;

            quantitySlider.interactable =
                false;


            if (isTradeRow)
            {
                /*
                 * 매수/매도 주문 패널
                 *
                 * 매수든 매도든 전부
                 * 왼쪽 → 오른쪽
                 */
                quantitySlider.direction =
                    Slider.Direction.LeftToRight;
            }
            else
            {
                /*
                 * 기존 메인 호가창
                 *
                 * 매도
                 * 오른쪽 → 왼쪽
                 *
                 * 매수
                 * 왼쪽 → 오른쪽
                 */
                quantitySlider.direction =
                    row.side == "ask"
                        ? Slider.Direction.RightToLeft
                        : Slider.Direction.LeftToRight;
            }
        }


        // =====================================================
        // Slider 색상
        // =====================================================

        if (sliderFillImage != null)
        {
            sliderFillImage.color =
                row.side == "ask"
                    ? askSliderColor
                    : bidSliderColor;
        }
    }


    // =========================================================
    // 메인 호가 가격 색상
    // =========================================================

    private Color GetPriceColor(
        long price,
        long previousClose
    )
    {
        if (previousClose <= 0)
        {
            return equalPriceColor;
        }

        if (price > previousClose)
        {
            return risePriceColor;
        }

        if (price < previousClose)
        {
            return fallPriceColor;
        }

        return equalPriceColor;
    }


    // =========================================================
    // 초기화
    // =========================================================

    public void Clear()
    {
        if (priceText != null)
        {
            priceText.text =
                string.Empty;

            priceText.color =
                equalPriceColor;
        }


        if (quantityText != null)
        {
            quantityText.text =
                string.Empty;
        }


        if (quantitySlider != null)
        {
            quantitySlider.value =
                0f;
        }
    }
}