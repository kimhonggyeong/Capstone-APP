using System;
using UnityEngine;

/// <summary>
/// 모든 Unity 서버 연결이 사용하는 단일 주소 설정입니다.
/// 기본값은 로그인 씬의 AuthManager Inspector에서 덮어쓸 수 있습니다.
/// </summary>
public static class ServerConfig
{
    public const string DefaultHttpBaseUrl = "http://192.168.219.107:8000";
    public const string PlayerPrefsKey = "serverHttpBaseUrl";

    private static string httpBaseUrl = DefaultHttpBaseUrl;

    public static string HttpBaseUrl => httpBaseUrl;

    public static string WebSocketBaseUrl
    {
        get
        {
            Uri uri = new Uri(HttpBaseUrl);
            string scheme = uri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            return scheme + "://" + uri.Authority;
        }
    }

    public static void Configure(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            httpBaseUrl = DefaultHttpBaseUrl;
            return;
        }

        string normalized = value.Trim().TrimEnd('/');
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "서버 주소는 http:// 또는 https://로 시작해야 합니다.",
                nameof(value)
            );
        }

        httpBaseUrl = normalized;
    }

    public static void ConfigureFromSaved(string fallbackUrl)
    {
        string savedUrl = PlayerPrefs.GetString(PlayerPrefsKey, "");
        Configure(string.IsNullOrWhiteSpace(savedUrl) ? fallbackUrl : savedUrl);
    }

    public static void ConfigureAndSave(string value)
    {
        Configure(value);
        PlayerPrefs.SetString(PlayerPrefsKey, HttpBaseUrl);
        PlayerPrefs.Save();
    }

    public static string HttpUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return HttpBaseUrl;

        return HttpBaseUrl + "/" + path.TrimStart('/');
    }

    public static string WebSocketUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return WebSocketBaseUrl;

        return WebSocketBaseUrl + "/" + path.TrimStart('/');
    }
}
