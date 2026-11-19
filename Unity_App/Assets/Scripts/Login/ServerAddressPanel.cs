using TMPro;
using UnityEngine;

/// <summary>
/// 로그인 화면에서 서버 주소 설정 패널을 열고 저장합니다.
/// </summary>
public class ServerAddressPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Input")]
    [SerializeField] private TMP_InputField serverAddressInput;

    [Header("Message (Optional)")]
    [SerializeField] private TMP_Text messageText;

    [Header("Startup")]
    [SerializeField] private bool closePanelOnStart = true;

    private void Start()
    {
        if (closePanelOnStart && panel != null)
            panel.SetActive(false);
    }

    public void OpenPanel()
    {
        if (serverAddressInput != null)
            serverAddressInput.text = ServerConfig.HttpBaseUrl;

        SetMessage("");

        if (panel != null)
            panel.SetActive(true);

        if (serverAddressInput != null)
        {
            serverAddressInput.ActivateInputField();
            serverAddressInput.Select();
        }
    }

    public void ClosePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void ApplyServerAddress()
    {
        if (serverAddressInput == null)
        {
            SetMessage("서버 주소 입력창이 연결되지 않았습니다.");
            return;
        }

        if (AuthManager.Instance == null)
        {
            SetMessage("로그인 관리자를 찾을 수 없습니다.");
            return;
        }

        string address = serverAddressInput.text.Trim();

        if (!AuthManager.Instance.TrySetServerUrl(address, out string error))
        {
            SetMessage(error);
            return;
        }

        serverAddressInput.text = ServerConfig.HttpBaseUrl;
        SetMessage("서버 주소가 저장되었습니다.");
        ClosePanel();
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
            messageText.text = message ?? "";
    }
}
