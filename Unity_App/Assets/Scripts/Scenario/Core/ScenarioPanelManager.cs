using UnityEngine;

public class ScenarioPanelManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField]
    private GameObject scenarioListPanel;

    [SerializeField]
    private GameObject scenarioInfoPanel;

    [SerializeField]
    private GameObject scenarioPlayPanel;

    [Header("Views")]
    [SerializeField]
    private ScenarioInfoPanel scenarioInfoView;

    private void Awake()
    {
        ShowScenarioList();
    }

    public void ShowScenarioList()
    {
        scenarioListPanel.SetActive(true);
        scenarioInfoPanel.SetActive(false);
        scenarioPlayPanel.SetActive(false);
    }

    public void ShowScenarioInfo(
        ScenarioRuntimeData scenario)
    {
        if (scenario == null)
        {
            Debug.LogError(
                "[ScenarioPanelManager] 시나리오 데이터가 없습니다."
            );

            return;
        }

        scenarioInfoView.SetData(scenario);

        scenarioListPanel.SetActive(false);
        scenarioInfoPanel.SetActive(true);
        scenarioPlayPanel.SetActive(false);
    }

    public void ShowScenarioPlay()
    {
        scenarioListPanel.SetActive(false);
        scenarioInfoPanel.SetActive(false);
        scenarioPlayPanel.SetActive(true);
    }

    public void BackToScenarioInfo()
    {
        ScenarioRuntimeData selected =
            ScenarioDataManager.Instance.SelectedScenario;

        if (selected == null)
        {
            ShowScenarioList();
            return;
        }

        ShowScenarioInfo(selected);
    }
}