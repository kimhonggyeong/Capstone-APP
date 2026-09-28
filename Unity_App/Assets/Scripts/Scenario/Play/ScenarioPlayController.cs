using System.Collections;
using TMPro;
using UnityEngine;

public class ScenarioPlayController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField]
    private ScenarioPanelManager panelManager;

    [Header("스크롤 위치 초기화")]
    [Tooltip("시나리오 패널 ScrollRect의 Content를 연결")]
    [SerializeField]
    private RectTransform scenarioContent;

    [Header("로딩")]
    [SerializeField]
    private GameObject loadingPanel;

    [SerializeField]
    [Min(0f)]
    private float minimumEntryLoadingSeconds = 0.5f;

    [Header("Stock List")]
    [Tooltip("Stock_Scroll/Viewport/Content를 연결")]
    [SerializeField]
    private Transform stockContent;

    [Tooltip("StockButtonItem이 붙은 주식 버튼 프리팹")]
    [SerializeField]
    private StockButtonItem stockButtonPrefab;

    [Header("Optional Status")]
    [SerializeField]
    private TMP_Text statusText;

    [Header("주식 상세정보")]
    [SerializeField]
    private ScenarioStockInfoView stockInfoView;

    [Header("시나리오 차트")]
    [SerializeField]
    private ScenarioCandlestickLoader scenarioChart;

    [Header("주문 UI")]
    [SerializeField]
    private ScenarioOrderController orderController;

    [Header("현재 턴 표시")]
    [SerializeField]
    private TMP_Text turnProgressText;

    [Header("보유주식 UI")]
    [SerializeField]
    private ScenarioHoldingListController holdingListController;

    private ScenarioRuntimeData currentScenario;
    private bool startingScenario;

    /// <summary>
    /// ScenarioInfoPanel의 시작 버튼 OnClick에 연결합니다.
    /// </summary>
    public void StartSelectedScenario()
    {
        if (startingScenario)
            return;

        ScenarioDataManager dataManager =
            ScenarioDataManager.Instance;

        if (dataManager == null)
        {
            Debug.LogError(
                "[ScenarioPlayController] ScenarioDataManager가 없습니다."
            );

            return;
        }

        ScenarioRuntimeData scenario =
            dataManager.SelectedScenario;

        if (scenario == null)
        {
            Debug.LogError(
                "[ScenarioPlayController] 선택된 시나리오가 없습니다."
            );

            return;
        }

        if (scenario.turnData == null)
        {
            Debug.LogError(
                "[ScenarioPlayController] 시나리오 턴 데이터가 없습니다."
            );

            return;
        }

        StartCoroutine(StartScenarioRoutine(scenario));
    }

    private IEnumerator StartScenarioRoutine(ScenarioRuntimeData scenario)
    {
        startingScenario = true;
        SetEntryLoading(true);

        if (minimumEntryLoadingSeconds > 0f)
            yield return new WaitForSecondsRealtime(minimumEntryLoadingSeconds);

        panelManager.ShowScenarioPlay();
        Initialize(scenario);

        SetEntryLoading(false);
        startingScenario = false;
    }

    private void SetEntryLoading(bool value)
    {
        if (loadingPanel == null)
            return;

        loadingPanel.SetActive(value);
        if (value)
            loadingPanel.transform.SetAsLastSibling();
    }

    public void Initialize(
        ScenarioRuntimeData scenario)
    {
        currentScenario = scenario;

        ResetContentY(scenarioContent);

        ClearStockButtons();
        CreateStockButtons();

        AssetInfo[] assets = scenario.turnData.assets;

        if (assets != null && assets.Length > 0)
        {
            AssetInfo defaultAsset = System.Array.Find(
                assets,
                asset => asset != null &&
                         asset.asset_id == scenario.turnData.default_asset_id
            );

            if (defaultAsset == null)
                defaultAsset = System.Array.Find(assets, asset => asset != null);

            if (defaultAsset != null)
                OnStockButtonClicked(defaultAsset);
        }

        if (statusText != null)

        {
            statusText.text =
                $"{scenario.Title} · " +
                $"{scenario.CurrentTurn}/{scenario.TotalTurns}턴";
        }

        Debug.Log(
            $"[ScenarioPlayController] 시나리오 시작\n" +
            $"이름: {scenario.Title}\n" +
            $"세션: {scenario.session.session_id}\n" +
            $"현재 턴: {scenario.CurrentTurn}\n" +
            $"종목 수: {scenario.AssetCount}"
        );

        if (turnProgressText != null &&
    scenario.turnData?.progress != null)
        {
            turnProgressText.text =
                $"TURN {scenario.turnData.progress.current_turn} / " +
                $"{scenario.turnData.progress.total_turns}";
        }

        if (holdingListController != null)
        {
            holdingListController.Refresh(scenario);
        }
        else
        {
            Debug.LogError(
                "[ScenarioPlayController] Holding List Controller를 연결하세요.",
                this
            );
        }
    }

    private static void ResetContentY(RectTransform content)
    {
        if (content == null)
            return;

        Canvas.ForceUpdateCanvases();
        Vector2 position = content.anchoredPosition;
        position.y = 0f;
        content.anchoredPosition = position;
    }

    private void CreateStockButtons()
    {
        AssetInfo[] assets = currentScenario.turnData.assets;

        if (assets == null)
            return;

        foreach (AssetInfo asset in assets)
        {
            if (asset == null)
                continue;

            StockButtonItem item =
                Instantiate(stockButtonPrefab, stockContent);

            // 이 연결로 버튼 클릭이 아래 함수까지 전달됩니다.
            item.Initialize(asset, OnStockButtonClicked);
        }
    }

    private void OnStockButtonClicked(AssetInfo asset)
    {
        if (asset == null || currentScenario?.turnData == null)
            return;

        Debug.Log($"[종목 화면 갱신] {asset.name}", this);

        if (stockInfoView != null)
        {
            stockInfoView.SetData(
                asset,
                currentScenario.turnData
            );
        }
        else
        {
            Debug.LogError(
                "ScenarioPlayController의 Stock Info View를 연결하세요.",
                this
            );
        }

        if (scenarioChart != null)
        {
            scenarioChart.LoadStock(
                currentScenario,
                asset
            );
        }
        else
        {
            Debug.LogError(
                "ScenarioPlayController의 Scenario Chart를 연결하세요.",
                this
            );
        }

        if (orderController != null)
        {
            orderController.SetStock(currentScenario, asset);
        }
    }

    private void ClearStockButtons()
    {
        if (stockContent == null)
            return;

        foreach (Transform child in stockContent)
        {
            Destroy(child.gameObject);
        }
    }
}
