using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public enum StockRankType
{
    Volume,
    ChangeRate,
    MarketCap,
    ExecutionStrength
}

public class StockSearchListController : MonoBehaviour
{

    [Header("검색")]
    public TMP_InputField searchInput;
    public Button searchButton;

    [Tooltip("검색 결과 최대 개수. 순위 목록 개수와 별개이며 서버는 최대 100개를 반환합니다.")]
    [Range(1, 100)]
    public int searchResultLimit = 100;

    [Header("순위 전환 버튼")]
    public Button volumeRankButton;
    public Button changeRateRankButton;
    public Button marketCapRankButton;
    public Button executionStrengthRankButton;

    [Header("순위 버튼 선택 이미지")]
    [Tooltip("거래량 순위 버튼의 자식 Amount_Button_Image")]
    public GameObject volumeRankButtonImage;
    [Tooltip("등락률 순위 버튼의 자식 Volume_Button_Image")]
    public GameObject changeRateRankButtonImage;
    [Tooltip("시가총액 순위 버튼의 자식 Up_Image")]
    public GameObject marketCapRankButtonImage;
    [Tooltip("체결강도 순위 버튼의 자식 Down_Button_Image")]
    public GameObject executionStrengthRankButtonImage;

    [Header("순위 버튼 텍스트")]
    public TMP_Text volumeRankButtonText;
    public TMP_Text changeRateRankButtonText;
    public TMP_Text marketCapRankButtonText;
    public TMP_Text executionStrengthRankButtonText;

    [Header("목록")]
    public Transform content;

    [Header("연결")]
    public StockCandlestickLoader stockLoader;
    public StockPanelManager panelManager;

    [Header("순위 설정")]
    [Range(1, 30)]
    public int topStockLimit = 30;

    private readonly List<StockListItemButton> itemPool =
        new List<StockListItemButton>();

    private StockRankType currentRankType =
        StockRankType.Volume;

    private Coroutine currentRequestCoroutine;
    private UnityWebRequest activePriceRequest;
    private int requestGeneration;


    private void Awake()
    {
        itemPool.Clear();

        foreach (Transform child in content)
        {
            StockListItemButton item =
                child.GetComponent<StockListItemButton>();

            if (item == null)
                continue;

            itemPool.Add(item);
            item.gameObject.SetActive(false);
        }
    }


    private void Start()
    {
        if (searchButton != null)
            searchButton.onClick.AddListener(OnClickSearch);

        if (volumeRankButton != null)
            volumeRankButton.onClick.AddListener(OnClickVolumeRank);

        if (changeRateRankButton != null)
            changeRateRankButton.onClick.AddListener(OnClickChangeRateRank);

        if (marketCapRankButton != null)
            marketCapRankButton.onClick.AddListener(OnClickMarketCapRank);

        if (executionStrengthRankButton != null)
        {
            executionStrengthRankButton.onClick.AddListener(
                OnClickExecutionStrengthRank
            );
        }

        LoadRank(StockRankType.Volume);
    }


    private void OnDestroy()
    {
        StopCurrentRequest();

        if (searchButton != null)
            searchButton.onClick.RemoveListener(OnClickSearch);

        if (volumeRankButton != null)
            volumeRankButton.onClick.RemoveListener(OnClickVolumeRank);

        if (changeRateRankButton != null)
        {
            changeRateRankButton.onClick.RemoveListener(
                OnClickChangeRateRank
            );
        }

        if (marketCapRankButton != null)
            marketCapRankButton.onClick.RemoveListener(OnClickMarketCapRank);

        if (executionStrengthRankButton != null)
        {
            executionStrengthRankButton.onClick.RemoveListener(
                OnClickExecutionStrengthRank
            );
        }
    }


    public void OnClickVolumeRank()
    {
        ClearSearchInput();
        LoadRank(StockRankType.Volume);
    }


    public void OnClickChangeRateRank()
    {
        ClearSearchInput();
        LoadRank(StockRankType.ChangeRate);
    }


    public void OnClickMarketCapRank()
    {
        ClearSearchInput();
        LoadRank(StockRankType.MarketCap);
    }


