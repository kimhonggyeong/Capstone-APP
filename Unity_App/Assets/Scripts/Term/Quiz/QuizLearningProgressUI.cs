using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>누적 학습 진행률 텍스트와 슬라이더만 Inspector에서 연결합니다.</summary>
public sealed class QuizLearningProgressUI : MonoBehaviour
{
    [Header("누적 학습 진행도")]
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Slider progressSlider;

    [Header("표시 설정")]
    [Tooltip("웹에서 푼 기록도 표시 중에 다시 조회하는 간격입니다. 0이면 자동 조회를 끕니다.")]
    [SerializeField, Min(0)] private float refreshIntervalSeconds = 15f;

    private FinanceQuizProgressService service;
    private float nextRefresh;

    private void OnEnable()
    {
        service = FinanceQuizProgressService.Instance;
        service.Changed += RefreshDisplay;
        if (progressSlider != null)
        {
            progressSlider.minValue = 0;
            progressSlider.maxValue = 100;
            progressSlider.wholeNumbers = false;
            progressSlider.interactable = false;
        }
        service.RefreshProgress();
        RefreshDisplay();
        nextRefresh = Time.unscaledTime + refreshIntervalSeconds;
    }

    private void Update()
    {
        if (refreshIntervalSeconds <= 0 || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshIntervalSeconds;
        service.RefreshProgress();
    }

    private void OnDisable()
    {
        if (service != null) service.Changed -= RefreshDisplay;
    }

    public void RefreshProgress()
    {
        if (service != null) service.RefreshProgress();
    }

    private void RefreshDisplay()
    {
        float percent = service != null ? service.ProgressPercent : 0;
        if (progressText != null)
            progressText.text = percent.ToString("F0", CultureInfo.InvariantCulture) + "%";
        if (progressSlider != null) progressSlider.SetValueWithoutNotify(percent);
    }
}
