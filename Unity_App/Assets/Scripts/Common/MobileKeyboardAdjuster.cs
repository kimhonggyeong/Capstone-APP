using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 현재 포커스된 UI 입력창이 모바일 키보드에 가려질 때 필요한 거리만큼만
/// targetPanel을 위로 이동합니다. TMP_InputField와 기본 InputField를 모두 지원합니다.
/// </summary>
[DisallowMultipleComponent]
public class MobileKeyboardAdjuster : MonoBehaviour
{
    [Header("Move Target")]
    [Tooltip("이 패널이 위아래로 이동합니다. 비어 있으면 이 컴포넌트의 RectTransform을 사용합니다.")]
    public RectTransform targetPanel;

    [Header("Input Detection")]
    [Tooltip("기존 씬 호환용 선택 필드입니다. 비워도 현재 선택된 입력창을 자동 감지합니다.")]
    public TMP_InputField inputField;

    [Tooltip("EventSystem에서 현재 선택된 모든 입력창을 자동 감지합니다.")]
    public bool detectAnyFocusedInput = true;

    [Tooltip("targetPanel 안에 포함된 입력창에만 반응합니다.")]
    public bool onlyInputsInsideTarget = true;

    [Header("Keyboard Detection")]
    public bool useKeyboardAreaHeight = true;

    [Range(0.2f, 0.8f)]
    [Tooltip("기기에서 키보드 영역을 알려주지 않을 때 사용할 화면 높이 비율입니다.")]
    public float fallbackKeyboardRatio = 0.50f;

    [Tooltip("키보드 API가 높이를 보고하지 않아도 포커스된 입력창이 있으면 fallback을 사용합니다.")]
    public bool useFallbackWhenAreaUnavailable = true;

    [Min(0f)]
    [Tooltip("키보드가 열리는 동안 API 응답을 기다린 뒤 fallback을 적용하는 시간입니다.")]
    public float keyboardOpenDelay = 0.12f;

    [Header("Move Settings")]
    [Min(0f)]
    [Tooltip("입력창과 키보드 사이에 둘 여백입니다. Canvas 단위입니다.")]
    public float extraPadding = 40f;

    [Min(0.1f)]
    public float moveSpeed = 18f;

    [Range(0.1f, 1f)]
    [Tooltip("화면 높이 대비 패널이 이동할 수 있는 최대 비율입니다.")]
    public float maxMoveScreenRatio = 0.75f;

    [Header("Close Detect")]
    [Min(0f)]
    public float closeDelay = 0.15f;

    [Header("Editor Test")]
    public bool editorTestMode = true;
    public float editorKeyboardHeight = 800f;
    public Color editorKeyboardColor = new Color(0.12f, 0.32f, 0.65f, 0.78f);

