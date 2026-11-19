using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class ImageFadeToScene : MonoBehaviour
{
    [Header("이미지 등장")]
    [Tooltip("이미지가 투명도 0%에서 불투명도 100%가 되는 데 걸리는 시간(초)")]
    [Min(0f)]
    public float fadeDuration = 2f;

    [Header("다음 씬")]
    [Tooltip("Build Profiles의 Scene List에 추가된 씬 이름을 입력하세요.")]
    public string nextSceneName = "Login";

    private Image targetImage;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        SetAlpha(0f);
    }

    private IEnumerator Start()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        while (elapsed < duration)
        {
            // Time.timeScale이 0이어도 이미지 등장은 진행한다.
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        SetAlpha(1f);
        // 완전히 나타난 이미지를 한 프레임 표시한 후 씬을 전환한다.
        yield return null;

        string sceneName = (nextSceneName ?? "").Trim();
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"[ImageFadeToScene] 다음 씬 '{sceneName}'을 불러올 수 없습니다. " +
                "씬 이름과 Build Profiles의 Scene List를 확인하세요.", this
            );
            yield break;
        }

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }

    private void SetAlpha(float alpha)
    {
        Color color = targetImage.color;
        color.a = Mathf.Clamp01(alpha);
        targetImage.color = color;
    }
}
