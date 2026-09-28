using UnityEngine;

public class FrameRateByDisplayHz : MonoBehaviour
{
    private void Awake()
    {
        QualitySettings.vSyncCount = 0;

#if UNITY_2022_2_OR_NEWER
        int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
#else
        int hz = Screen.currentResolution.refreshRate;
#endif

        // 혹시 0으로 잡히는 기기 대비
        if (hz <= 0)
            hz = 60;

        Application.targetFrameRate = hz;

        Debug.Log($"Display Hz: {hz}, Target FPS: {Application.targetFrameRate}");
    }
}