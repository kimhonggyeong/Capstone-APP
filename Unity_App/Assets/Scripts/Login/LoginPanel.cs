using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginPanel : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private TMP_InputField usernameInput;

    [SerializeField]
    private TMP_InputField passwordInput;

    [Header("Text")]
    [SerializeField]
    private TMP_Text messageText;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text loginButtonText;

    [SerializeField]
    private TMP_Text switchModeButtonText;

    [Header("Buttons")]
    [SerializeField]
    private Button submitButton;

    [SerializeField]
    private Button switchModeButton;

    [Header("Scene")]
    [SerializeField]
    private string nextSceneName = "Real";

    [Header("Auto Login")]
    [Tooltip("저장된 토큰이 있으면 로그인 화면을 건너뜁니다.")]
    [SerializeField]
    private bool autoLoginEnabled = true;

    private bool isRegisterMode;
    private bool isProcessing;

    private void Start()
    {
        SetLoginMode();

        if (
            autoLoginEnabled &&
            AuthManager.Instance != null &&
            AuthManager.Instance.IsLoggedIn()
        )
        {
            SetMessage("저장된 로그인 정보를 확인하는 중...");
            SetButtonsEnabled(false);

            /*
             * 현재 방식은 저장된 토큰 존재 여부만 확인합니다.
             * 토큰이 만료된 경우 다음 API 요청에서 401을 받고
             * 로그아웃 처리해야 합니다.
             */
            SceneManager.LoadScene(nextSceneName);
        }
    }

    /// <summary>
    /// 로그인 또는 회원가입 버튼의 OnClick에 연결합니다.
    /// </summary>
    public void OnClickLogin()
    {
        if (isProcessing)
            return;

        if (isRegisterMode)
        {
            Register();
        }
        else
        {
            Login();
        }
    }

    /// <summary>
    /// 로그인/회원가입 모드 전환 버튼의 OnClick에 연결합니다.
    /// </summary>
    public void OnClickSwitchMode()
    {
        if (isProcessing)
            return;

        if (isRegisterMode)
        {
            SetLoginMode();
        }
        else
        {
            SetRegisterMode();
        }
    }

    private void SetLoginMode()
    {
        isRegisterMode = false;

        if (titleText != null)
        {
            titleText.text = "Sign in to your account";
        }

        if (loginButtonText != null)
        {
            loginButtonText.text = "Sign in";
        }

        if (switchModeButtonText != null)
        {
            switchModeButtonText.text =
                "Or create an account";
        }

        SetMessage("");
        ClearInputs();
    }

    private void SetRegisterMode()
    {
        isRegisterMode = true;

        if (titleText != null)
        {
            titleText.text = "Sign up";
        }

        if (loginButtonText != null)
        {
            loginButtonText.text = "Sign up";
        }

        if (switchModeButtonText != null)
        {
            switchModeButtonText.text =
                "Already a user?";
        }

        SetMessage("");
        ClearInputs();
    }

    private void ClearInputs()
    {
        if (usernameInput != null)
        {
            usernameInput.text = "";
        }

        if (passwordInput != null)
        {
            passwordInput.text = "";
        }
    }

    private bool CheckInput(
        out string username,
        out string password)
    {
        username = usernameInput != null
            ? usernameInput.text.Trim()
            : "";

        /*
         * 비밀번호에는 공백이 포함될 수도 있으므로
         * Trim()하지 않습니다.
         */
        password = passwordInput != null
            ? passwordInput.text
            : "";

        if (
            string.IsNullOrEmpty(username) ||
            string.IsNullOrEmpty(password)
        )
        {
            SetMessage(
                "아이디와 비밀번호를 입력하세요."
            );

            return false;
        }

        if (username.Length < 3)
        {
            SetMessage(
                "아이디는 3자 이상이어야 합니다."
            );

            return false;
        }

        if (username.Length > 30)
        {
            SetMessage(
                "아이디는 30자 이하여야 합니다."
            );

            return false;
        }

        if (password.Length < 6)
        {
            SetMessage(
                "비밀번호는 6자 이상이어야 합니다."
            );

            return false;
        }

        if (password.Length > 100)
        {
            SetMessage(
                "비밀번호는 100자 이하여야 합니다."
            );

            return false;
        }

        return true;
    }

    private void Login()
    {
        Debug.Log(
            "[LoginPanel] 로그인 버튼 클릭"
        );

        if (!TryGetAuthManager())
            return;

        if (
            !CheckInput(
                out string username,
                out string password
            )
        )
        {
            return;
        }

        SetProcessing(
            true,
            "로그인 중..."
        );

        AuthManager.Instance.Login(
            username,
            password,
            (success, message) =>
            {
                SetProcessing(false);

                Debug.Log(
                    "[LoginPanel] 로그인 결과: " +
                    $"success={success}, " +
                    $"message={message}"
                );

                if (!success)
                {
                    SetMessage(
                        "로그인 실패: " + message
                    );

                    return;
                }

                SetMessage("로그인 성공");

                Debug.Log(
                    "[LoginPanel] 사용자명: " +
                    AuthManager.Instance.Username
                );

                Debug.Log(
                    "[LoginPanel] 사용자 ID: " +
                    AuthManager.Instance.UserId
                );

                /*
                 * 보안상 AccessToken 전체를
                 * Console에 출력하지 않습니다.
                 */
                Debug.Log(
                    "[LoginPanel] 토큰 저장 여부: " +
                    !string.IsNullOrWhiteSpace(
                        AuthManager.Instance.AccessToken
                    )
                );

                if (
                    string.IsNullOrWhiteSpace(
                        nextSceneName
                    )
                )
                {
                    SetMessage(
                        "이동할 씬 이름이 설정되지 않았습니다."
                    );

                    return;
                }

                SceneManager.LoadScene(
                    nextSceneName
                );
            }
        );
    }

    private void Register()
    {
        Debug.Log(
            "[LoginPanel] 회원가입 버튼 클릭"
        );

        if (!TryGetAuthManager())
            return;

        if (
            !CheckInput(
                out string username,
                out string password
            )
        )
        {
            return;
        }

        SetProcessing(
            true,
            "회원가입 중..."
        );

        AuthManager.Instance.Register(
            username,
            password,
            (success, message) =>
            {
                SetProcessing(false);

                Debug.Log(
                    "[LoginPanel] 회원가입 결과: " +
                    $"success={success}, " +
                    $"message={message}"
                );

                if (!success)
                {
                    SetMessage(
                        "회원가입 실패: " + message
                    );

                    return;
                }

                /*
                 * 모드를 로그인으로 전환하면서
                 * 입력창을 초기화합니다.
                 */
                SetLoginMode();

                if (usernameInput != null)
                {
                    usernameInput.text = username;
                }

                if (passwordInput != null)
                {
                    passwordInput.text = "";
                    passwordInput.ActivateInputField();
                }

                SetMessage(
                    "회원가입 성공. 로그인해 주세요."
                );
            }
        );
    }

    private bool TryGetAuthManager()
    {
        if (AuthManager.Instance != null)
        {
            return true;
        }

        Debug.LogError(
            "[LoginPanel] AuthManager.Instance가 null입니다."
        );

        SetMessage(
            "로그인 관리자를 찾을 수 없습니다."
        );

        return false;
    }

    private void SetProcessing(
        bool processing,
        string message = null)
    {
        isProcessing = processing;
        SetButtonsEnabled(!processing);

        if (message != null)
        {
            SetMessage(message);
        }
    }

    private void SetButtonsEnabled(
        bool enabled)
    {
        if (submitButton != null)
        {
            submitButton.interactable =
                enabled;
        }

        if (switchModeButton != null)
        {
            switchModeButton.interactable =
                enabled;
        }

        if (usernameInput != null)
        {
            usernameInput.interactable =
                enabled;
        }

        if (passwordInput != null)
        {
            passwordInput.interactable =
                enabled;
        }
    }

    private void SetMessage(
        string message)
    {
        if (messageText != null)
        {
            messageText.text =
                message ?? "";
        }
    }
}