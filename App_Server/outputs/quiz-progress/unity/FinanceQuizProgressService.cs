using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>씬을 이동해도 퀴즈 기록을 전송하며, 실패한 기록은 사용자별로 재전송합니다.</summary>
public sealed class FinanceQuizProgressService : MonoBehaviour
{
    private const string StoragePrefix = "antitude:app-quiz-progress:v1:";
    private static FinanceQuizProgressService instance;
    public static FinanceQuizProgressService Instance
    {
        get
        {
            if (instance == null)
                new GameObject(nameof(FinanceQuizProgressService)).AddComponent<FinanceQuizProgressService>();
            return instance;
        }
    }

    public event Action Changed;
    public int TotalQuizCount { get; private set; } = 360;
    public string Username => username;
    public string LastError { get; private set; } = "";
    public int PendingCount => state.pending.Count;
    public FinanceQuizProgressSnapshot Current => state.Project();
    public float ProgressPercent => Current.ProgressPercent(TotalQuizCount);

    private FinanceQuizProgressState state = new FinanceQuizProgressState();
    private string username = "";
    private string baseUrl = "";
    private int generation;
    private bool syncing;
    private bool refreshRequested;
    private float retryAt;
    private float nextIdentityCheck;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextIdentityCheck)
        {
            nextIdentityCheck = Time.unscaledTime + 0.5f;
            UpdateIdentity();
        }
        if (!syncing && !string.IsNullOrEmpty(username) && Time.unscaledTime >= retryAt
            && (refreshRequested || state.pending.Count > 0))
        {
            syncing = true;
            StartCoroutine(Sync(generation, username, baseUrl));
        }
    }

    public void ConfigureTotalQuizCount(int count)
    {
        if (count <= 0 || count == TotalQuizCount) return;
        TotalQuizCount = count;
        Changed?.Invoke();
    }

    public void RefreshProgress()
    {
        UpdateIdentity();
        if (string.IsNullOrEmpty(username)) return;
        refreshRequested = true;
    }

    public void RecordAnswer(string quizId, bool isCorrect, string sessionUsername)
    {
        UpdateIdentity();
        if (!CanRecord(sessionUsername) || string.IsNullOrWhiteSpace(quizId)) return;
        state.QueueAnswer(quizId, isCorrect);
        Persist();
        Changed?.Invoke();
    }

    public void RecordSessionCompleted(string sessionUsername)
    {
        UpdateIdentity();
        if (!CanRecord(sessionUsername)) return;
        state.QueueSessionCompleted();
        Persist();
        Changed?.Invoke();
    }

    private bool CanRecord(string sessionUsername)
    {
        if (!string.IsNullOrEmpty(username) && username == sessionUsername) return true;
        Debug.LogWarning("[QuizProgress] 로그인 사용자가 변경되어 이전 퀴즈 기록을 저장하지 않았습니다.");
        return false;
    }

    private void UpdateIdentity()
    {
        AuthManager auth = AuthManager.Instance;
        // The existing web uses username as the quiz_progress.user_id key.
        string current = auth != null && auth.IsLoggedIn() ? auth.Username : "";
        string server = ServerConfig.HttpBaseUrl;
        if (current == username && server == baseUrl) return;
        Persist();
        username = current;
        baseUrl = server;
        generation++;
        state = Load(username);
        LastError = "";
        retryAt = 0;
        refreshRequested = !string.IsNullOrEmpty(username);
        Changed?.Invoke();
    }

    private static string StorageKey(string user) => StoragePrefix + Uri.EscapeDataString(user);

    private static FinanceQuizProgressState Load(string user)
    {
        if (string.IsNullOrEmpty(user)) return new FinanceQuizProgressState();
        try
        {
            var result = JsonConvert.DeserializeObject<FinanceQuizProgressState>(
                PlayerPrefs.GetString(StorageKey(user), "")) ?? new FinanceQuizProgressState();
            result.Normalize();
            return result;
        }
        catch (Exception error)
        {
            Debug.LogWarning("[QuizProgress] 저장된 기록을 읽지 못했습니다: " + error.Message);
            return new FinanceQuizProgressState();
        }
    }

    private void Persist()
    {
        if (string.IsNullOrEmpty(username)) return;
        try
        {
            // One value contains both the server snapshot and the pending queue.
            PlayerPrefs.SetString(StorageKey(username), JsonConvert.SerializeObject(state));
            PlayerPrefs.Save();
        }
        catch (Exception error)
        {
            Debug.LogWarning("[QuizProgress] 기기 임시 저장 실패: " + error.Message);
        }
    }

    private IEnumerator Sync(int version, string user, string server)
    {
        try
        {
            while (version == generation && (state.pending.Count > 0 || refreshRequested))
            {
                FinanceQuizProgressEvent item = state.pending.Count > 0 ? state.pending[0] : null;
                if (item == null) refreshRequested = false;
                string path = server + "/api/users/" + Uri.EscapeDataString(user) + "/quiz-progress";
                string url = item != null ? path + "/events" : path + "?total_quiz_count=" + TotalQuizCount;
                FinanceQuizProgressSnapshot snapshot = null;
                string failure = null;
                yield return Send(url, item, (result, error) => { snapshot = result; failure = error; });
                // An old request must never change another account's displayed/cache data.
                UpdateIdentity();
                if (version != generation) yield break;
                if (snapshot == null)
                {
                    if (LastError != failure) Debug.LogWarning("[QuizProgress] " + failure + " (10초 뒤 재시도)");
                    LastError = failure;
                    refreshRequested = true;
                    retryAt = Time.unscaledTime + 10f;
                    Changed?.Invoke();
                    yield break;
                }
                state.Accept(item?.event_id, snapshot);
                LastError = "";
                Persist();
                Changed?.Invoke();
            }
        }
        finally
        {
            syncing = false;
        }
    }

    private static IEnumerator Send(string url, FinanceQuizProgressEvent item,
        Action<FinanceQuizProgressSnapshot, string> completed)
    {
        using (var request = new UnityWebRequest(url, item == null ? "GET" : "POST"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 10;
            if (item != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (AuthManager.Instance != null) AuthManager.Instance.AddAuthorizationHeader(request);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                completed(null, "서버 저장·조회 실패: " + request.error);
                yield break;
            }
            FinanceQuizProgressSnapshot snapshot = null;
            string failure = null;
            try
            {
                JObject root = JObject.Parse(request.downloadHandler.text);
                JToken data = root["data"] ?? root;
                if (data.Type != JTokenType.Object || data["answered_quiz_ids"] == null)
                    throw new JsonException("퀴즈 진행도 응답 형식이 올바르지 않습니다.");
                snapshot = data.ToObject<FinanceQuizProgressSnapshot>();
            }
            catch (Exception error)
            {
                failure = "서버 응답을 읽지 못했습니다: " + error.Message;
            }
            completed(snapshot, failure);
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) RefreshProgress();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Persist();
        else RefreshProgress();
    }

    private void OnApplicationQuit() => Persist();

    private void OnDestroy()
    {
        if (instance == this)
        {
            Persist();
            instance = null;
        }
    }
}
