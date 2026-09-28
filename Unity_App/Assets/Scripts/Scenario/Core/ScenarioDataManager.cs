using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ScenarioDataManager : MonoBehaviour
{
    public static ScenarioDataManager Instance { get; private set; }

    [Header("Server")]

    [SerializeField]
    private string userId = "UNITY-USER";

    public string CurrentUserId
    {
        get
        {
            if (AuthManager.Instance != null &&
                !string.IsNullOrWhiteSpace(AuthManager.Instance.UserId))
            {
                return AuthManager.Instance.UserId;
            }

            return userId;
        }
    }

    public bool IsLoading { get; private set; }

    public IReadOnlyList<ScenarioRuntimeData> Scenarios =>
        scenarios;

    public ScenarioRuntimeData SelectedScenario
    {
        get;
        private set;
    }

    public event Action<IReadOnlyList<ScenarioRuntimeData>>
        OnScenariosLoaded;

    public event Action<ScenarioRuntimeData>
        OnScenarioSelected;

    public event Action<string>
        OnError;

    private readonly List<ScenarioRuntimeData> scenarios =
        new List<ScenarioRuntimeData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadAllScenarios()
    {
        if (IsLoading)
            return;

        StartCoroutine(LoadAllScenariosRoutine());
    }

    public void SelectScenario(
        ScenarioRuntimeData scenario)
    {
        SelectedScenario = scenario;
        OnScenarioSelected?.Invoke(scenario);
    }

    public ScenarioRuntimeData FindScenario(string scenarioId)
    {
        return scenarios.Find(
            item => item.summary.scenario_id == scenarioId
        );
    }

    private IEnumerator LoadAllScenariosRoutine()
    {
        IsLoading = true;
        scenarios.Clear();

        ScenarioSummary[] summaries = null;
        string requestError = null;

        yield return GetScenarioList(
            result => summaries = result,
            error => requestError = error
        );

        if (!string.IsNullOrEmpty(requestError))
        {
            FinishWithError(requestError);
            yield break;
        }

        if (summaries == null)
        {
            FinishWithError(
                "시나리오 목록 데이터가 없습니다."
            );
            yield break;
        }

        // 목록 API에 시작일과 종목 정보가 없으므로
        // 시나리오마다 세션을 생성해 1턴 데이터를 조회합니다.
        foreach (ScenarioSummary summary in summaries)
        {
            SessionInfo session = null;
            CurrentTurnData turnData = null;

            yield return CreateSession(
                summary.scenario_id,
                result => session = result,
                error => requestError = error
            );

            if (!string.IsNullOrEmpty(requestError))
            {
                Debug.LogError(
                    $"[{summary.scenario_id}] 세션 생성 실패: " +
                    requestError
                );

                requestError = null;
                continue;
            }

            yield return GetCurrentTurn(
                session.session_id,
                result => turnData = result,
                error => requestError = error
            );

            if (!string.IsNullOrEmpty(requestError))
            {
                Debug.LogError(
                    $"[{summary.scenario_id}] 턴 조회 실패: " +
                    requestError
                );

                requestError = null;
                continue;
            }

            ScenarioRuntimeData runtimeData =
                new ScenarioRuntimeData
                {
                    summary = summary,
                    session = session,
                    turnData = turnData
                };

            scenarios.Add(runtimeData);
        }

        IsLoading = false;
        OnScenariosLoaded?.Invoke(scenarios);
    }

    private IEnumerator GetScenarioList(
        Action<ScenarioSummary[]> onSuccess,
        Action<string> onError)
    {
        string url =
            ServerConfig.HttpBaseUrl +
            "/api/scenarios";

        using (UnityWebRequest request =
               UnityWebRequest.Get(url))
        {
            request.timeout = 15;
            request.SetRequestHeader(
                "Accept",
                "application/json"
            );

            yield return request.SendWebRequest();

            if (!RequestSucceeded(request))
            {
                onError?.Invoke(
                    BuildRequestError(request)
                );

                yield break;
            }

            ScenarioListResponse response =
                JsonUtility.FromJson<ScenarioListResponse>(
                    request.downloadHandler.text
                );

            if (response == null ||
                response.status != "ok" ||
                response.data == null)
            {
                onError?.Invoke(
                    response?.message ??
                    "시나리오 목록 응답이 올바르지 않습니다."
                );

                yield break;
            }

            onSuccess?.Invoke(response.data);
        }
    }

    private IEnumerator CreateSession(
        string scenarioId,
        Action<SessionInfo> onSuccess,
        Action<string> onError)
    {
        string safeScenarioId =
            UnityWebRequest.EscapeURL(scenarioId);

        string url =
            ServerConfig.HttpBaseUrl +
            $"/api/scenarios/{safeScenarioId}/sessions";

        StartSessionRequest body =
            new StartSessionRequest
            {
                user_id = CurrentUserId
            };

        byte[] bodyBytes =
            Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(body)
            );

        using (UnityWebRequest request =
               new UnityWebRequest(
                   url,
                   UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler =
                new UploadHandlerRaw(bodyBytes);

            request.downloadHandler =
                new DownloadHandlerBuffer();

            request.timeout = 15;

            request.SetRequestHeader(
                "Content-Type",
                "application/json"
            );

            request.SetRequestHeader(
                "Accept",
                "application/json"
            );

            yield return request.SendWebRequest();

            if (!RequestSucceeded(request))
            {
                onError?.Invoke(
                    BuildRequestError(request)
                );

                yield break;
            }

            StartSessionResponse response =
                JsonUtility.FromJson<StartSessionResponse>(
                    request.downloadHandler.text
                );

            if (response == null ||
                response.status != "ok" ||
                response.data == null ||
                string.IsNullOrEmpty(
                    response.data.session_id))
            {
                onError?.Invoke(
                    response?.message ??
                    "세션 생성 응답이 올바르지 않습니다."
                );

                yield break;
            }

            onSuccess?.Invoke(response.data);
        }
    }

    private IEnumerator GetCurrentTurn(
        string sessionId,
        Action<CurrentTurnData> onSuccess,
        Action<string> onError)
    {
        string safeSessionId =
            UnityWebRequest.EscapeURL(sessionId);

        string url =
            ServerConfig.HttpBaseUrl +
            $"/api/sessions/{safeSessionId}/turn";

        using (UnityWebRequest request =
               UnityWebRequest.Get(url))
        {
            request.timeout = 15;
            request.SetRequestHeader(
                "Accept",
                "application/json"
            );

            yield return request.SendWebRequest();

            if (!RequestSucceeded(request))
            {
                onError?.Invoke(
                    BuildRequestError(request)
                );

                yield break;
            }

            CurrentTurnResponse response =
                JsonUtility.FromJson<CurrentTurnResponse>(
                    request.downloadHandler.text
                );

            if (response == null ||
                response.status != "ok" ||
                response.data == null)
            {
                onError?.Invoke(
                    response?.message ??
                    "현재 턴 응답이 올바르지 않습니다."
                );

                yield break;
            }

            onSuccess?.Invoke(response.data);
        }
    }

    private bool RequestSucceeded(
        UnityWebRequest request)
    {
        return request.result ==
               UnityWebRequest.Result.Success;
    }

    private string BuildRequestError(
        UnityWebRequest request)
    {
        string response =
            request.downloadHandler != null
                ? request.downloadHandler.text
                : "";

        return
            $"Status: {request.responseCode}\n" +
            $"Error: {request.error}\n" +
            $"Response: {response}";
    }

    private void FinishWithError(string message)
    {
        IsLoading = false;
        Debug.LogError(message);
        OnError?.Invoke(message);
    }
}

