using NativeWebSocket;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ExecutionListLoader : MonoBehaviour
{


    [Header("목록")]
    public ScrollRect scrollRect;
    public RectTransform content;
    public ExecutionRowUI rowPrefab;

    [Header("로딩 설정")]
    [Range(1, 30)]
    public int initialLoadCount = 30;

    [Range(1, 30)]
    public int loadMoreCount = 30;

    [Tooltip("Unity 화면에 유지할 최대 체결 Row 수")]
    public int maxVisibleRows = 300;

    [Tooltip("스크롤이 이 위치 이하로 내려가면 이전 체결 요청")]
    [Range(0f, 0.2f)]
    public float bottomThreshold = 0.05f;

    [Header("상태 표시 - 선택")]
    public TMP_Text emptyText;
    public TMP_Text loadingText;

    private readonly List<ExecutionRowUI> rows =
        new List<ExecutionRowUI>();

    /*
     * REST와 WebSocket에서 같은 체결이 겹칠 수 있으므로
     * 체결 식별키를 Unity에서 보관한다.
     */
    private readonly HashSet<string> loadedExecutionKeys =
        new HashSet<string>();

    private WebSocket executionSocket;
    private Coroutine loadingRoutine;

    private string currentSymbol = "";

    /*
     * 현재 목록에서 가장 오래된 체결시간.
     * 예: 101530
     */
    private string oldestExecutionTime = "";

    private bool isLoading;
    private bool hasMore = true;
    private bool isClosing;

    /*
     * 이전 요청 응답이 종목 변경 후 도착하는 것을 방지한다.
     */
    private int requestGeneration;


    private void Awake()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(
                OnScrollValueChanged
            );
        }

        SetLoadingVisible(false);
        SetEmptyVisible(false);
    }


    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (executionSocket != null)
        {
            executionSocket.DispatchMessageQueue();
        }
