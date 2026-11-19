using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioButtonItem : MonoBehaviour
{
    [Header("Button")]
    [SerializeField]
    private Button button;

    [Header("Texts")]
    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text startDateText;

    [SerializeField]
    private TMP_Text turnsText;

    [SerializeField]
    private TMP_Text difficultyText;

    [Header("Difficulty Dots")]
    [Tooltip("난이도 하/중/상을 1~3개의 동그라미로 표시")]
    [SerializeField]
    private ScenarioDifficultyDots difficultyDots;

    private ScenarioRuntimeData scenarioData;
    private Action<ScenarioRuntimeData> clickCallback;

    public void Initialize(
        ScenarioRuntimeData data,
        Action<ScenarioRuntimeData> onClick)
    {
        scenarioData = data;
        clickCallback = onClick;

        titleText.text =
            data.Title;

        startDateText.text =
            $"{FormatDate(data.StartDate)} 시작";

        turnsText.text =
            $"총 {data.TotalTurns} 턴";

        if (difficultyText != null)
        {
            difficultyText.text =
                $"난이도  {data.Difficulty}";
        }

        if (difficultyDots != null)
        {
            difficultyDots.SetDifficulty(
                data.Difficulty
            );
        }

        // 동적으로 생성된 프리팹 버튼의 OnClick 연결
        button.onClick.RemoveListener(
            HandleButtonClick
        );

        button.onClick.AddListener(
            HandleButtonClick
        );
    }

    private void HandleButtonClick()
    {
        if (scenarioData == null)
        {
            Debug.LogError(
                "시나리오 데이터가 연결되지 않았습니다."
            );

            return;
        }

        // ScenarioListController.OpenScenarioInfoPanel 호출
        clickCallback?.Invoke(scenarioData);
    }

    private static string FormatDate(string value)
    {
        if (DateTime.TryParse(value, out DateTime date))
            return date.ToString("yyyy.MM.dd");

        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(
                HandleButtonClick
            );
        }
    }
}
