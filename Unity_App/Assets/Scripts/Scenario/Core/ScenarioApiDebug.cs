using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
public class ScenarioFullApiDebug : MonoBehaviour
{
    [Header("Server")]

    [Header("Test Data")]
    [SerializeField]
    private string scenarioId = "semiconductor";

    [SerializeField]
    private string userId = "UNITY-DEBUG-USER";

    private string sessionId;

    private void Start()
    {
        StartCoroutine(DebugFullFlow());
    }

    private IEnumerator DebugFullFlow()
    {
        yield return GetAndLog(
            "/",
            "서버 상태"
        );

        yield return GetAndLog(
            "/api/scenarios",
            "시나리오 목록"
        );

        yield return StartScenarioSession();

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Debug.LogError(
                "[Scenario API] 세션 생성 단계에서 실패했습니다. " +
                "바로 위의 [세션 생성 결과] 로그에서 " +
                "Status Code와 Response를 확인하세요."
            );

            yield break;
        }

        yield return GetAndLog(
            $"/api/sessions/{sessionId}/turn",
            "현재 턴 전체 데이터"
        );
    }

    private IEnumerator GetAndLog(
        string path,
        string logTitle)
    {
        string url = ServerConfig.HttpBaseUrl + path;

        Debug.Log(
            $"[{logTitle}] GET 요청\n{url}"
        );

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 15;
            request.SetRequestHeader(
                "Accept",
                "application/json"
            );

            yield return request.SendWebRequest();

            PrintResponse(request, logTitle);
        }
    }

    private IEnumerator StartScenarioSession()
    {
        string url =
            ServerConfig.HttpBaseUrl +
            $"/api/scenarios/{scenarioId}/sessions";

        string requestJson =
            "{\"user_id\":\"" + userId + "\"}";

        Debug.Log(
            $"[세션 생성] 요청\n" +
            $"URL: {url}\n" +
            $"Body: {requestJson}"
        );

        byte[] bodyRaw =
            Encoding.UTF8.GetBytes(requestJson);

        using (UnityWebRequest request =
               new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler =
                new UploadHandlerRaw(bodyRaw);

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

            string responseText =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : "";

            Debug.Log(
                $"[세션 생성 결과]\n" +
                $"Result: {request.result}\n" +
                $"Status Code: {request.responseCode}\n" +
                $"Error: {request.error}\n" +
                $"Response: {responseText}"
            );

            if (request.result !=
                UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[세션 생성 실패]\n{responseText}"
                );

                yield break;
            }

            // JsonUtility 대신 응답 원문에서 session_id 추출
            Match match = Regex.Match(
                responseText,
                "\"session_id\"\\s*:\\s*\"([^\"]+)\""
            );

            if (!match.Success)
            {
                Debug.LogError(
                    $"[세션 파싱 실패] 응답에 session_id가 없습니다.\n" +
                    $"서버 응답 원문:\n{responseText}"
                );

                yield break;
            }

            sessionId = match.Groups[1].Value;

            Debug.Log(
                $"[세션 생성 성공]\n" +
                $"Session ID: {sessionId}"
            );
        }
    }

    private void PrintResponse(
        UnityWebRequest request,
        string logTitle)
    {
        string responseText =
            request.downloadHandler != null
                ? request.downloadHandler.text
                : "";

        if (request.result ==
            UnityWebRequest.Result.Success)
        {
            Debug.Log(
                $"[{logTitle}] 요청 성공\n" +
                $"Status Code: {request.responseCode}\n" +
                $"Response:\n{responseText}"
            );
        }
        else
        {
            Debug.LogError(
                $"[{logTitle}] 요청 실패\n" +
                $"Status Code: {request.responseCode}\n" +
                $"Result: {request.result}\n" +
                $"Error: {request.error}\n" +
                $"Response:\n{responseText}"
            );
        }
    }

    [System.Serializable]
    private class StartSessionRequest
    {
        public string user_id;
    }

    [System.Serializable]
    private class StartSessionResponse
    {
        public string status;
        public SessionData data;
        public string message;
    }

    [System.Serializable]
    private class SessionData
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
}

