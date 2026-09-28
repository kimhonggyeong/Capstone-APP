using System;
using System.Globalization;
using TMPro;
using UnityEngine;

public class ScenarioInfoPanel : MonoBehaviour
{
    [Header("Scenario")]
    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text dateText;

    [SerializeField]
    private TMP_Text turnText;

    [SerializeField]
    private TMP_Text difficultyText;

    [SerializeField]
    private TMP_Text backgroundText;

    [Header("Simulation")]
    [SerializeField]
    private TMP_Text initialCashText;

    [SerializeField]
    private TMP_Text assetCountText;

    [SerializeField]
    private TMP_Text totalTurnText;

    [SerializeField]
    private TMP_Text turnIntervalText;

    public void SetData(
        ScenarioRuntimeData scenario)
    {
        if (scenario == null ||
            scenario.turnData == null)
        {
            Debug.LogError(
                "[ScenarioInfoPanel] 시나리오 데이터가 없습니다."
            );

            return;
        }

        CurrentTurnData data =
            scenario.turnData;

        titleText.text =
            scenario.Title;

        dateText.text =
            scenario.StartDate;

        turnText.text =
            $"{scenario.CurrentTurn} / " +
            $"{scenario.TotalTurns}턴";

        difficultyText.text =
            scenario.Difficulty;

        backgroundText.text =
            scenario.Description;

        initialCashText.text =
            $"{scenario.InitialCash:N0}원";

        assetCountText.text =
            $"{scenario.AssetCount}개";

        totalTurnText.text =
            $"총 {scenario.TotalTurns}턴";

        turnIntervalText.text =
            BuildTurnIntervalText(
                data.progress.current_turn,
                data.progress.market_date,
                data.progress.next_market_date
            );
    }

    private string BuildTurnIntervalText(
        int currentTurn,
        string currentDateText,
        string nextDateText)
    {
        if (string.IsNullOrEmpty(nextDateText))
            return "마지막 턴";

        if (!TryParseDate(
                currentDateText,
                out DateTime currentDate) ||
            !TryParseDate(
                nextDateText,
                out DateTime nextDate))
        {
            return "간격 계산 불가";
        }

        int totalDays =
            (nextDate - currentDate).Days;

        return $"{totalDays}일";
    }

    private bool TryParseDate(
        string value,
        out DateTime date)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date
        );
    }
}
