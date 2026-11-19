using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public class AuthManager : MonoBehaviour
{
    [Header("통합 서버 주소")]
    [Tooltip("예: http://192.168.219.107:8000")]
    public string serverUrl = ServerConfig.DefaultHttpBaseUrl;

    public static AuthManager Instance { get; private set; }

    public string AccessToken =>
        PlayerPrefs.GetString("accessToken", "");

    public string Username =>
        PlayerPrefs.GetString("username", "");

    public string UserId =>
        PlayerPrefs.GetString("userId", "");

    public bool IsRequesting { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ServerConfig.ConfigureFromSaved(serverUrl);
        serverUrl = ServerConfig.HttpBaseUrl;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[ServerConfig] " + ServerConfig.HttpBaseUrl);
    }

    public bool TrySetServerUrl(string value, out string errorMessage)
    {
        if (IsRequesting)
        {
            errorMessage = "로그인 요청이 끝난 뒤 변경해 주세요.";
            return false;
        }

        try
        {
            ServerConfig.ConfigureAndSave(value);
            serverUrl = ServerConfig.HttpBaseUrl;
            errorMessage = "";

            Debug.Log("[ServerConfig] 저장됨: " + serverUrl);
            return true;
        }
        catch (ArgumentException error)
        {
            errorMessage = error.Message;
            return false;
        }
    }

    public void Login(
        string username,
        string password,
        Action<bool, string> callback)
    {
        if (IsRequesting)
        {
            callback?.Invoke(false, "요청을 처리하고 있습니다.");
            return;
        }

        StartCoroutine(
            LoginCoroutine(username, password, callback)
        );
    }

    public void Register(
        string username,
        string password,
        Action<bool, string> callback)
    {
        if (IsRequesting)
        {
            callback?.Invoke(false, "요청을 처리하고 있습니다.");
            return;
        }

        StartCoroutine(
            RegisterCoroutine(username, password, callback)
        );
    }

    private IEnumerator LoginCoroutine(
        string username,
        string password,
        Action<bool, string> callback)
    {
        IsRequesting = true;

        AuthRequest body = new AuthRequest
        {
            username = username.Trim(),
            password = password
        };

        yield return SendJson(
            "/api/auth/login",
            body,
            (success, responseBody) =>
            {
                IsRequesting = false;

                if (!success)
                {
                    callback?.Invoke(
                        false,
                        ParseError(responseBody)
                    );
                    return;
                }

                LoginResponse response;

                try
                {
                    response = JsonConvert.DeserializeObject<LoginResponse>(
                        responseBody
                    );
                }
                catch (Exception error)
                {
                    callback?.Invoke(
                        false,
                        "로그인 응답을 읽지 못했습니다: " + error.Message
                    );
                    return;
                }

                if (response == null ||
                    string.IsNullOrWhiteSpace(response.accessToken))
                {
                    callback?.Invoke(false, "로그인 토큰이 없습니다.");
                    return;
                }

                PlayerPrefs.SetString("accessToken", response.accessToken);
                PlayerPrefs.SetString("username", response.username ?? "");
                PlayerPrefs.SetString("userId", response.id ?? "");
                PlayerPrefs.Save();

                callback?.Invoke(true, "로그인 성공");
            }
        );
    }

    private IEnumerator RegisterCoroutine(
        string username,
        string password,
        Action<bool, string> callback)
    {
        IsRequesting = true;

        AuthRequest body = new AuthRequest
        {
            username = username.Trim(),
            password = password
        };

        yield return SendJson(
            "/api/auth/signup",
            body,
            (success, responseBody) =>
            {
                IsRequesting = false;
                callback?.Invoke(
                    success,
                    success
                        ? "회원가입 성공"
                        : ParseError(responseBody)
                );
            }
        );
    }

    private IEnumerator SendJson(
        string path,
        object body,
        Action<bool, string> callback)
    {
        string url = ServerConfig.HttpBaseUrl + path;
        string json = JsonConvert.SerializeObject(body);

        using UnityWebRequest request =
            new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler = new UploadHandlerRaw(
            Encoding.UTF8.GetBytes(json)
        );
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 15;

        yield return request.SendWebRequest();

        string responseBody = request.downloadHandler?.text ?? "";
        bool success = request.responseCode >= 200 &&
                       request.responseCode < 300;

        if (!success && string.IsNullOrWhiteSpace(responseBody))
        {
            responseBody = request.error;
        }

        callback?.Invoke(success, responseBody);
    }

    private string ParseError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return "서버 응답이 없습니다.";

        try
        {
            ApiError error =
                JsonConvert.DeserializeObject<ApiError>(responseBody);

            if (error != null && !string.IsNullOrWhiteSpace(error.detail))
                return error.detail;
        }
        catch
        {
            // JSON 오류면 원문 반환
        }

        return responseBody;
    }

    public void AddAuthorizationHeader(UnityWebRequest request)
    {
        if (!string.IsNullOrWhiteSpace(AccessToken))
        {
            request.SetRequestHeader(
                "Authorization",
                "Bearer " + AccessToken
            );
        }
    }

    public void Logout()
    {
        PlayerPrefs.DeleteKey("accessToken");
        PlayerPrefs.DeleteKey("username");
        PlayerPrefs.DeleteKey("userId");
        PlayerPrefs.Save();
    }

    public bool IsLoggedIn()
    {
        return !string.IsNullOrWhiteSpace(AccessToken);
    }
}

[Serializable]
public class AuthRequest
{
    public string username;
    public string password;
}

[Serializable]
public class LoginResponse
{
    public string id;
    public string username;
    public string accessToken;
}

[Serializable]
public class ApiError
{
    public string detail;
}



