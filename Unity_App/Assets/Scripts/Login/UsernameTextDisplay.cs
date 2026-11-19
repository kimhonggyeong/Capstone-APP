using TMPro;
using UnityEngine;

/// <summary>
/// 이 컴포넌트를 붙인 TMP 텍스트에 현재 로그인한 사용자명을 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public class UsernameTextDisplay : MonoBehaviour
{
    [Header("표시 대상")]
    [SerializeField] private TMP_Text targetText;

    [Header("표시 형식")]
    [SerializeField] private string prefix = "";
    [SerializeField] private string suffix = "";
    [SerializeField] private string emptyText = "사용자";

    private string lastUsername;

    private void Reset()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        lastUsername = null;
        RefreshUsername();
    }

    private void Update()
    {
        // AuthManager 생성이나 로그인 정보 변경이 늦게 이뤄져도 자동 반영합니다.
        string username = GetCurrentUsername();
        if (username != lastUsername)
            ApplyUsername(username);
    }

    /// <summary>
    /// 필요할 때 버튼이나 다른 스크립트에서도 직접 호출할 수 있습니다.
    /// </summary>
    public void RefreshUsername()
    {
        ApplyUsername(GetCurrentUsername());
    }

    private static string GetCurrentUsername()
    {
        if (AuthManager.Instance != null)
            return AuthManager.Instance.Username;

        // 씬 로딩 순서상 AuthManager가 아직 생성되지 않은 경우를 위한 예비 처리입니다.
        return PlayerPrefs.GetString("username", "");
    }

    private void ApplyUsername(string username)
    {
        lastUsername = username;

        if (targetText == null)
            return;

        targetText.text = string.IsNullOrWhiteSpace(username)
            ? emptyText
            : prefix + username + suffix;
    }
}
