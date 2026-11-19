using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ScenarioListController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField]
    private ScenarioDataManager dataManager;

    [Header("Panels")]
    [SerializeField]
    private ScenarioPanelManager panelManager;

    [Header("List")]
    [SerializeField]
    private Transform content;

    [SerializeField]
    private ScenarioButtonItem buttonPrefab;

    [SerializeField]
    [Tooltip("전체 시나리오 제목 옆에 표시할 개수 텍스트")]
    private TMP_Text scenarioCountText;

    [Header("State")]
    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private GameObject loadingObject;

    private ScenarioLoadingPanel sharedLoadingPanel;

    private void OnEnable()
    {
        if (!ResolveDataManager())
            return;

        dataManager.OnScenariosLoaded += BuildList;
        dataManager.OnError += ShowError;
    }

    private void OnDisable()
    {
        if (dataManager == null)
            return;

        dataManager.OnScenariosLoaded -= BuildList;
        dataManager.OnError -= ShowError;
    }

    private void Start()
    {
        panelManager.ShowScenarioList();
        LoadScenarios();
    }

    public void LoadScenarios()
    {
        ClearList();
        SetScenarioCount(0);

        SetStatusText("시나리오를 불러오는 중...");

        SetLoadingVisible(true);

        if (!ResolveDataManager())
        {
            ShowError("ScenarioDataManager를 찾을 수 없습니다.");
            return;
        }

        dataManager.LoadAllScenarios();
    }

    private void BuildList(
        IReadOnlyList<ScenarioRuntimeData> scenarios)
    {
        ClearList();

        foreach (ScenarioRuntimeData scenario in scenarios)
        {
            ScenarioButtonItem buttonItem =
                Instantiate(buttonPrefab, content);

            buttonItem.Initialize(
                scenario,
                OnScenarioButtonClicked
            );
        }

        SetScenarioCount(scenarios.Count);

        SetLoadingVisible(false);

        SetStatusText(
            scenarios.Count == 0
                ? "등록된 시나리오가 없습니다."
                : ""
        );
    }

    private void OnScenarioButtonClicked(
        ScenarioRuntimeData scenario)
    {
        if (!ResolveDataManager())
        {
            ShowError("ScenarioDataManager를 찾을 수 없습니다.");
            return;
        }

        // 선택 데이터 저장
        dataManager.SelectScenario(scenario);

        // 패널 매니저에게 화면 전환 요청
        panelManager.ShowScenarioInfo(scenario);
    }

    private void ShowError(string message)
    {
        SetLoadingVisible(false);
        SetScenarioCount(0);

        SetStatusText($"불러오기 실패\n{message}");
    }

    private void ClearList()
    {
        foreach (Transform child in content)
            Destroy(child.gameObject);
    }

    private bool ResolveDataManager()
    {
        // 씬 재진입 시 씬에 직렬화된 복제 관리자는 Awake에서 제거됩니다.
        // 항상 DontDestroyOnLoad로 유지 중인 실제 싱글턴을 우선 사용합니다.
        if (ScenarioDataManager.Instance != null)
            dataManager = ScenarioDataManager.Instance;

        return dataManager != null;
    }

    private void SetLoadingVisible(bool value)
    {
        if (loadingObject == null)
            return;

        if (sharedLoadingPanel == null)
        {
            sharedLoadingPanel =
                loadingObject.GetComponent<ScenarioLoadingPanel>();

            if (sharedLoadingPanel == null)
            {
                sharedLoadingPanel =
                    loadingObject.AddComponent<ScenarioLoadingPanel>();
            }
        }

        if (value)
            sharedLoadingPanel.Show(this);
        else
            sharedLoadingPanel.Hide(this);
    }

    private void SetStatusText(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void SetScenarioCount(int count)
    {
        if (scenarioCountText != null)
            scenarioCountText.text = count + "개";
    }
}
