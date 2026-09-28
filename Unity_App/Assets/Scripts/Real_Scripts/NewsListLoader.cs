using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class NewsListLoader : MonoBehaviour
{

    [Header("뉴스 목록")]
    public ScrollRect scrollRect;
    public RectTransform content;
    public NewsRowUI rowPrefab;

    [Header("페이징")]
    [Range(1, 100)]
    public int pageSize = 30;

    [Tooltip("스크롤이 이 값 이하가 되면 다음 뉴스 요청")]
    [Range(0f, 0.2f)]
    public float bottomThreshold = 0.05f;

    [Tooltip("한 화면에서 유지할 최대 뉴스 개수")]
    public int maxVisibleRows = 300;

    [Header("상태 표시 - 선택")]
    public TMP_Text loadingText;
    public TMP_Text emptyText;
    public TMP_Text errorText;

    private readonly List<NewsRowUI> rows =
        new List<NewsRowUI>();

    private readonly HashSet<string> loadedNewsIds =
        new HashSet<string>();

    private Coroutine loadingRoutine;

    private string currentSymbol = "";

    private int nextStart = 1;
    private int requestGeneration;

    private bool isLoading;
    private bool hasMore;


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
        SetError("");
    }


    private void OnDestroy()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(
                OnScrollValueChanged
            );
        }
    }


    // =========================================================
    // 뉴스 화면 열기
    // =========================================================

    public void ShowNews(
        string symbol
    )
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            Debug.LogWarning(
                "뉴스 조회 종목코드가 없습니다."
            );
            return;
        }

        StopNews();

        requestGeneration++;

        currentSymbol =
            symbol.Trim();

        nextStart = 1;
        hasMore = true;
        isLoading = false;

        ClearRows();

        SetError("");
        SetEmptyVisible(false);

        int generation =
            requestGeneration;

        loadingRoutine = StartCoroutine(
            LoadNews(
                symbol: currentSymbol,
                start: 1,
                generation: generation
            )
        );
    }


    // =========================================================
    // 뉴스 요청
    // =========================================================

    private IEnumerator LoadNews(
        string symbol,
        int start,
        int generation
    )
    {
        if (
            isLoading ||
            string.IsNullOrWhiteSpace(symbol) ||
            !hasMore
        )
        {
            yield break;
        }

        isLoading = true;
        SetLoadingVisible(true);
        SetError("");

        int requestDisplay =
            Mathf.Clamp(
                pageSize,
                1,
                100
            );

        int requestStart =
            Mathf.Clamp(
                start,
                1,
                1000
            );

        string url =
            $"{ServerConfig.HttpBaseUrl}/stock/news" +
            $"?symbol=" +
            UnityWebRequest.EscapeURL(symbol) +
            $"&start={requestStart}" +
            $"&display={requestDisplay}";

        Debug.Log(
            "[종목 뉴스 요청] " +
            url
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
                string responseText =
                    request.downloadHandler != null
                        ? request.downloadHandler.text
                        : "";

                Debug.LogError(
                    "종목 뉴스 요청 실패\n" +
                    $"HTTP: {request.responseCode}\n" +
                    $"오류: {request.error}\n" +
                    $"응답: {responseText}"
                );

                SetError(
                    "뉴스를 불러오지 못했습니다."
                );

                FinishLoading();
                yield break;
            }

            NewsResponse response;

            try
            {
                response =
                    JsonConvert.DeserializeObject
                    <NewsResponse>(
                        request.downloadHandler.text
                    );
            }
            catch (Exception error)
            {
                Debug.LogError(
                    "뉴스 JSON 파싱 실패: " +
                    error.Message
                );

                SetError(
                    "뉴스 데이터 형식이 올바르지 않습니다."
                );

                FinishLoading();
                yield break;
            }

            if (response == null)
            {
                SetError(
                    "뉴스 응답이 비어 있습니다."
                );

                FinishLoading();
                yield break;
            }

            hasMore =
                response.has_more;

            if (response.next_start > 0)
            {
                nextStart =
                    response.next_start;
            }
            else
            {
                nextStart =
                    requestStart +
                    requestDisplay;
            }

            if (response.items != null)
            {
                foreach (
                    NewsItem item
                    in response.items
                )
                {
                    AddNewsToBottom(
                        item
                    );
                }
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
    // 스크롤 페이징
    // =========================================================

    private void OnScrollValueChanged(
        Vector2 position
    )
    {
        if (
            scrollRect == null ||
            isLoading ||
            !hasMore ||
            string.IsNullOrEmpty(
                currentSymbol
            )
        )
        {
            return;
        }

        /*
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

        if (nextStart > 1000)
        {
            hasMore = false;
            return;
        }

        int generation =
            requestGeneration;

        loadingRoutine = StartCoroutine(
            LoadNews(
                symbol: currentSymbol,
                start: nextStart,
                generation: generation
            )
        );
    }


    // =========================================================
    // 프리팹 생성
    // =========================================================

    private void AddNewsToBottom(
        NewsItem item
    )
    {
        if (!IsValidNewItem(item))
            return;

        NewsRowUI row =
            Instantiate(
                rowPrefab,
                content
            );

        row.SetData(item);
        row.transform.SetAsLastSibling();

        rows.Add(row);

        loadedNewsIds.Add(
            MakeNewsKey(item)
        );

        RemoveExcessRowsFromTop();

        SetEmptyVisible(false);
    }


    private bool IsValidNewItem(
        NewsItem item
    )
    {
        if (
            item == null ||
            string.IsNullOrWhiteSpace(
                item.description
            ) ||
            string.IsNullOrWhiteSpace(
                item.link
            )
        )
        {
            return false;
        }

        string key =
            MakeNewsKey(item);

        return !loadedNewsIds.Contains(
            key
        );
    }


    private string MakeNewsKey(
        NewsItem item
    )
    {
        if (item == null)
            return "";

        if (
            !string.IsNullOrWhiteSpace(
                item.id
            )
        )
        {
            return item.id;
        }

        if (
            !string.IsNullOrWhiteSpace(
                item.link
            )
        )
        {
            return item.link;
        }

        return
            $"{item.source}|" +
            $"{item.published_at}|" +
            $"{item.description}";
    }


    private void RemoveExcessRowsFromTop()
    {
        int maximum =
            Mathf.Max(
                pageSize,
                maxVisibleRows
            );

        while (rows.Count > maximum)
        {
            NewsRowUI target =
                rows[0];

            if (
                target != null &&
                target.CurrentItem != null
            )
            {
                loadedNewsIds.Remove(
                    MakeNewsKey(
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
    }


    // =========================================================
    // 종료 및 초기화
    // =========================================================

    public void StopNews()
    {
        requestGeneration++;

        currentSymbol = "";

        nextStart = 1;
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
        SetError("");

        ClearRows();
    }


    private void OnDisable()
    {
        StopNews();
    }


    private void ClearRows()
    {
        foreach (
            NewsRowUI row
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
        loadedNewsIds.Clear();

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


    private void SetLoadingVisible(
        bool visible
    )
    {
        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(
                visible
            );
        }
    }


    private void SetEmptyVisible(
        bool visible
    )
    {
        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(
                visible
            );
        }
    }


    private void SetError(
        string message
    )
    {
        if (errorText == null)
            return;

        errorText.text =
            message ?? "";

        errorText.gameObject.SetActive(
            !string.IsNullOrWhiteSpace(
                message
            )
        );
    }
}


// =========================================================
// JSON 모델
// =========================================================

[System.Serializable]
public class NewsItem
{
    public string id;
    public string source;
    public string published_at;
    public string description;
    public string link;
}


[Serializable]
public class NewsResponse
{
    public string symbol;
    public string name;

    public int total;
    public int start;
    public int display;

    public int next_start;
    public bool has_more;

    public List<NewsItem> items;
}

