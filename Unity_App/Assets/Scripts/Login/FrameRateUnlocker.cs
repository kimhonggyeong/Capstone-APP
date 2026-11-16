using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class FrameRateByDisplayHz : MonoBehaviour
{
    private static FrameRateByDisplayHz instance;

    [Header("실제 FPS 표시")]
    public bool showFps = true;
    [Tooltip("씬 전환 후에도 표시하려면 이 오브젝트 자식의 전용 Canvas 안에 있는 TMP 텍스트를 연결하세요.")]
    public TMP_Text fpsText;
    [Tooltip("이 시간 동안의 평균 FPS를 표시합니다. 단위: 초")]
    [Min(0.1f)]
    public float fpsUpdateInterval = 0.5f;

    private int measuredFrames;
    private float measuredSeconds;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Login 씬에 재진입해도 새 씬의 FPS 텍스트가 중복 표시되지 않게 한다.
            if (fpsText != null && fpsText != instance.fpsText)
                fpsText.enabled = false;
            Destroy(this);
            return;
        }

        instance = this;
        // 다른 씬에서도 앱 복귀 시 목표 FPS를 재적용한다.
        DontDestroyOnLoad(gameObject);
        ApplyFrameRate();
        if (fpsText != null)
        {
            fpsText.raycastTarget = false;
            fpsText.enabled = showFps;
            fpsText.text = "FPS: --";
        }
    }

    private void Update()
    {
        if (instance != this || fpsText == null)
            return;

        if (fpsText.enabled != showFps)
            fpsText.enabled = showFps;

        if (!showFps)
        {
            measuredFrames = 0;
            measuredSeconds = 0f;
            return;
        }

        // 목표 FPS가 아닌 실제 프레임 진행 횟수를 사용한다.
        measuredFrames++;
        measuredSeconds += Time.unscaledDeltaTime;
        if (measuredSeconds < Mathf.Max(0.1f, fpsUpdateInterval))
            return;

        float actualFps = measuredFrames / measuredSeconds;
        fpsText.text = $"FPS: {actualFps:0.0} / Target: {Application.targetFrameRate}";
        measuredFrames = 0;
        measuredSeconds = 0f;
    }

    private IEnumerator Start()
    {
        if (instance != this)
            yield break;

        // Android 화면 초기화 직후의 값을 한 번 더 확인한다.
        yield return new WaitForSecondsRealtime(0.5f);
        ApplyFrameRate();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (instance != this)
            return;

        measuredFrames = 0;
        measuredSeconds = 0f;
        if (hasFocus && isActiveAndEnabled)
            ApplyFrameRate();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = 0;

#if UNITY_2022_2_OR_NEWER
        int currentHz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
#else
        int currentHz = Screen.currentResolution.refreshRate;
#endif
        if (currentHz <= 0)
            currentHz = 60;

        int maximumHz = currentHz;
#if UNITY_ANDROID && !UNITY_EDITOR
        maximumHz = GetMaximumAndroidRefreshRate(currentHz);
#endif
        Application.targetFrameRate = maximumHz;
        Debug.Log(
            $"Display Hz: {currentHz}, Supported Max Hz: {maximumHz}, " +
            $"Target FPS: {Application.targetFrameRate}"
        );
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static int GetMaximumAndroidRefreshRate(int fallbackHz)
    {
        int maximumHz = fallbackHz;
        AndroidJavaObject[] modes = null;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var windowManager = activity.Call<AndroidJavaObject>("getWindowManager"))
            using (var display = windowManager.Call<AndroidJavaObject>("getDefaultDisplay"))
            using (var currentMode = display.Call<AndroidJavaObject>("getMode"))
            {
                int width = currentMode.Call<int>("getPhysicalWidth");
                int height = currentMode.Call<int>("getPhysicalHeight");
                modes = display.Call<AndroidJavaObject[]>("getSupportedModes");
                if (modes != null)
                {
                    foreach (AndroidJavaObject mode in modes)
                    {
                        if (mode == null)
                            continue;

                        // 해상도를 바꾸지 않고 현재 해상도에서 지원하는 최대 Hz를 선택한다.
                        if (mode.Call<int>("getPhysicalWidth") != width ||
                            mode.Call<int>("getPhysicalHeight") != height)
                            continue;

                        maximumHz = Mathf.Max(maximumHz,
                            Mathf.RoundToInt(mode.Call<float>("getRefreshRate")));
                    }
                }
            }
        }
        catch (System.Exception error)
        {
            Debug.LogWarning(
                $"[FrameRateByDisplayHz] 지원 주사율 조회 실패. 현재 주사율을 사용합니다: {error.Message}"
            );
        }
        finally
        {
            if (modes != null)
            {
                foreach (AndroidJavaObject mode in modes)
                    mode?.Dispose();
            }
        }
        return maximumHz;
    }
#endif
}