    public void OnClickExecutionStrengthRank()
    {
        ClearSearchInput();
        LoadRank(StockRankType.ExecutionStrength);
    }


    private void ClearSearchInput()
    {
        if (searchInput != null)
            searchInput.text = string.Empty;
    }


    public void OnClickSearch()
    {
        string keyword = searchInput != null
            ? searchInput.text.Trim()
            : string.Empty;

        if (string.IsNullOrEmpty(keyword))
        {
            LoadRank(currentRankType);
            ClearRankButtonVisuals();
            return;
        }

        ClearRankButtonVisuals();
        StartNewRequest(SearchStocks(keyword));
    }


    private void LoadRank(StockRankType rankType)
    {
        currentRankType = rankType;
        UpdateRankButtonImages(rankType);

        StartNewRequest(
            LoadRankStocks(rankType)
        );
    }


    private void UpdateRankButtonImages(StockRankType selectedRankType)
    {
        SetActiveIfAssigned(
            volumeRankButtonImage,
            selectedRankType == StockRankType.Volume
        );
        SetActiveIfAssigned(
            changeRateRankButtonImage,
            selectedRankType == StockRankType.ChangeRate
        );
        SetActiveIfAssigned(
            marketCapRankButtonImage,
            selectedRankType == StockRankType.MarketCap
        );
        SetActiveIfAssigned(
            executionStrengthRankButtonImage,
            selectedRankType == StockRankType.ExecutionStrength
        );

        ColorUtility.TryParseHtmlString("#F66B24", out Color selectedColor);
        Color normalColor = Color.black;

        SetTextColorIfAssigned(
            volumeRankButtonText,
            selectedRankType == StockRankType.Volume,
            selectedColor,
            normalColor
        );
        SetTextColorIfAssigned(
            changeRateRankButtonText,
            selectedRankType == StockRankType.ChangeRate,
            selectedColor,
            normalColor
        );
        SetTextColorIfAssigned(
            marketCapRankButtonText,
            selectedRankType == StockRankType.MarketCap,
            selectedColor,
            normalColor
        );
        SetTextColorIfAssigned(
            executionStrengthRankButtonText,
            selectedRankType == StockRankType.ExecutionStrength,
            selectedColor,
            normalColor
        );
    }


    private static void SetActiveIfAssigned(GameObject target, bool value)
    {
        if (target != null)
            target.SetActive(value);
    }


    private static void SetTextColorIfAssigned(
        TMP_Text target,
        bool selected,
        Color selectedColor,
        Color normalColor
    )
    {
        if (target != null)
            target.color = selected ? selectedColor : normalColor;
    }


    private void ClearRankButtonVisuals()
    {
        SetActiveIfAssigned(volumeRankButtonImage, false);
        SetActiveIfAssigned(changeRateRankButtonImage, false);
        SetActiveIfAssigned(marketCapRankButtonImage, false);
        SetActiveIfAssigned(executionStrengthRankButtonImage, false);

        Color normalColor = Color.black;
        SetTextColorIfAssigned(volumeRankButtonText, false, normalColor, normalColor);
        SetTextColorIfAssigned(changeRateRankButtonText, false, normalColor, normalColor);
        SetTextColorIfAssigned(marketCapRankButtonText, false, normalColor, normalColor);
        SetTextColorIfAssigned(executionStrengthRankButtonText, false, normalColor, normalColor);
    }


    private void StartNewRequest(IEnumerator routine)
    {
        StopCurrentRequest();
        currentRequestCoroutine = StartCoroutine(routine);
    }

    private void StopCurrentRequest()
    {
        requestGeneration++;

        if (currentRequestCoroutine != null)
        {
            StopCoroutine(currentRequestCoroutine);
            currentRequestCoroutine = null;
        }

        if (activePriceRequest != null)
        {
            activePriceRequest.Abort();
            activePriceRequest.Dispose();
            activePriceRequest = null;
        }

    }