    private Vector2 originalAnchoredPos;
    private float targetY;
    private float keyboardClosedTimer;
    private float focusStartedAt;
    private bool keyboardWasReported;
    private bool initialized;
    private RectTransform lastFocusedInput;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        CaptureOriginalPosition();
    }

    private void OnDisable()
    {
        RestoreImmediately();
    }

    private void Initialize()
    {
        if (targetPanel == null)
            targetPanel = GetComponent<RectTransform>();

        if (targetPanel == null)
        {
            enabled = false;
            Debug.LogWarning("[MobileKeyboardAdjuster] 이동할 RectTransform이 없습니다.", this);
            return;
        }

        if (!initialized)
        {
            originalAnchoredPos = targetPanel.anchoredPosition;
            targetY = originalAnchoredPos.y;
            initialized = true;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void CaptureOriginalPosition()
    {
        if (targetPanel == null)
            return;

        originalAnchoredPos = targetPanel.anchoredPosition;
        targetY = originalAnchoredPos.y;
        keyboardClosedTimer = 0f;
        keyboardWasReported = false;
        lastFocusedInput = null;
    }

    private void Update()
    {
        if (!initialized || targetPanel == null)
            return;

        HandleResolutionChange();

        RectTransform focusedInput = FindFocusedInput();
        if (focusedInput != lastFocusedInput)
        {
            // 키보드가 닫힌 상태에서 레이아웃이 이동했다면 새 기준 위치를 사용합니다.
            if (lastFocusedInput == null && Mathf.Abs(targetPanel.anchoredPosition.y - targetY) < 0.5f)
                originalAnchoredPos = targetPanel.anchoredPosition;

            lastFocusedInput = focusedInput;
            focusStartedAt = Time.unscaledTime;
            keyboardClosedTimer = 0f;
            keyboardWasReported = false;
        }

        bool shouldAvoid = false;
        float keyboardTopPixels = 0f;

#if UNITY_EDITOR
        if (IsEditorKeyboardVisible())
        {
            shouldAvoid = focusedInput != null || inputField != null;
            keyboardTopPixels = Mathf.Clamp(editorKeyboardHeight, 0f, Screen.height);
            if (focusedInput == null && inputField != null)
                focusedInput = inputField.GetComponent<RectTransform>();
        }
#elif UNITY_ANDROID
        if (focusedInput != null)
        {
            float imeHeight = GetAndroidImeHeightPixels();
            bool keyboardReported = imeHeight > 0f;

            if (keyboardReported)
            {
                keyboardWasReported = true;
                keyboardClosedTimer = 0f;
                shouldAvoid = true;
                keyboardTopPixels = Mathf.Clamp(imeHeight, 0f, Screen.height);
            }
            else if (!keyboardWasReported &&
                     useFallbackWhenAreaUnavailable &&
                     Time.unscaledTime - focusStartedAt >= keyboardOpenDelay)
            {
                // 플로팅 키보드나 Insets를 제공하지 않는 구형 기기에서만 사용합니다.
                shouldAvoid = true;
                keyboardTopPixels = Screen.height * fallbackKeyboardRatio;
            }
            else if (keyboardWasReported)
            {
                keyboardClosedTimer += Time.unscaledDeltaTime;
                shouldAvoid = keyboardClosedTimer < closeDelay;
            }
        }
#elif UNITY_IOS
        if (focusedInput != null)
        {
            Rect keyboardArea = TouchScreenKeyboard.area;
            bool areaAvailable = useKeyboardAreaHeight && keyboardArea.height > 0f;
            bool keyboardReported = TouchScreenKeyboard.visible || areaAvailable;

            if (keyboardReported)
            {
                keyboardWasReported = true;
                keyboardClosedTimer = 0f;
                shouldAvoid = true;
                keyboardTopPixels = areaAvailable
                    ? keyboardArea.yMax
                    : Screen.height * fallbackKeyboardRatio;
                keyboardTopPixels = Mathf.Clamp(keyboardTopPixels, 0f, Screen.height);
            }
            else if (!keyboardWasReported &&
                     useFallbackWhenAreaUnavailable &&
                     Time.unscaledTime - focusStartedAt >= keyboardOpenDelay)
            {
                // 일부 Android 기기는 area와 visible을 보고하지 않으므로 비율 기반으로 대응합니다.
                shouldAvoid = true;
                keyboardTopPixels = Screen.height * fallbackKeyboardRatio;
            }
            else if (keyboardWasReported)
            {
                keyboardClosedTimer += Time.unscaledDeltaTime;
                shouldAvoid = keyboardClosedTimer < closeDelay;
            }
        }
#endif

        if (shouldAvoid && focusedInput != null)
            targetY = CalculateAvoidanceY(focusedInput, keyboardTopPixels);
        else
            targetY = originalAnchoredPos.y;

        MoveTowardsTarget();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// Android가 보고하는 실제 IME 점유 높이를 Unity 화면 픽셀 단위로 반환합니다.
    /// API 30 이상에서는 WindowInsets.Type.ime()를 사용하므로 키보드 툴바도 포함됩니다.
    /// 구형 Android에서는 보이는 Window 영역의 차이를 사용합니다.
    /// </summary>
    private float GetAndroidImeHeightPixels()
    {
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow"))
            using (AndroidJavaObject decorView = window.Call<AndroidJavaObject>("getDecorView"))
            {
                int viewHeight = decorView.Call<int>("getHeight");
                if (viewHeight <= 0)
                    return 0f;

                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdk = version.GetStatic<int>("SDK_INT");
                    if (sdk >= 30)
                    {
                        using (AndroidJavaObject windowInsets = decorView.Call<AndroidJavaObject>("getRootWindowInsets"))
                        {
                            if (windowInsets == null)
                                return 0f;

                            using (AndroidJavaClass insetType = new AndroidJavaClass("android.view.WindowInsets$Type"))
                            {
                                int imeType = insetType.CallStatic<int>("ime");
                                if (!windowInsets.Call<bool>("isVisible", imeType))
                                    return 0f;

                                using (AndroidJavaObject imeInsets =
                                       windowInsets.Call<AndroidJavaObject>("getInsets", imeType))
                                {
                                    int bottom = imeInsets.Get<int>("bottom");
                                    return ConvertAndroidToUnityPixels(bottom, viewHeight);
                                }
                            }
                        }
                    }
                }

                using (AndroidJavaObject visibleFrame = new AndroidJavaObject("android.graphics.Rect"))
                {
                    decorView.Call("getWindowVisibleDisplayFrame", visibleFrame);
                    int visibleBottom = visibleFrame.Get<int>("bottom");
                    int obscuredHeight = Mathf.Max(0, viewHeight - visibleBottom);

                    // 내비게이션 바만 있는 경우를 키보드로 오인하지 않습니다.
                    if (obscuredHeight < viewHeight * 0.15f)
                        return 0f;

                    return ConvertAndroidToUnityPixels(obscuredHeight, viewHeight);
                }
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[MobileKeyboardAdjuster] Android IME 높이 측정 실패: {exception.Message}");
            return 0f;
        }
    }

    private static float ConvertAndroidToUnityPixels(int androidPixels, int androidViewHeight)
    {
        if (androidPixels <= 0 || androidViewHeight <= 0)
            return 0f;

        return androidPixels * (Screen.height / (float)androidViewHeight);
    }
#endif

#if UNITY_EDITOR
    private bool IsEditorKeyboardVisible()
    {
        return editorTestMode &&
               Keyboard.current != null &&
               Keyboard.current.kKey.isPressed;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !IsEditorKeyboardVisible())
            return;

        float height = Mathf.Clamp(editorKeyboardHeight, 0f, Screen.height);
        Rect keyboardRect = new Rect(0f, Screen.height - height, Screen.width, height);

        Color previousColor = GUI.color;
        GUI.color = editorKeyboardColor;
        GUI.DrawTexture(keyboardRect, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.025f), 14, 28),
            fontStyle = FontStyle.Bold
        };
        GUI.Label(keyboardRect, "EDITOR KEYBOARD  (K 키를 놓으면 닫힘)", labelStyle);
        GUI.color = previousColor;
    }
