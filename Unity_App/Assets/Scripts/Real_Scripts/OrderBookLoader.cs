using NativeWebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderBookLoader : MonoBehaviour
{


    // =========================================================
    // 메인 호가창
    // =========================================================

    [Header("메인 호가창 - 매도호가")]
    public OrderBookRowUI[] askRows =
        new OrderBookRowUI[10];

    [Header("메인 호가창 - 매수호가")]
    public OrderBookRowUI[] bidRows =
        new OrderBookRowUI[10];


    // =========================================================
    // 매수 / 매도 주문 패널
    // =========================================================

    [Header("매수/매도 주문 패널 호가")]
    public OrderBookRowUI[] tradeRows =
        new OrderBookRowUI[10];


    // =========================================================
    // 주문 패널 요약
    // =========================================================

    [Header("주문 패널 총 잔량")]
    [Tooltip("예: 125,430주")]
    public TMP_Text tradeTotalQuantityText;

    [Header("주문 패널 잔량 비율")]
    [Tooltip("예: 67.4%")]
    public TMP_Text tradeRatioText;

    public Slider tradeRatioSlider;

    [Tooltip("Slider의 Fill 이미지")]
    public Image tradeRatioFillImage;


    // =========================================================
    // 주문 패널 색상
    // =========================================================

    [Header("주문 패널 색상")]

    // 매수
    public Color buyColor =
        new Color32(
            0xF6,
            0x6B,
            0x24,
            0xFF
        );

    // 매도
    public Color sellColor =
        new Color32(
            0x4A,
            0x90,
            0xE2,
            0xFF
        );


    // =========================================================
    // 현재가
    // =========================================================

    [Header("현재가")]
    public TMP_Text currentPriceText;


    // =========================================================
    // 내부 상태
    // =========================================================

    private string currentSymbol;

    private WebSocket orderBookSocket;

    private int socketGeneration = 0;

    private bool isClosing = false;


    /*
     * false
     * → 기존 메인 호가창
     *
     * true
     * → 매수 / 매도 주문 패널
     */
    private bool tradeMode = false;


    /*
     * tradeMode == true일 때
     *
     * true
     * → 매수
     *
     * false
     * → 매도
     */
    private bool tradeBuyMode = true;


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR

        if (orderBookSocket != null)
        {
            orderBookSocket
                .DispatchMessageQueue();
        }

#endif
    }


    // =========================================================
    // 기존 메인 호가창
    // =========================================================

    public void ShowOrderBook(
        string symbol
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                symbol
            )
        )
        {
            Debug.LogWarning(
                "호가를 불러올 종목 코드가 없습니다."
            );

            return;
        }

        tradeMode = false;

        StartOrderBook(
            symbol
        );
    }


    // =========================================================
    // 주문 패널 - 매수
    // =========================================================

    public void ShowBuyOrderBook(
        string symbol
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                symbol
            )
        )
        {
            Debug.LogWarning(
                "매수 호가를 불러올 종목 코드가 없습니다."
            );

            return;
        }

        tradeMode = true;
        tradeBuyMode = true;

        ClearTradeRows();
        ClearTradeSummary();

        StartOrderBook(
            symbol
        );
    }


    // =========================================================
    // 주문 패널 - 매도
    // =========================================================

    public void ShowSellOrderBook(
        string symbol
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                symbol
            )
        )
        {
            Debug.LogWarning(
                "매도 호가를 불러올 종목 코드가 없습니다."
            );

            return;
        }

        tradeMode = true;
        tradeBuyMode = false;

        ClearTradeRows();
        ClearTradeSummary();

        StartOrderBook(
            symbol
        );
    }


    // =========================================================
    // 공통 시작
    // =========================================================

    private void StartOrderBook(
        string symbol
    )
    {
        /*
         * 기존 연결 종료
         */
        StopOrderBook();

        currentSymbol =
            symbol.Trim();

        socketGeneration++;

        if (!tradeMode)
        {
            ClearMainRows();
        }

        ConnectOrderBookSocket(
            currentSymbol,
            socketGeneration
        );
    }


    // =========================================================
    // WebSocket 연결
    // =========================================================

    private async void ConnectOrderBookSocket(
        string symbol,
        int generation
    )
    {
        string url =
            $"{ServerConfig.WebSocketBaseUrl}/ws/orderbook/{symbol}";

        Debug.Log(
            "[호가 WebSocket 연결 시도] " +
            url
        );

        WebSocket newSocket =
            new WebSocket(
                url
            );

        orderBookSocket =
            newSocket;


        // =====================================================
        // 연결 성공
        // =====================================================

        newSocket.OnOpen += () =>
        {
            if (
                generation != socketGeneration ||
                symbol != currentSymbol
            )
            {
                return;
            }

            Debug.Log(
                "[호가 WebSocket 연결됨] " +
                symbol
            );
        };


        // =====================================================
        // 오류
        // =====================================================

        newSocket.OnError += error =>
        {
            if (
                generation !=
                socketGeneration
            )
            {
                return;
            }

            Debug.LogError(
                "[호가 WebSocket 오류] " +
                error
            );
        };


        // =====================================================
        // 종료
        // =====================================================

        newSocket.OnClose += closeCode =>
        {
            if (
                generation !=
                socketGeneration
            )
            {
                return;
            }

            Debug.Log(
                "[호가 WebSocket 종료] " +
                closeCode
            );
        };


        // =====================================================
        // 메시지 수신
        // =====================================================

        newSocket.OnMessage += bytes =>
        {
            if (
                generation != socketGeneration ||
                symbol != currentSymbol
            )
            {
                return;
            }

            try
            {
                string json =
                    System.Text.Encoding
                    .UTF8
                    .GetString(
                        bytes
                    );

                OrderBookSocketResponse response =
                    JsonConvert
                    .DeserializeObject
                    <OrderBookSocketResponse>(
                        json
                    );

                if (response == null)
                {
                    Debug.LogWarning(
                        "호가 WebSocket 응답이 비어 있음"
                    );

                    return;
                }

                if (
                    response.symbol !=
                    currentSymbol
                )
                {
                    return;
                }

                DrawOrderBook(
                    response
                );
            }
            catch (Exception error)
            {
                Debug.LogError(
                    "호가 WebSocket JSON 파싱 실패: " +
                    error.Message
                );
            }
        };


        // =====================================================
        // 실제 연결
        // =====================================================

        try
        {
            await newSocket.Connect();
        }
        catch (Exception error)
        {
            if (
                generation ==
                socketGeneration
            )
            {
                Debug.LogError(
                    "호가 WebSocket 연결 실패: " +
                    error.Message
                );
            }
        }
    }


    // =========================================================
    // 호가 출력
    // =========================================================

    private void DrawOrderBook(
        OrderBookSocketResponse response
    )
    {
        List<OrderBookRowData> asks =
            new List<OrderBookRowData>();

        List<OrderBookRowData> bids =
            new List<OrderBookRowData>();


        // =====================================================
        // ask / bid 분리
        // =====================================================

        if (response.rows != null)
        {
            foreach (
                OrderBookRowData row
                in response.rows
            )
            {
                if (row == null)
                    continue;

                if (row.side == "ask")
                {
                    asks.Add(
                        row
                    );
                }
                else if (
                    row.side == "bid"
                )
                {
                    bids.Add(
                        row
                    );
                }
            }
        }


        // =====================================================
        // 각 방향 최대 잔량
        // =====================================================

        long maxAskQuantity =
            FindMaxQuantity(
                asks
            );

        long maxBidQuantity =
            FindMaxQuantity(
                bids
            );


        // =====================================================
        // 총 매도 / 총 매수 잔량
        // =====================================================

        long totalAskQuantity =
            SumQuantity(
                asks
            );

        long totalBidQuantity =
            SumQuantity(
                bids
            );


        // =====================================================
        // 전체 잔량
        // =====================================================

        long totalQuantity =
            totalAskQuantity +
            totalBidQuantity;


        // =====================================================
        // 비율
        // =====================================================

        float askRatio = 0f;
        float bidRatio = 0f;

        if (totalQuantity > 0)
        {
            askRatio =
                (float)totalAskQuantity /
                totalQuantity;

            bidRatio =
                (float)totalBidQuantity /
                totalQuantity;
        }


        // =====================================================
        // 주문 패널
        // =====================================================

        if (tradeMode)
        {
            // =================================================
            // 매수
            // =================================================

            if (tradeBuyMode)
            {
                /*
                 * 매수 호가 10개 표시
                 */
                DrawRows(
                    tradeRows,
                    bids,
                    maxBidQuantity,
                    response.previous_close,
                    true
                );

                /*
                 * 총 매수 잔량
                 */
                DrawTradeSummary(
                    totalBidQuantity,
                    bidRatio,
                    true
                );
            }

            // =================================================
            // 매도
            // =================================================

            else
            {
                /*
                 * 매도 호가 10개 표시
                 */
                DrawRows(
                    tradeRows,
                    asks,
                    maxAskQuantity,
                    response.previous_close,
                    true
                );

                /*
                 * 총 매도 잔량
                 */
                DrawTradeSummary(
                    totalAskQuantity,
                    askRatio,
                    false
                );
            }

            return;
        }


        // =====================================================
        // 기존 메인 호가창
        // =====================================================

        /*
         * 매도
         */
        DrawRows(
            askRows,
            asks,
            maxAskQuantity,
            response.previous_close,
            false
        );


        /*
         * 매수
         */
        DrawRows(
            bidRows,
            bids,
            maxBidQuantity,
            response.previous_close,
            false
        );


        DrawCurrentPrice(
            response.current_price,
            response.previous_close
        );
    }


    // =========================================================
    // 주문 패널 요약 표시
    // =========================================================

    private void DrawTradeSummary(
        long quantity,
        float ratio,
        bool isBuy
    )
    {
        ratio =
            Mathf.Clamp01(
                ratio
            );


        // =====================================================
        // 총 잔량 값
        // =====================================================

        if (
            tradeTotalQuantityText !=
            null
        )
        {
            tradeTotalQuantityText.text =
                $"{quantity:N0}주";

            /*
             * 값 텍스트 색상
             *
             * 매수 = 주황/빨강
             * 매도 = 파랑
             */
            tradeTotalQuantityText.color =
                isBuy
                    ? buyColor
                    : sellColor;
        }


        // =====================================================
        // 퍼센트 Text
        // =====================================================

        if (
            tradeRatioText !=
            null
        )
        {
            tradeRatioText.text =
                $"{ratio * 100f:F1}%";

            tradeRatioText.color =
                isBuy
                    ? buyColor
                    : sellColor;
        }


        // =====================================================
        // Slider
        // =====================================================

        if (
            tradeRatioSlider !=
            null
        )
        {
            tradeRatioSlider.minValue =
                0f;

            tradeRatioSlider.maxValue =
                1f;

            tradeRatioSlider.value =
                ratio;

            tradeRatioSlider.interactable =
                false;

            /*
             * 항상 왼쪽 → 오른쪽
             */
            tradeRatioSlider.direction =
                Slider.Direction.LeftToRight;
        }


        // =====================================================
        // Slider Fill 색상
        // =====================================================

        if (
            tradeRatioFillImage !=
            null
        )
        {
            tradeRatioFillImage.color =
                isBuy
                    ? buyColor
                    : sellColor;
        }
    }


    // =========================================================
    // 총 잔량 계산
    // =========================================================

    private long SumQuantity(
        List<OrderBookRowData> rows
    )
    {
        long total = 0;

        if (rows == null)
            return total;

        foreach (
            OrderBookRowData row
            in rows
        )
        {
            if (row != null)
            {
                total +=
                    row.quantity;
            }
        }

        return total;
    }


    // =========================================================
    // 최대 잔량
    // =========================================================

    private long FindMaxQuantity(
        List<OrderBookRowData> rows
    )
    {
        long maxQuantity = 0;

        if (rows == null)
            return maxQuantity;

        foreach (
            OrderBookRowData row
            in rows
        )
        {
            if (
                row != null &&
                row.quantity >
                maxQuantity
            )
            {
                maxQuantity =
                    row.quantity;
            }
        }

        return maxQuantity;
    }


    // =========================================================
    // Row 출력
    // =========================================================

    private void DrawRows(
        OrderBookRowUI[] targetRows,
        List<OrderBookRowData> dataRows,
        long maxQuantity,
        long previousClose,
        bool isTradeRow
    )
    {
        if (targetRows == null)
            return;

        for (
            int i = 0;
            i < targetRows.Length;
            i++
        )
        {
            OrderBookRowUI target =
                targetRows[i];

            if (target == null)
                continue;

            if (
                dataRows != null &&
                i < dataRows.Count
            )
            {
                target.SetData(
                    dataRows[i],
                    maxQuantity,
                    previousClose,
                    isTradeRow
                );
            }
            else
            {
                target.Clear();
            }
        }
    }


    // =========================================================
    // 현재가
    // =========================================================

    private void DrawCurrentPrice(
        long currentPrice,
        long previousClose
    )
    {
        if (currentPriceText == null)
            return;

        if (currentPrice <= 0)
        {
            currentPriceText.text =
                "현재가\n-";

            currentPriceText.color =
                Color.black;

            return;
        }

        currentPriceText.text =
            $"{currentPrice:N0}";

        if (previousClose <= 0)
        {
            currentPriceText.color =
                Color.black;
        }
        else if (
            currentPrice >
            previousClose
        )
        {
            currentPriceText.color =
                new Color(
                    1f,
                    0.25f,
                    0.25f
                );
        }
        else if (
            currentPrice <
            previousClose
        )
        {
            currentPriceText.color =
                new Color(
                    0.25f,
                    0.45f,
                    1f
                );
        }
        else
        {
            currentPriceText.color =
                Color.black;
        }
    }


    // =========================================================
    // 메인 호가 초기화
    // =========================================================

    private void ClearMainRows()
    {
        if (askRows != null)
        {
            foreach (
                OrderBookRowUI row
                in askRows
            )
            {
                if (row != null)
                {
                    row.Clear();
                }
            }
        }

        if (bidRows != null)
        {
            foreach (
                OrderBookRowUI row
                in bidRows
            )
            {
                if (row != null)
                {
                    row.Clear();
                }
            }
        }

        if (currentPriceText != null)
        {
            currentPriceText.text =
                "현재가\n-";
        }
    }


    // =========================================================
    // 주문 패널 호가 초기화
    // =========================================================

    private void ClearTradeRows()
    {
        if (tradeRows == null)
            return;

        foreach (
            OrderBookRowUI row
            in tradeRows
        )
        {
            if (row != null)
            {
                row.Clear();
            }
        }
    }


    // =========================================================
    // 주문 패널 요약 초기화
    // =========================================================

    private void ClearTradeSummary()
    {
        if (
            tradeTotalQuantityText !=
            null
        )
        {
            tradeTotalQuantityText.text =
                "-";
        }

        if (
            tradeRatioText !=
            null
        )
        {
            tradeRatioText.text =
                "-";
        }

        if (
            tradeRatioSlider !=
            null
        )
        {
            tradeRatioSlider.value =
                0f;
        }
    }


    // =========================================================
    // 주문 패널 호가 종료
    // =========================================================

    public void StopTradeOrderBook()
    {
        tradeMode =
            false;

        ClearTradeRows();

        ClearTradeSummary();

        StopOrderBook();

        Debug.Log(
            "매수/매도 주문 패널 호가 종료"
        );
    }


    // =========================================================
    // 오브젝트 비활성화
    // =========================================================

    private void OnDisable()
    {
        StopOrderBook();
    }


    // =========================================================
    // WebSocket 종료
    // =========================================================

    public async void StopOrderBook()
    {
        socketGeneration++;

        currentSymbol =
            null;

        if (
            orderBookSocket == null ||
            isClosing
        )
        {
            return;
        }

        isClosing =
            true;

        WebSocket socketToClose =
            orderBookSocket;

        orderBookSocket =
            null;

        try
        {
            await socketToClose.Close();

            Debug.Log(
                "호가 WebSocket 수동 종료"
            );
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "호가 WebSocket 종료 오류: " +
                error.Message
            );
        }
        finally
        {
            isClosing =
                false;
        }
    }
}


// =========================================================
// JSON 모델
// =========================================================

[Serializable]
public class OrderBookSocketResponse
{
    public string symbol;

    public long current_price;

    public long previous_close;

    public List<OrderBookRowData> rows;
}


[Serializable]
public class OrderBookRowData
{
    public int price;

    public long quantity;

    public string side;

    public int level;
}