    private IEnumerator LoadRankStocks(StockRankType rankType)
    {
        int generation = requestGeneration;

        if (panelManager != null)
            panelManager.ShowLoading();

        int requestLimit =
            Mathf.Clamp(topStockLimit, 1, 30);

        string rankQuery = GetRankQuery(rankType);

        string url =
            $"{ServerConfig.HttpBaseUrl}/stocks/rank" +
            $"?type={rankQuery}" +
            $"&limit={requestLimit}";

        Debug.Log($"[주식 순위 요청] {url}");

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 30;
            yield return req.SendWebRequest();

            if (generation != requestGeneration)
                yield break;

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"주식 순위 요청 실패\n" +
                    $"종류: {rankType}\n" +
                    $"오류: {req.error}\n" +
                    $"HTTP: {req.responseCode}\n" +
                    $"응답: {req.downloadHandler.text}"
                );

                if (panelManager != null)
                    panelManager.HideLoading();

                currentRequestCoroutine = null;
                yield break;
            }

            StockRankResponse response;

            try
            {
                response =
                    JsonConvert.DeserializeObject<StockRankResponse>(
                        req.downloadHandler.text
                    );
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"주식 순위 JSON 변환 실패: {e.Message}\n" +
                    req.downloadHandler.text
                );

                if (panelManager != null)
                    panelManager.HideLoading();

                currentRequestCoroutine = null;
                yield break;
            }

            List<StockRankItem> items =
                response != null
                    ? response.items
                    : null;

            yield return DrawStockButtons(items, rankType, false, generation);

            if (generation != requestGeneration)
                yield break;

            if (panelManager != null)
            {
                yield return panelManager.ShowListAfterLoading();
            }
        }

        currentRequestCoroutine = null;
    }


    private IEnumerator SearchStocks(string keyword)
    {
        int generation = requestGeneration;

        // 검색은 전체 로딩창 없이 목록을 먼저 보여 준다.
        // 이후 가격 조회가 끝나도 패널을 전환하지 않아 종목 선택을 방해하지 않는다.
        if (panelManager != null)
            panelManager.ShowListWithoutLoading();

        foreach (StockListItemButton item in itemPool)
            item.gameObject.SetActive(false);

        string escapedKeyword = UnityWebRequest.EscapeURL(keyword);
        string url =
            $"{ServerConfig.HttpBaseUrl}/stocks/search" +
            $"?keyword={escapedKeyword}" +
            $"&limit={Mathf.Clamp(searchResultLimit, 1, 100)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 30;
            yield return req.SendWebRequest();

            if (generation != requestGeneration)
                yield break;

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"종목 검색 실패\n오류: {req.error}\n" +
                    $"HTTP: {req.responseCode}\n응답: {req.downloadHandler.text}"
                );
                currentRequestCoroutine = null;
                yield break;
            }

            StockRankResponse response;
            try
            {
                response = JsonConvert.DeserializeObject<StockRankResponse>(
                    req.downloadHandler.text
                );
            }
            catch (Exception error)
            {
                Debug.LogError($"검색 결과 JSON 변환 실패: {error.Message}");
                currentRequestCoroutine = null;
                yield break;
            }

            List<StockRankItem> items = response != null ? response.items : null;
            yield return DrawStockButtons(items, currentRankType, true, generation);

            if (generation != requestGeneration)
                yield break;

            RectTransform contentRect = content as RectTransform;
            if (contentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Canvas.ForceUpdateCanvases();

            // 종목명은 이미 표시된 상태이며 현재가/등락률만 하나씩 갱신한다.
            yield return LoadSearchPrices(items, generation);
        }

        if (generation == requestGeneration)
            currentRequestCoroutine = null;
    }


    private void EnsureItemPoolCapacity(int requiredCount)
    {
        if (content == null || itemPool.Count == 0)
            return;

        StockListItemButton template = itemPool[0];
        while (itemPool.Count < requiredCount)
        {
            StockListItemButton item = Instantiate(template, content);
            item.gameObject.SetActive(false);
            itemPool.Add(item);
        }
    }


    private IEnumerator DrawStockButtons(
        List<StockRankItem> items,
        StockRankType rankType,
        bool isSearchResult,
        int generation
    )
    {
        if (generation != requestGeneration)
            yield break;

        EnsureItemPoolCapacity(items != null ? items.Count : 0);

        for (int i = 0; i < itemPool.Count; i++)
        {
            itemPool[i].gameObject.SetActive(false);
        }

        yield return null;

        if (generation != requestGeneration)
            yield break;

        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("표시할 종목이 없습니다.");

            if (panelManager != null)
                panelManager.HideLoading();

            yield break;
        }

        int count =
            Mathf.Min(items.Count, itemPool.Count);

        for (int i = 0; i < count; i++)
        {
            if (generation != requestGeneration)
                yield break;

            StockRankItem data = items[i];
            StockListItemButton itemButton = itemPool[i];

            int displayRank = data.rank > 0
                ? data.rank
                : i + 1;

            itemButton.SetData(
                rank: displayRank,
                data: data,
                rankType: rankType,
                isSearchResult: isSearchResult,
                loader: stockLoader,
                panelManager: panelManager
            );

            itemButton.gameObject.SetActive(true);

            if (i > 0 && i % 5 == 0)
                yield return null;
        }
    }


    private IEnumerator LoadSearchPrices(List<StockRankItem> items, int generation)
    {
        if (items == null)
            yield break;

        int count = Mathf.Min(items.Count, itemPool.Count);

        // 화면에 표시한 검색 결과만 차례로 조회해 시세 API 요청이 몰리지 않게 합니다.
        for (int i = 0; i < count; i++)
        {
            if (generation != requestGeneration)
                yield break;

            StockRankItem data = items[i];
            StockListItemButton itemButton = itemPool[i];
            if (data == null || itemButton == null || string.IsNullOrWhiteSpace(data.symbol))
            {
                if (itemButton != null)
                    itemButton.ShowPriceUnavailable();
                continue;
            }

            string url = ServerConfig.HttpBaseUrl + "/stock-info?symbol=" +
                         UnityWebRequest.EscapeURL(data.symbol);
            UnityWebRequest request = UnityWebRequest.Get(url);
            activePriceRequest = request;

            try
            {
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (generation != requestGeneration)
                    yield break;

                StockInfoResponse quote = null;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        quote = JsonConvert.DeserializeObject<StockInfoResponse>(
                            request.downloadHandler.text
                        );
                    }
                    catch (Exception error)
                    {
                        Debug.LogWarning($"[검색 현재가] {data.symbol} 응답 변환 실패: {error.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[검색 현재가] {data.symbol} 조회 실패: {request.error}");
                }

                bool hasSearchQuote =
                    quote != null && quote.symbol == data.symbol && quote.price > 0;

                if (hasSearchQuote)
                {
                    data.price = quote.price;
                    data.change_rate = (float)quote.change_rate;
                }

                if (hasSearchQuote)
                {
                    itemButton.SetData(
                        rank: data.rank > 0 ? data.rank : i + 1,
                        data: data,
                        rankType: currentRankType,
                        isSearchResult: true,
                        loader: stockLoader,
                        panelManager: panelManager,
                        hasSearchQuote: true
                    );
                }
                else
                {
                    itemButton.ShowPriceUnavailable();
                }
            }
            finally
            {
                if (activePriceRequest == request)
                {
                    activePriceRequest = null;
                    request.Dispose();
                }
            }
        }
    }

    private string GetRankQuery(StockRankType rankType)
    {
        switch (rankType)
        {
            case StockRankType.ChangeRate:
                return "change-rate";

            case StockRankType.MarketCap:
                return "market-cap";

            case StockRankType.ExecutionStrength:
                return "execution-strength";

            case StockRankType.Volume:
            default:
                return "volume";
        }
    }
}


[Serializable]
public class StockRankResponse
{
    public string source;
    public string ranking;
    public bool realtime;
    public string as_of;
    public List<StockRankItem> items;
}


[Serializable]
public class StockRankItem
{
    public string symbol;
    public string name;

    public long price;
    public float change_rate;

    public long volume;
    public long trade_value;
    public long market_cap;

    public float execution_strength;

    public int rank;
}