// ─────────────────────────────────────────────
// 런타임 통합 데이터
// ─────────────────────────────────────────────

[Serializable]
public class ScenarioRuntimeData
{
    public ScenarioSummary summary;
    public SessionInfo session;
    public CurrentTurnData turnData;

    public string ScenarioId =>
        summary.scenario_id;

    public string Title =>
        turnData.scenario.title;

    public string StartDate =>
        turnData.progress.market_date;

    public int CurrentTurn =>
        turnData.progress.current_turn;

    public int TotalTurns =>
        turnData.progress.total_turns;

    public string Difficulty =>
        turnData.scenario.difficulty;

    public string Description =>
        turnData.scenario.description;

    public long InitialCash =>
        turnData.portfolio.total_value;

    public int AssetCount =>
        turnData.assets != null
            ? turnData.assets.Length
            : 0;
}

// ─────────────────────────────────────────────
// API 응답 모델
// ─────────────────────────────────────────────

[Serializable]
public class ScenarioListResponse
{
    public string status;
    public ScenarioSummary[] data;
    public string message;
}

[Serializable]
public class ScenarioSummary
{
    public string scenario_id;
    public int version;
    public string title;
    public string description;
    public string difficulty;
    public int total_turns;
    public long initial_cash;
    public string[] learning_points;
}