#endif

    private RectTransform FindFocusedInput()
    {
        if (detectAnyFocusedInput && EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null)
            {
                TMP_InputField tmpInput = selected.GetComponent<TMP_InputField>() ??
                                          selected.GetComponentInParent<TMP_InputField>();
                if (tmpInput != null && tmpInput.isFocused && IsAllowed(tmpInput.transform))
                    return tmpInput.GetComponent<RectTransform>();

                InputField legacyInput = selected.GetComponent<InputField>() ??
                                         selected.GetComponentInParent<InputField>();
                if (legacyInput != null && legacyInput.isFocused && IsAllowed(legacyInput.transform))
                    return legacyInput.GetComponent<RectTransform>();
            }
        }

        if (inputField != null && inputField.isFocused && IsAllowed(inputField.transform))
            return inputField.GetComponent<RectTransform>();

        return null;
    }

    private bool IsAllowed(Transform inputTransform)
    {
        return !onlyInputsInsideTarget ||
               targetPanel == null ||
               inputTransform == targetPanel ||
               inputTransform.IsChildOf(targetPanel);
    }

    private float CalculateAvoidanceY(RectTransform focusedInput, float keyboardTopPixels)
    {
        Canvas canvas = focusedInput.GetComponentInParent<Canvas>();
        float canvasScale = canvas != null && canvas.scaleFactor > 0f
            ? canvas.scaleFactor
            : 1f;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3[] corners = new Vector3[4];
        focusedInput.GetWorldCorners(corners);

        float inputBottom = float.MaxValue;
        float inputTop = float.MinValue;
        for (int i = 0; i < corners.Length; i++)
        {
            float screenY = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]).y;
            inputBottom = Mathf.Min(inputBottom, screenY);
            inputTop = Mathf.Max(inputTop, screenY);
        }

        float currentShiftUnits = targetPanel.anchoredPosition.y - originalAnchoredPos.y;
        float currentShiftPixels = currentShiftUnits * canvasScale;
        float baseInputBottom = inputBottom - currentShiftPixels;
        float baseInputTop = inputTop - currentShiftPixels;

        float paddingPixels = extraPadding * canvasScale;
        float requiredShiftPixels = Mathf.Max(
            0f,
            keyboardTopPixels + paddingPixels - baseInputBottom
        );

        // 입력창이 화면의 Safe Area 위쪽을 벗어나지 않도록 이동량을 제한합니다.
        float safeTop = Screen.safeArea.yMax;
        float availableUpwardPixels = Mathf.Max(0f, safeTop - paddingPixels - baseInputTop);
        float ratioLimitPixels = Screen.height * maxMoveScreenRatio;
        float maximumShiftPixels = Mathf.Min(ratioLimitPixels, availableUpwardPixels);

        if (maximumShiftPixels > 0f)
            requiredShiftPixels = Mathf.Min(requiredShiftPixels, maximumShiftPixels);
        else
            requiredShiftPixels = Mathf.Min(requiredShiftPixels, ratioLimitPixels);

        return originalAnchoredPos.y + requiredShiftPixels / canvasScale;
    }

    private void MoveTowardsTarget()
    {
        Vector2 position = targetPanel.anchoredPosition;
        position.y = Mathf.Lerp(
            position.y,
            targetY,
            1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime)
        );

        if (Mathf.Abs(position.y - targetY) < 0.5f)
            position.y = targetY;

        targetPanel.anchoredPosition = position;
    }

    private void HandleResolutionChange()
    {
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
            return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (lastFocusedInput == null)
            CaptureOriginalPosition();
    }

    private void RestoreImmediately()
    {
        if (!initialized || targetPanel == null)
            return;

        targetPanel.anchoredPosition = originalAnchoredPos;
        targetY = originalAnchoredPos.y;
        lastFocusedInput = null;
        keyboardWasReported = false;
        keyboardClosedTimer = 0f;
    }
}
