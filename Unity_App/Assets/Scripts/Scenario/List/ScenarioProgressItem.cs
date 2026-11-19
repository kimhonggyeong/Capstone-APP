using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioProgressItem : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private ScenarioDifficultyDots difficultyDots;
    [SerializeField] private ScenarioProgressRing progressRing;

    private UserScenarioSession data;
    private Action<UserScenarioSession> clickCallback;

    public void Initialize(
        UserScenarioSession value,
        Action<UserScenarioSession> onClick)
    {
        data = value;
        clickCallback = onClick;

        bool completed = value.status == "COMPLETED";

        SetText(badgeText, completed ? "완료" : "진행 중");
        SetText(titleText, value.title);
        SetText(dateText, FormatDate(value.start_market_date) + " 시작");
        SetText(durationText, "총 " + value.total_turns + "턴");

        if (difficultyDots != null)
            difficultyDots.SetDifficulty(value.difficulty);

        if (progressRing != null)
        {
            if (completed)
                progressRing.SetReturn(value.cumulative_return_pct);
            else
                progressRing.SetProgress(value.progress_pct, "진행률");
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        if (data != null)
            clickCallback?.Invoke(data);
    }

    private static string FormatDate(string value)
    {
        if (DateTime.TryParse(value, out DateTime date))
            return date.ToString("yyyy.MM.dd");

        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }
}