[Serializable]
public class StartSessionRequest
{
    public string user_id;
}

[Serializable]
public class StartSessionResponse
{
    public string status;
    public SessionInfo data;
    public string message;
}

[Serializable]
public class CurrentTurnResponse
{
    public string status;
    public CurrentTurnData data;
    public string message;
}

[Serializable]
public class CurrentTurnData
{
    public SessionInfo session;
    public ProgressInfo progress;
    public ScenarioInfo scenario;
    public TurnInfo turn;
    public MarketState market_state;
    public NewsInfo[] news;
    public AssetInfo[] assets;
    public string default_asset_id;
    public PortfolioInfo portfolio;
    public QuestionInfo[] questions;
}

[Serializable]
public class SessionInfo
{
    public string session_id;
    public string user_id;
    public string scenario_id;
    public int scenario_version;
    public string status;
    public int current_turn;
    public string started_at;
    public string completed_at;
    public string final_evaluation_id;
}

[Serializable]
public class ProgressInfo
{
    public int current_turn;
    public int total_turns;
    public string market_date;
    public string next_market_date;
    public string final_valuation_date;
}

[Serializable]
public class ScenarioInfo
{
    public string scenario_id;
    public string title;
    public string description;
    public string difficulty;
    public string[] learning_points;
}

[Serializable]
public class TurnInfo
{
    public int turn_no;
    public string title;
    public string phase;
    public string summary;
}

[Serializable]
public class MarketState
{
    public string snapshot_id;
    public string scenario_id;
    public int scenario_version;
    public int turn_no;
    public string as_of_date;
    public string sentiment;
    public int sentiment_score;
    public string sector_state;
    public string[] risk_factors;
}

[Serializable]
public class NewsInfo
{
    public string scenario_id;
    public int scenario_version;
    public int visible_from_turn;
    public string news_id;
    public string published_at;
    public string title;
    public string summary;
    public string source_name;
    public string source_url;
    public string[] related_assets;
    public string importance;
    public int display_order;
}

[Serializable]
public class AssetInfo
{
    public string asset_id;
    public string name;
    public string market;
    public string asset_type;
    public string industry_label;
    public long current_price;
    public long previous_close;
    public long change;
    public float change_pct;
    public long volume;
    public string price_date;
    public bool data_available;
}

[Serializable]
public class PortfolioInfo
{
    public string market_date;
    public long cash;
    public float cash_weight_pct;
    public long position_value;
    public long total_value;
    public long profit_loss;
    public float cumulative_return_pct;
    public PositionInfo[] positions;
    public long realized_pnl_total;
    public string[] missing_price_assets;
    public bool data_complete;
    public long turn_base_value;
    public float turn_return_pct;
}

[Serializable]
public class PositionInfo
{
    public string asset_id;
    public string name;
    public int quantity;
    public long avg_price;
    public long current_price;
    public long market_value;
    public long unrealized_pnl;
    public float weight_pct;
    public string industry_label;
}

[Serializable]
public class QuestionInfo
{
    public string category;
    public string metric;
    public string type;
    public int max_select;
    public string text;
    public string[] options;
    public string guide;
    public string question_id;
}


