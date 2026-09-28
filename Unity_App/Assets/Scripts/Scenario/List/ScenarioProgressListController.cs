using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class ScenarioProgressListController : MonoBehaviour
{

    [Header("Controllers")]
    [SerializeField] private ScenarioDataManager dataManager;
    [SerializeField] private ScenarioPanelManager panelManager;
    [SerializeField] private ScenarioPlayController playController;
    [SerializeField] private ScenarioFinalResultController finalResultController;

    [Header("Lists")]
    [SerializeField] private Transform activeContent;
    [SerializeField] private ScenarioProgressItem activePrefab;
    [SerializeField] private TMP_Text activeCountText;
    [SerializeField] private GameObject activeEmptyObject;
    [SerializeField] private Transform completedContent;
    [SerializeField] private ScenarioProgressItem completedPrefab;
    [SerializeField] private TMP_Text completedCountText;
    [SerializeField] private GameObject completedEmptyObject;

    [Header("State")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingObject;

    private bool loading;
    private ScenarioLoadingPanel sharedLoadingPanel;

    private void OnEnable()
    {
        ResolveDataManager();
        LoadProgress();
    }

    public void LoadProgress()
    {
        if (!loading)
            StartCoroutine(LoadProgressRoutine());
    }

    private IEnumerator LoadProgressRoutine()
    {
        loading = true;
        SetLoading(true, "진행 상황을 불러오는 중...");
        ClearChildren(activeContent);
        ClearChildren(completedContent);

        string userId = dataManager != null
            ? dataManager.CurrentUserId
            : "UNITY-USER";
        string url = ServerConfig.HttpBaseUrl + "/api/users/" +
            UnityWebRequest.EscapeURL(userId) + "/sessions";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 60;
            request.SetRequestHeader("Accept", "application/json");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetLoading(false, "진행 상황을 불러오지 못했습니다.");
                string responseBody = request.downloadHandler != null
                    ? request.downloadHandler.text
                    : "";
                Debug.LogError(
                    "[Scenario Progress] 요청 실패\n" +
                    "Status Code: " + request.responseCode + "\n" +
                    "Result: " + request.result + "\n" +
                    "Error: " + request.error + "\n" +
                    "Response: " + responseBody
                );
                loading = false;
                yield break;
            }

            UserScenarioSessionsResponse response =
                JsonUtility.FromJson<UserScenarioSessionsResponse>(
                    request.downloadHandler.text);

            if (response == null || response.status != "ok" || response.data == null)
            {
                SetLoading(false, response?.message ?? "세션 응답이 올바르지 않습니다.");
                loading = false;
                yield break;
            }

            BuildLists(response.data);
        }

        SetLoading(false, "");
        loading = false;
    }

    private void BuildLists(UserScenarioSession[] sessions)
    {
        int activeCount = 0;
        int completedCount = 0;

        foreach (UserScenarioSession item in sessions)
        {
            if (item == null)
                continue;

            if (item.status == "COMPLETED")
            {
                if (completedContent != null && completedPrefab != null)
                    Instantiate(completedPrefab, completedContent)
                        .Initialize(item, OpenCompletedScenario);
                completedCount++;
            }
            else if (item.status == "ACTIVE")
            {
                if (activeContent != null && activePrefab != null)
                    Instantiate(activePrefab, activeContent)
                        .Initialize(item, ContinueSession);
                activeCount++;
            }
        }

        SetText(activeCountText, activeCount + "개");
        SetText(completedCountText, completedCount + "개");
        if (activeEmptyObject != null) activeEmptyObject.SetActive(activeCount == 0);
        if (completedEmptyObject != null) completedEmptyObject.SetActive(completedCount == 0);
    }

    private void OpenCompletedScenario(UserScenarioSession item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.scenario_id))
        {
            SetText(statusText, "시나리오 정보를 찾을 수 없습니다.");
            return;
        }

        if (!ResolveDataManager() || panelManager == null)
        {
            SetText(statusText, "시나리오 화면 연결을 확인하세요.");
            return;
        }

        if (!loading)
            StartCoroutine(OpenCompletedScenarioRoutine(item.scenario_id));
    }

    private IEnumerator OpenCompletedScenarioRoutine(string scenarioId)
    {
        loading = true;

        if (!dataManager.IsLoading && dataManager.Scenarios.Count == 0)
            dataManager.LoadAllScenarios();

        while (dataManager.IsLoading)
            yield return null;

        ScenarioRuntimeData scenario =
            dataManager.FindScenario(scenarioId);

        if (scenario == null)
        {
            SetText(statusText, "해당 시나리오 정보를 찾을 수 없습니다.");
            loading = false;
            yield break;
        }

        dataManager.SelectScenario(scenario);
        panelManager.ShowScenarioInfo(scenario);

        SetText(statusText, "");
        loading = false;
    }

    private void ContinueSession(UserScenarioSession item)
    {
        if (dataManager == null || panelManager == null || playController == null)
        {
            SetText(statusText, "진행 화면 컨트롤러 연결을 확인하세요.");
            return;
        }

        if (!loading)
            StartCoroutine(ContinueSessionRoutine(item));
    }

    private IEnumerator ContinueSessionRoutine(UserScenarioSession item)
    {
        float loadingStartedAt = Time.realtimeSinceStartup;
        loading = true;
        SetLoading(true, "시나리오를 불러오는 중...");
        string url = ServerConfig.HttpBaseUrl + "/api/sessions/" +
            UnityWebRequest.EscapeURL(item.session_id) + "/turn";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            CurrentTurnResponse response = JsonUtility.FromJson<CurrentTurnResponse>(
                request.downloadHandler.text);

            if (request.result != UnityWebRequest.Result.Success ||
                response?.status != "ok" || response.data?.progress == null)
            {
                yield return WaitForMinimumLoading(loadingStartedAt, 0.5f);
                SetLoading(false, response?.message ?? "시나리오 이어가기에 실패했습니다.");
                loading = false;
                yield break;
            }

            yield return WaitForMinimumLoading(loadingStartedAt, 0.5f);

            ScenarioRuntimeData runtime = new ScenarioRuntimeData
            {
                summary = new ScenarioSummary
                {
                    scenario_id = item.scenario_id,
                    version = item.scenario_version,
                    title = item.title,
                    description = item.description,
                    difficulty = item.difficulty,
                    total_turns = item.total_turns
                },
                session = response.data.session,
                turnData = response.data
            };

            dataManager.SelectScenario(runtime);
            panelManager.ShowScenarioPlay();
            playController.Initialize(runtime);
        }

        SetLoading(false, "");
        loading = false;
    }

    private static IEnumerator WaitForMinimumLoading(
        float startedAt,
        float minimumSeconds)
    {
        float remaining = minimumSeconds -
            (Time.realtimeSinceStartup - startedAt);

        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);
    }

    private void OpenCompletedResult(UserScenarioSession item)
    {
        if (dataManager == null || finalResultController == null)
        {
            SetText(statusText, "최종 결과 컨트롤러 연결을 확인하세요.");
            return;
        }

        if (!loading && !string.IsNullOrEmpty(item.evaluation_id))
            StartCoroutine(LoadCompletedResult(item));
    }

    private IEnumerator LoadCompletedResult(UserScenarioSession item)
    {
        loading = true;
        SetLoading(true, "최종 결과를 불러오는 중...");
        string userId = dataManager.CurrentUserId;
        string url = ServerConfig.HttpBaseUrl + "/api/users/" +
            UnityWebRequest.EscapeURL(userId) + "/evaluations/" +
            UnityWebRequest.EscapeURL(item.evaluation_id);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                SetLoading(false, "최종 결과 조회에 실패했습니다.");
                loading = false;
                yield break;
            }

            JObject root = JObject.Parse(request.downloadHandler.text);
            JToken result = root["data"];
            if (root["status"]?.ToString() != "ok" || result == null)
            {
                SetLoading(false, root["message"]?.ToString() ?? "최종 결과가 없습니다.");
                loading = false;
                yield break;
            }

            finalResultController.Show(result);
        }

        SetLoading(false, "");
        loading = false;
    }

    private void SetLoading(bool value, string message)
    {
        SetLoadingVisible(value);
        SetText(statusText, message);
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

    private static void ClearChildren(Transform content)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }

    private bool ResolveDataManager()
    {
        // 재진입한 Scenario 씬의 복제 객체 대신 영구 유지 중인 인스턴스를 사용합니다.
        if (ScenarioDataManager.Instance != null)
            dataManager = ScenarioDataManager.Instance;

        return dataManager != null;
    }
}

[Serializable]
public class UserScenarioSessionsResponse
{
    public string status;
    public UserScenarioSession[] data;
    public string message;
}

[Serializable]
public class UserScenarioSession
{
    public string session_id;
    public string user_id;
    public string scenario_id;
    public int scenario_version;
    public string title;
    public string description;
    public string difficulty;
    public string status;
    public int current_turn;
    public int completed_turns;
    public int total_turns;
    public float progress_pct;
    public string start_market_date;
    public string started_at;
    public string updated_at;
    public string completed_at;
    public string evaluation_id;
    public float cumulative_return_pct;
    public long final_value;
    public long profit_loss;
}


