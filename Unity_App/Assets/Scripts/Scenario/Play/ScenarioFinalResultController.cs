using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioFinalResultController : MonoBehaviour
{
    [Header("연결")]
    [SerializeField]
    private ScenarioTurnFeedbackController feedbackController;

    [SerializeField]
    private ScenarioPanelManager panelManager;

    [SerializeField]
    private ScenarioListController scenarioListController;

    [SerializeField]
    private ScenarioProgressListController progressListController;

    [Header("패널")]
    [SerializeField]
    private GameObject finalResultPanel;

    [Header("버튼")]
    [SerializeField]
    private Button returnToListButton;

    [SerializeField]
    private Button closeButton;

    [Header("투자 결과")]
    [SerializeField]
    private TMP_Text returnRateText;

    [SerializeField]
    private TMP_Text profitLossText;

    [SerializeField]
    private TMP_Text initialAssetText;

    [SerializeField]
    private TMP_Text finalAssetText;

    [Header("전체 판단 점수 - 선택")]
    [SerializeField]
    private TMP_Text overallScoreText;

    [SerializeField]
    private ScenarioStarRatingView overallScoreStars;

    [Header("항목별 평균 평가 - 6개")]
    [SerializeField]
    private MetricSlot[] metricSlots;

    [Header("최종 피드백")]
    [SerializeField]
    private TMP_Text strengthsText;

    [SerializeField]
    private TMP_Text improvementsText;

    [SerializeField]
    private TMP_Text summaryText;

    [Header("추가 분석 - 선택")]
    [SerializeField]
    private TMP_Text behaviorPatternsText;

    [SerializeField]
    private TMP_Text nextActionsText;

    [Header("손익 색상")]
    [SerializeField]
    private Color riseColor =
        new Color(0.95f, 0.2f, 0.2f);

    [SerializeField]
    private Color fallColor =
        new Color(0.2f, 0.4f, 0.95f);

    [SerializeField]
    private Color neutralColor = Color.gray;

    [Header("상태 메시지 - 선택")]
    [SerializeField]
    private TMP_Text messageText;

    private void Awake()
    {
        if (returnToListButton != null)
        {
            returnToListButton.onClick.AddListener(
                ReturnToScenarioList
            );
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(
                ReturnToScenarioList
            );
        }

        if (finalResultPanel != null)
            finalResultPanel.SetActive(false);
    }

    // FeedbackController의 On Final Result Requested에 연결
    public void ShowFromFeedback()
    {
        if (feedbackController == null)
        {
            SetMessage(
                "ScenarioTurnFeedbackController를 연결하세요."
            );

            return;
        }

        Show(feedbackController.FinalEvaluation);
    }

    public void Show(JToken evaluation)
    {
        if (evaluation == null ||
            evaluation.Type == JTokenType.Null)
        {
            SetMessage("최종 평가 데이터가 없습니다.");
            return;
        }

        ScenarioFinalEvaluation data;

        try
        {
            data = evaluation.ToObject<
                ScenarioFinalEvaluation
            >();
        }
        catch (Exception exception)
        {
            SetMessage("최종 평가 데이터를 읽을 수 없습니다.");

            Debug.LogError(
                "[ScenarioFinalResult] " +
                exception.Message
            );

            return;
        }

        if (data?.decision_evaluation == null ||
            data.portfolio_analysis == null)
        {
            SetMessage("최종 평가 응답을 확인하세요.");
            return;
        }

        ApplyData(data);

        if (finalResultPanel != null)
        {
            finalResultPanel.SetActive(true);
            finalResultPanel.transform.SetAsLastSibling();
        }

        SetMessage("");
    }

    private void ApplyData(
        ScenarioFinalEvaluation data)
    {

        FinalPortfolioAnalysis portfolio =
            data.portfolio_analysis;

        SetText(
            returnRateText,
            FormatSigned(
                portfolio.cumulative_return_pct,
                "0.00"
            ) + "%"
        );

        SetText(
            profitLossText,
            "(" +
            FormatSigned(
                portfolio.profit_loss,
                "N0"
            ) +
            "원)"
        );

        SetText(
            initialAssetText,
            portfolio.initial_value.ToString("N0") +
            "원"
        );

        SetText(
            finalAssetText,
            portfolio.final_value.ToString("N0") +
            "원"
        );

        Color resultColor =
            GetProfitColor(portfolio.profit_loss);

        if (returnRateText != null)
            returnRateText.color = resultColor;

        if (profitLossText != null)
            profitLossText.color = resultColor;

        float overallScore = Mathf.Clamp(
            data.decision_evaluation.overall_score,
            0f,
            5f
        );

        SetText(
            overallScoreText,
            overallScore.ToString("0.0") + " / 5.0"
        );

        if (overallScoreStars != null)
            overallScoreStars.SetScore(overallScore);

        ApplyMetricScores(
            data.decision_evaluation.metric_averages
        );

        FinalFeedback feedback = data.feedback;

        SetText(
            strengthsText,
            FormatList(
                feedback?.strengths,
                "뚜렷한 강점으로 분류된 항목이 없습니다."
            )
        );

        SetText(
            improvementsText,
            FormatList(
                feedback?.improvements,
                "우선 보완 항목으로 분류된 항목이 없습니다."
            )
        );

        SetText(
            summaryText,
            feedback?.summary ??
            "종합 피드백이 없습니다."
        );

        SetText(
            nextActionsText,
            FormatList(
                feedback?.next_actions,
                "추가로 제안된 개선 행동이 없습니다."
            )
        );

        SetText(
            behaviorPatternsText,
            FormatBehaviorPatterns(
                data.behavior_patterns
            )
        );
    }

    private void ApplyMetricScores(
        Dictionary<string, float> averages)
    {
        if (metricSlots == null)
            return;

        foreach (MetricSlot slot in metricSlots)
        {
            if (slot == null)
                continue;

            float value = 0f;

            bool hasScore =
                averages != null &&
                averages.TryGetValue(
                    slot.metric.ToString(),
                    out value
                );

            float score =
                hasScore
                    ? Mathf.Clamp(value, 0f, 5f)
                    : 0f;

            SetText(
                slot.scoreText,
                hasScore
                    ? score.ToString("0.0") + " / 5"
                    : "-"
            );

            if (slot.stars != null)
                slot.stars.SetScore(score);

            if (slot.scoreSlider != null)
            {
                slot.scoreSlider.minValue = 0f;
                slot.scoreSlider.maxValue = 5f;
                slot.scoreSlider.wholeNumbers = false;
                slot.scoreSlider.interactable = false;

                slot.scoreSlider.SetValueWithoutNotify(
                    score
                );
            }
        }
    }

    public void ReturnToScenarioList()
    {
        if (panelManager == null)
        {
            SetMessage(
                "ScenarioPanelManager를 연결하세요."
            );

            return;
        }

        if (finalResultPanel != null)
            finalResultPanel.SetActive(false);

        panelManager.ShowScenarioList();

        if (scenarioListController != null)
        {
            scenarioListController.LoadScenarios();
        }
        else
        {
            Debug.LogWarning(
                "[ScenarioFinalResult] " +
                "ScenarioListController가 연결되지 않았습니다.",
                this
            );
        }

        if (progressListController != null)
        {
            progressListController.LoadProgress();
        }
        else
        {
            Debug.LogWarning(
                "[ScenarioFinalResult] " +
                "ScenarioProgressListController가 연결되지 않았습니다.",
                this
            );
        }
    }

    private Color GetProfitColor(double value)
    {
        if (value > 0)
            return riseColor;

        if (value < 0)
            return fallColor;

        return neutralColor;
    }

    private static string FormatSigned(
        double value,
        string format)
    {
        string prefix = value > 0 ? "+" : "";

        return prefix + value.ToString(format);
    }

    private static string FormatList(
        string[] items,
        string emptyMessage)
    {
        if (items == null || items.Length == 0)
            return emptyMessage;

        List<string> lines = new List<string>();

        foreach (string item in items)
        {
            if (!string.IsNullOrWhiteSpace(item))
                lines.Add("• " + item);
        }

        return lines.Count > 0
            ? string.Join("\n", lines)
            : emptyMessage;
    }

    private static string FormatBehaviorPatterns(
        FinalBehaviorPattern[] patterns)
    {
        if (patterns == null || patterns.Length == 0)
            return "관찰된 행동 패턴이 없습니다.";

        List<string> lines = new List<string>();

        foreach (FinalBehaviorPattern pattern in patterns)
        {
            if (pattern == null)
                continue;

            string classification =
                pattern.classification == "REPEATED_PATTERN"
                    ? "반복 패턴"
                    : "관찰";

            lines.Add(
                "• " +
                pattern.label +
                " (" +
                classification +
                ", " +
                pattern.occurrence_count +
                "개 턴)\n" +
                pattern.explanation
            );
        }

        return string.Join("\n\n", lines);
    }

    private void SetMessage(string message)
    {
        SetText(messageText, message);
    }

    private static void SetText(
        TMP_Text target,
        string value)
    {
        if (target != null)
            target.text = value;
    }

    private void OnDestroy()
    {
        if (returnToListButton != null)
        {
            returnToListButton.onClick.RemoveListener(
                ReturnToScenarioList
            );
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(
                ReturnToScenarioList
            );
        }
    }

    public enum MetricType
    {
        M1,
        M2,
        M3,
        M4,
        M5,
        PORTFOLIO
    }

    [Serializable]
    private class MetricSlot
    {
        public MetricType metric;
        public TMP_Text scoreText;
        public ScenarioStarRatingView stars;

        // 최종 화면에 슬라이더가 없다면 비워도 됨
        public Slider scoreSlider;
    }
}

// 최종 평가 응답 모델

[Serializable]
public class ScenarioFinalEvaluation
{
    public string evaluation_id;
    public string user_id;
    public string session_id;
    public string scenario_id;
    public int scenario_version;
    public string completed_at;

    public FinalDecisionEvaluation decision_evaluation;
    public FinalPortfolioAnalysis portfolio_analysis;
    public FinalFeedback feedback;
    public FinalBehaviorPattern[] behavior_patterns;
}

[Serializable]
public class FinalDecisionEvaluation
{
    public float overall_score;

    public Dictionary<string, float> metric_averages;
}

[Serializable]
public class FinalPortfolioAnalysis
{
    public long initial_value;
    public long final_value;
    public long profit_loss;

    public double cumulative_return_pct;
}

[Serializable]
public class FinalFeedback
{
    public string summary;
    public string[] strengths;
    public string[] improvements;
    public string[] next_actions;
}

[Serializable]
public class FinalBehaviorPattern
{
    public string pattern_code;
    public string label;
    public string classification;
    public int occurrence_count;
    public int[] evidence_turns;
    public float confidence;
    public string explanation;
    public string recommendation;
}