#endif
    }


    private void OnDestroy()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(
                OnScrollValueChanged
            );
        }

        StopExecutions();
    }


    // =========================================================
    // 체결 탭 열기
    // =========================================================

    public void ShowExecutions(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            Debug.LogWarning(
                "체결 목록 종목코드가 없습니다."
            );
            return;
        }

        /*
         * 기존 연결과 목록을 먼저 정리한다.
         */
        StopExecutions();

        requestGeneration++;

        currentSymbol = symbol.Trim();
        oldestExecutionTime = "";
        hasMore = true;
        isLoading = false;

        ClearRows();

        int generation = requestGeneration;

        /*
         * REST 조회보다 WebSocket을 먼저 연결한다.
         *
         * REST를 먼저 호출하면 REST 응답을 기다리는 동안 발생한
         * 체결이 누락될 수 있기 때문이다.
         */
        ConnectExecutionSocket(
            currentSymbol,
            generation
        );

        loadingRoutine = StartCoroutine(
            LoadHistory(
                symbol: currentSymbol,
                beforeTime: "",
                limit: initialLoadCount,
                generation: generation
            )
        );
    }


    // =========================================================
    // REST 체결내역 조회
    // =========================================================

    private IEnumerator LoadHistory(
        string symbol,
        string beforeTime,
        int limit,
        int generation
    )
    {
        if (
            isLoading ||
            string.IsNullOrWhiteSpace(symbol)
        )
        {
            yield break;
        }

        isLoading = true;
        SetLoadingVisible(true);

        int requestLimit =
            Mathf.Clamp(limit, 1, 30);

        string url =
            $"{ServerConfig.HttpBaseUrl}/executions/history" +
            $"?symbol={UnityWebRequest.EscapeURL(symbol)}" +
            $"&limit={requestLimit}";

        if (!string.IsNullOrWhiteSpace(beforeTime))
        {
            string normalizedBeforeTime =
                NormalizeRawTime(beforeTime);

            if (!string.IsNullOrEmpty(normalizedBeforeTime))
            {
                url +=
                    $"&before_time=" +
                    UnityWebRequest.EscapeURL(
                        normalizedBeforeTime
                    );
            }
        }

        Debug.Log(
            "[체결내역 요청] " + url
        );

        using (
            UnityWebRequest request =
                UnityWebRequest.Get(url)
        )
        {
            yield return request.SendWebRequest();

            if (
                generation != requestGeneration ||
                symbol != currentSymbol
            )
            {
                FinishLoading();
                yield break;
            }

            if (
                request.result !=
                UnityWebRequest.Result.Success
            )
            {
                Debug.LogError(
                    "체결내역 요청 실패\n" +
                    $"HTTP: {request.responseCode}\n" +
                    $"오류: {request.error}\n" +
                    $"응답: {request.downloadHandler.text}"
                );

                FinishLoading();
                yield break;
            }

            ExecutionHistoryResponse response;

            try
            {
                response =
                    JsonConvert.DeserializeObject
                    <ExecutionHistoryResponse>(
                        request.downloadHandler.text
                    );
            }
            catch (Exception error)
            {
                Debug.LogError(
                    "체결내역 JSON 파싱 실패: " +
                    error.Message
                );

                FinishLoading();
                yield break;
            }

            if (response == null)
            {
                FinishLoading();
                yield break;
            }

            hasMore = response.has_more;

            if (response.items != null)
            {
                foreach (
                    ExecutionItem item
                    in response.items
                )
                {
                    AddExecutionToBottom(item);
                }
            }

            if (
                !string.IsNullOrWhiteSpace(
                    response.next_cursor
                )
            )
            {
                oldestExecutionTime =
                    NormalizeRawTime(
                        response.next_cursor
                    );
            }
            else
            {
                UpdateOldestExecutionTime();
            }

            SetEmptyVisible(
                rows.Count == 0
            );
        }

        FinishLoading();
    }


    private void FinishLoading()
    {
        isLoading = false;
        loadingRoutine = null;

        SetLoadingVisible(false);
    }


    // =========================================================
    // 실시간 WebSocket
    // =========================================================

    private async void ConnectExecutionSocket(
        string symbol,
        int generation
    )
    {
        string socketUrl =
            $"{ServerConfig.WebSocketBaseUrl}/ws/executions/{symbol}";

        Debug.Log(
            "[체결 WebSocket 연결 시도] " +
            socketUrl
        );

        WebSocket newSocket =
            new WebSocket(socketUrl);

        executionSocket = newSocket;

        newSocket.OnOpen += () =>
        {
            if (
                generation != requestGeneration ||
                symbol != currentSymbol
            )
            {
                return;
            }

            Debug.Log(
                "[체결 WebSocket 연결됨] " +
                symbol
            );
        };

        newSocket.OnError += error =>
        {
            if (generation != requestGeneration)
                return;

            Debug.LogError(
                "[체결 WebSocket 오류] " +
                error
            );
        };

        newSocket.OnClose += closeCode =>
        {
            if (generation != requestGeneration)
                return;

            Debug.Log(
                "[체결 WebSocket 종료] " +
                closeCode
            );
        };

        newSocket.OnMessage += bytes =>
        {
            if (
                generation != requestGeneration ||
                symbol != currentSymbol
            )
            {
                return;
            }

            try
            {
                string json =
                    System.Text.Encoding.UTF8
                    .GetString(bytes);

                ExecutionSocketMessage message =
                    JsonConvert.DeserializeObject
                    <ExecutionSocketMessage>(json);

                if (
                    message == null ||
                    message.item == null ||
                    message.symbol != currentSymbol
                )
                {
                    return;
                }

                AddExecutionToTop(
                    message.item
                );
            }
            catch (Exception error)
            {
                Debug.LogError(
                    "실시간 체결 JSON 파싱 실패: " +
                    error.Message
                );
            }
        };

        try
        {
            await newSocket.Connect();
        }
        catch (Exception error)
        {
            if (generation == requestGeneration)
            {
                Debug.LogError(
                    "체결 WebSocket 연결 실패: " +
                    error.Message
                );
            }
        }
    }


    // =========================================================
    // 스크롤 추가 조회
    // =========================================================

    private void OnScrollValueChanged(Vector2 position)
    {
        if (
            scrollRect == null ||
            isLoading ||
            !hasMore ||
            string.IsNullOrEmpty(currentSymbol) ||
            string.IsNullOrEmpty(oldestExecutionTime)
        )
        {
            return;
        }

        /*
         * verticalNormalizedPosition
         * 1 = 가장 위
         * 0 = 가장 아래
         */
        if (
            scrollRect.verticalNormalizedPosition >
            bottomThreshold
        )
        {
            return;
        }

        int generation = requestGeneration;

        loadingRoutine = StartCoroutine(
            LoadHistory(
                symbol: currentSymbol,
                beforeTime: oldestExecutionTime,
                limit: loadMoreCount,
                generation: generation
            )
        );
    }


    // =========================================================
    // Row 추가
    // =========================================================

    private void AddExecutionToTop(
        ExecutionItem item
    )
    {
        if (!IsValidNewItem(item))
            return;

        ExecutionRowUI row =
            Instantiate(
                rowPrefab,
                content
            );

        row.SetData(item);
        row.transform.SetAsFirstSibling();

        rows.Insert(0, row);

        loadedExecutionKeys.Add(
            MakeExecutionKey(item)
        );

        RemoveExcessRowsFromBottom();

        SetEmptyVisible(false);

        Canvas.ForceUpdateCanvases();

        /*
         * 사용자가 목록 맨 위를 보고 있을 때만
         * 새 체결 발생 후 맨 위를 유지한다.
         *
         * 과거 체결을 보고 있다면 스크롤 위치를 강제로
         * 맨 위로 올리지 않는다.
         */
        if (
            scrollRect != null &&
            scrollRect.verticalNormalizedPosition >= 0.95f
        )
        {
            scrollRect.verticalNormalizedPosition =
                1f;
        }
    }


    private void AddExecutionToBottom(
        ExecutionItem item
    )
    {
        if (!IsValidNewItem(item))
            return;

        ExecutionRowUI row =
            Instantiate(
                rowPrefab,
                content
            );

        row.SetData(item);
        row.transform.SetAsLastSibling();

        rows.Add(row);

        loadedExecutionKeys.Add(
            MakeExecutionKey(item)
        );

        string itemTime =
            NormalizeRawTime(item.time);

        if (
            !string.IsNullOrEmpty(itemTime) &&
            (
                string.IsNullOrEmpty(oldestExecutionTime) ||
                string.CompareOrdinal(
                    itemTime,
                    oldestExecutionTime
                ) < 0
            )
        )
        {
            oldestExecutionTime = itemTime;
        }

        RemoveExcessRowsFromTop();
    }


    private bool IsValidNewItem(
        ExecutionItem item
    )
    {
        if (
            item == null ||
            item.price <= 0 ||
            item.quantity <= 0 ||
            string.IsNullOrWhiteSpace(item.time)
        )
        {
            return false;
        }

        string key =
            MakeExecutionKey(item);

        return !loadedExecutionKeys.Contains(key);
    }


    /*
     * REST와 WebSocket에서 동일 체결이 들어오는 것을 막기 위한 키.
     *
     * 누적 거래량이 있으면 누적 거래량까지 사용한다.
     * 같은 시각, 같은 가격, 같은 수량 체결이 반복되는 경우를
     * 최대한 구분하기 위함이다.
     */
    private string MakeExecutionKey(
        ExecutionItem item
    )
    {
        if (item == null)
            return "";

        string rawTime =
            NormalizeRawTime(item.time);

        return
            $"{item.symbol}|" +
            $"{rawTime}|" +
            $"{item.price}|" +
            $"{item.quantity}|" +
            $"{item.cumulative_volume}";
    }


    private void UpdateOldestExecutionTime()
    {
        string oldest = "";

        foreach (
            ExecutionRowUI row
            in rows
        )
        {
            if (
                row == null ||
                row.CurrentItem == null
            )
            {
                continue;
            }

            string itemTime =
                NormalizeRawTime(
                    row.CurrentItem.time
                );

            if (string.IsNullOrEmpty(itemTime))
                continue;

            if (
                string.IsNullOrEmpty(oldest) ||
                string.CompareOrdinal(
                    itemTime,
                    oldest
                ) < 0
            )
            {
                oldest = itemTime;
            }
        }

        oldestExecutionTime = oldest;
    }


    private string NormalizeRawTime(
        string rawTime
    )
    {
        if (string.IsNullOrWhiteSpace(rawTime))
            return "";

        string numbersOnly =
            rawTime
            .Replace(":", "")
            .Trim();

        if (numbersOnly.Length < 6)
            return "";

        return numbersOnly.Substring(0, 6);
    }


    // =========================================================
    // 최대 Row 제한
    // =========================================================

    private void RemoveExcessRowsFromBottom()
    {
        while (
            rows.Count >
            Mathf.Max(30, maxVisibleRows)
        )
        {
            int lastIndex =
                rows.Count - 1;

            ExecutionRowUI target =
                rows[lastIndex];

            if (
                target != null &&
                target.CurrentItem != null
            )
            {
                loadedExecutionKeys.Remove(
                    MakeExecutionKey(
                        target.CurrentItem
                    )
                );
            }

            rows.RemoveAt(lastIndex);

            if (target != null)
            {
                Destroy(
                    target.gameObject
                );
            }
        }

        UpdateOldestExecutionTime();
    }


    private void RemoveExcessRowsFromTop()
    {
        while (
            rows.Count >
            Mathf.Max(30, maxVisibleRows)
        )
        {
            ExecutionRowUI target =
                rows[0];

            if (
                target != null &&
                target.CurrentItem != null
            )
            {
                loadedExecutionKeys.Remove(
                    MakeExecutionKey(
                        target.CurrentItem
                    )
                );
            }

            rows.RemoveAt(0);

            if (target != null)
            {
                Destroy(
                    target.gameObject
                );
            }
        }

        UpdateOldestExecutionTime();
    }


    // =========================================================
    // 종료 및 초기화
    // =========================================================

    private void OnDisable()
    {
        StopExecutions();
    }


    public async void StopExecutions()
    {
        requestGeneration++;

        currentSymbol = "";
        oldestExecutionTime = "";

        hasMore = false;
        isLoading = false;

        if (loadingRoutine != null)
        {
            StopCoroutine(
                loadingRoutine
            );

            loadingRoutine = null;
        }

        SetLoadingVisible(false);
        SetEmptyVisible(false);

        /*
         * 체결 탭을 닫으면 Unity에서 보관 중이던
         * 체결 목록도 전부 초기화한다.
         */
        ClearRows();

        if (
            executionSocket == null ||
            isClosing
        )
        {
            return;
        }

        isClosing = true;

        WebSocket socketToClose =
            executionSocket;

        executionSocket = null;

        try
        {
            await socketToClose.Close();

            Debug.Log(
                "체결 WebSocket 수동 종료"
            );
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "체결 WebSocket 종료 오류: " +
                error.Message
            );
        }
        finally
        {
            isClosing = false;
        }
    }


    private void ClearRows()
    {
        foreach (
            ExecutionRowUI row
            in rows
        )
        {
            if (row != null)
            {
                Destroy(
                    row.gameObject
                );
            }
        }

        rows.Clear();
        loadedExecutionKeys.Clear();

        oldestExecutionTime = "";

        if (content != null)
        {
            content.anchoredPosition =
                Vector2.zero;
        }

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition =
                1f;
        }
    }


    private void SetLoadingVisible(bool visible)
    {
        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(
                visible
            );
        }
    }


    private void SetEmptyVisible(bool visible)
    {
        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(
                visible
            );
        }
    }
}


// =========================================================
// JSON 모델
// =========================================================

[Serializable]
public class ExecutionItem
{
    public string id;
    public string symbol;
    public string time;

    public long price;
    public string side;
    public long quantity;

    public long cumulative_volume;
}


[Serializable]
public class ExecutionHistoryResponse
{
    public string symbol;
    public bool has_more;

    public string next_cursor;

    public List<ExecutionItem> items;
}


[Serializable]
public class ExecutionSocketMessage
{
    public string type;
    public string symbol;
    public ExecutionItem item;
}

