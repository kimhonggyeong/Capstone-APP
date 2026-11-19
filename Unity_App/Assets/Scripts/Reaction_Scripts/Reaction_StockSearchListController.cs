using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class Reaction_StockSearchListController : MonoBehaviour
{

    public TMP_InputField searchInput;
    public Button searchButton;
    public Transform content;

    public Reaction_StockCandlestickLoader stockLoader;
    public Reaction_PanelManager panelManager;

    public Reaction_AIChatController chatController;

    private List<Reaction_StockListItemButton> itemPool = new List<Reaction_StockListItemButton>();

    private void Awake()
    {
        itemPool.Clear();

        foreach (Transform child in content)
        {
            Reaction_StockListItemButton item =
                child.GetComponent<Reaction_StockListItemButton>();

            if (item != null)
            {
                itemPool.Add(item);
                item.gameObject.SetActive(false);
            }
        }
    }

    private void Start()
    {
        searchButton.onClick.AddListener(OnClickSearch);
        StartCoroutine(LoadTopVolumeStocks());
    }

    public void OnClickSearch()
    {
        string keyword = searchInput.text.Trim();

        if (string.IsNullOrEmpty(keyword))
            StartCoroutine(LoadTopVolumeStocks());
        else
            StartCoroutine(SearchStocks(keyword));
    }

    private IEnumerator LoadTopVolumeStocks()
    {
        panelManager.ShowLoadingOnly();

        string url = $"{ServerConfig.HttpBaseUrl}/stocks/top-volume";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(req.error);
                panelManager.ShowList();
                yield break;
            }

            StockRankResponse response =
                JsonConvert.DeserializeObject<StockRankResponse>(req.downloadHandler.text);

            yield return StartCoroutine(DrawStockButtons(response.items));
            yield return StartCoroutine(panelManager.ShowListAfterLoading());
        }
    }

    private IEnumerator SearchStocks(string keyword)
    {
        panelManager.ShowLoadingOnly();

        string url = $"{ServerConfig.HttpBaseUrl}/stocks/search?keyword={UnityWebRequest.EscapeURL(keyword)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(req.error);
                panelManager.ShowList();
                yield break;
            }

            StockRankResponse response =
                JsonConvert.DeserializeObject<StockRankResponse>(req.downloadHandler.text);

            yield return StartCoroutine(DrawStockButtons(response.items));
            yield return StartCoroutine(panelManager.ShowListAfterLoading());
        }
    }

    private IEnumerator DrawStockButtons(List<StockRankItem> items)
    {
        for (int i = 0; i < itemPool.Count; i++)
        {
            itemPool[i].gameObject.SetActive(false);
        }

        yield return null;

        if (items == null)
        {
            panelManager.ShowList();
            yield break;
        }

        int count = Mathf.Min(items.Count, itemPool.Count);

        for (int i = 0; i < count; i++)
        {
            StockRankItem data = items[i];
            Reaction_StockListItemButton itemButton = itemPool[i];

            itemButton.SetData(
                i + 1,
                data.symbol,
                data.name,
                data.volume,
                data.trade_value,
                stockLoader,
                panelManager,
                chatController
            );

            itemButton.gameObject.SetActive(true);

            if (i > 0 && i % 5 == 0)
                yield return null;
        }
    }
}

