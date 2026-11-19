using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuySellPanelSlider : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("움직일 패널")]
    [SerializeField] private RectTransform panelRect;

    [Header("패널 오브젝트")]
    [SerializeField] private GameObject buySell_Back_Panel;

    [Header("패널 위치")]
    [SerializeField] private float openY = 672f;
    [SerializeField] private float closedY = 0f;

    [Header("애니메이션")]
    [SerializeField] private float snapSpeed = 10f;

    [Tooltip("닫힐 때 속도 배율")]
    [SerializeField] private float closeSpeedMultiplier = 1.5f;

    [Range(0.1f, 0.9f)]
    [SerializeField] private float closeThreshold = 0.5f;

    [SerializeField]
    private OrderBookLoader orderBookLoader;

    [SerializeField]
    private StockCandlestickLoader stockLoader;

    private bool isDragging = false;
    private bool isOpened = false;

    private Coroutine slideRoutine;


    // =========================================================
    // 열기
    // =========================================================

    public void OpenPanel()
    {
        if (panelRect == null)
            return;

        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }

        if (buySell_Back_Panel != null)
        {
            buySell_Back_Panel.SetActive(true);
        }

        // 주문 패널 초기화
        // 지정가로 설정 + 현재가를 주문가격에 입력
        if (stockLoader != null)
        {
            stockLoader.PrepareOrderPanel();
        }

        SetPanelPosition(closedY);

        isOpened = true;

        slideRoutine = StartCoroutine(
            SmoothSlide(
                openY,
                false
            )
        );
    }


    // =========================================================
    // 닫기
    // =========================================================

    public void ClosePanel()
    {
        if (panelRect == null)
            return;

        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }

        isDragging = false;
        isOpened = false;

        slideRoutine = StartCoroutine(
            SmoothSlide(
                closedY,
                true
            )
        );
    }


    // =========================================================
    // 드래그 시작
    // =========================================================

    public void OnBeginDrag(
        PointerEventData eventData
    )
    {
        if (!isOpened)
            return;

        isDragging = true;

        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }
    }


    // =========================================================
    // 드래그 중
    // =========================================================

    public void OnDrag(
        PointerEventData eventData
    )
    {
        if (
            !isDragging ||
            panelRect == null
        )
        {
            return;
        }

        Vector2 currentPosition =
            panelRect.anchoredPosition;

        currentPosition.y +=
            eventData.delta.y;

        currentPosition.y =
            Mathf.Clamp(
                currentPosition.y,
                closedY,
                openY
            );

        panelRect.anchoredPosition =
            currentPosition;
    }


    // =========================================================
    // 드래그 종료
    // =========================================================

    public void OnEndDrag(
        PointerEventData eventData
    )
    {
        if (!isDragging)
            return;

        isDragging = false;

        if (panelRect == null)
            return;

        float downRatio =
            Mathf.InverseLerp(
                openY,
                closedY,
                panelRect.anchoredPosition.y
            );

        if (downRatio >= closeThreshold)
        {
            ClosePanel();
        }
        else
        {
            isOpened = true;

            if (slideRoutine != null)
            {
                StopCoroutine(slideRoutine);
                slideRoutine = null;
            }

            slideRoutine = StartCoroutine(
                SmoothSlide(
                    openY,
                    false
                )
            );
        }
    }


    // =========================================================
    // 부드러운 이동
    // =========================================================

    private IEnumerator SmoothSlide(
        float targetY,
        bool disableAfter
    )
    {
        float speed =
            snapSpeed * 100f;

        // 닫힐 때 조금 더 빠르게
        if (disableAfter)
        {
            speed *= closeSpeedMultiplier;
        }

        while (
            panelRect != null &&
            Mathf.Abs(
                panelRect.anchoredPosition.y -
                targetY
            ) > 0.1f
        )
        {
            float newY =
                Mathf.MoveTowards(
                    panelRect.anchoredPosition.y,
                    targetY,
                    speed * Time.deltaTime
                );

            panelRect.anchoredPosition =
                new Vector2(
                    panelRect.anchoredPosition.x,
                    newY
                );

            yield return null;
        }

        if (panelRect != null)
        {
            SetPanelPosition(targetY);
        }

        slideRoutine = null;

        // closedY에 도착한 즉시 배경 패널 비활성화
        if (
            disableAfter &&
            buySell_Back_Panel != null
        )
        {
            if (orderBookLoader != null)
            {
                orderBookLoader.StopTradeOrderBook();
            }

            buySell_Back_Panel.SetActive(false);
        }
    }


    // =========================================================
    // 위치 설정
    // =========================================================

    private void SetPanelPosition(
        float targetY
    )
    {
        if (panelRect == null)
            return;

        panelRect.anchoredPosition =
            new Vector2(
                panelRect.anchoredPosition.x,
                targetY
            );
    }


    // =========================================================
    // 현재 상태 확인
    // =========================================================

    public bool IsOpened()
    {
        return isOpened;
    }
}