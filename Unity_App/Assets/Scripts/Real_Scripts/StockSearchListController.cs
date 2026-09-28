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
        if (currentRequestCoroutine != null)
        {
            StopCoroutine(currentRequestCoroutine);
            currentRequestCoroutine = null;
        }

        currentRequestCoroutine = StartCoroutine(routine);
    }


    private IEnumerator LoadRankStocks(StockRankType rankType)
    {
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
            yield return req.SendWebRequest();

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

            yield return StartCoroutine(
                DrawStockButtons(
                    items,
                    rankType,
                    false
                )
            );

            if (panelManager != null)
            {
                yield return StartCoroutine(
                    panelManager.ShowListAfterLoading()
                );
            }
        }

        currentRequestCoroutine = null;
    }


    private IEnumerator SearchStocks(string keyword)
    {
        if (panelManager != null)
            panelManager.ShowLoading();

        string escapedKeyword =
            UnityWebRequest.EscapeURL(keyword);

        string url =
            $"{ServerConfig.HttpBaseUrl}/stocks/search" +
            $"?keyword={escapedKeyword}" +
            $"&limit={Mathf.Clamp(topStockLimit, 1, 100)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"종목 검색 실패\n" +
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
                    $"검색 결과 JSON 변환 실패: {e.Message}"
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

            yield return StartCoroutine(
                DrawStockButtons(
                    items,
                    currentRankType,
                    true
                )
            );

            if (panelManager != null)
            {
                yield return StartCoroutine(
                    panelManager.ShowListAfterLoading()
                );
            }
        }

        currentRequestCoroutine = null;
    }


    private IEnumerator DrawStockButtons(
        List<StockRankItem> items,
        StockRankType rankType,
        bool isSearchResult
    )
    {
        for (int i = 0; i < itemPool.Count; i++)
        {
            itemPool[i].gameObject.SetActive(false);
        }

        yield return null;

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

