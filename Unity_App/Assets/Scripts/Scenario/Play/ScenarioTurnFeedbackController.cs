using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ScenarioTurnFeedbackController : MonoBehaviour
{
    [Header("서버")]

    [Header("컨트롤러")]
    [SerializeField]
    private ScenarioOrderController orderController;

    [SerializeField]
    private ScenarioPlayController playController;

    [Header("패널")]
    [SerializeField]
    private GameObject feedbackPanel;

    [SerializeField]
    private GameObject saveCompletePanel;

    [Header("로딩")]
    [SerializeField]
    private GameObject loadingPanel;

    [Header("피드백 버튼")]
    [SerializeField]
    private Button nextTurnButton;

    [SerializeField]
    private TMP_Text turnProgressText;

    [Header("저장 완료 패널")]
    [SerializeField]
    private Button saveCompleteConfirmButton;

    [SerializeField]
    private Button saveCompleteCloseButton;

    [SerializeField]
    private TMP_Text savedTitleText;

    [SerializeField]
    private TMP_Text savedDescriptionText;

    [SerializeField] private TMP_Text savedCurrentDateText;
    [SerializeField] private TMP_Text savedNextDateText;

    [SerializeField] private TMP_Text savedCurrentTurnText;
    [SerializeField] private TMP_Text savedNextTurnText;

    [Header("피드백")]
    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text totalScoreText;

    [SerializeField]
    private ScenarioStarRatingView totalScoreStars;

    [SerializeField]
    private TMP_Text goodPointsText;

    [SerializeField]
    private TMP_Text missedPointsText;

    [SerializeField]
    private TMP_Text explanationText;

    [Header("항목별 평가 - 6개")]
    [SerializeField]
    private MetricSlot[] metricSlots;

    [Header("이번 턴 주문 요약")]
    [SerializeField]
    private Transform orderContent;

    [SerializeField]
    private TurnEndOrderItem orderPrefab;

    [SerializeField]
    private TMP_Text emptyOrderText;

    [Header("턴 종료 시점 비중")]
    [SerializeField]
    private TMP_Text investmentWeightText;

    [SerializeField]
    private TMP_Text cashWeightText;

    [Header("상태 메시지")]
    [SerializeField]
    private TMP_Text messageText;

    [Header("최종 결과 화면")]
    [SerializeField]
    private UnityEvent onFinalResultRequested;

    public JToken FinalEvaluation { get; private set; }

    private ScenarioRuntimeData submittedScenario;
    private TurnSubmitData lastResult;
    private bool loadingNextTurn;

    private void Awake()
    {
        if (nextTurnButton != null)
        {
            nextTurnButton.onClick.AddListener(
                OpenSaveCompletePanel
            );
        }

        if (saveCompleteConfirmButton != null)
        {
            saveCompleteConfirmButton.onClick.AddListener(
                ConfirmSaveAndContinue
            );
        }

        if (saveCompleteCloseButton != null)
        {
            saveCompleteCloseButton.onClick.AddListener(
                ConfirmSaveAndContinue
            );
        }

        if (feedbackPanel != null)
            feedbackPanel.SetActive(false);

        if (saveCompletePanel != null)
            saveCompletePanel.SetActive(false);
    }

    // BasisController가 서버 응답을 받은 뒤 호출
    public void ShowFeedbackResult(
        TurnSubmitData result,
        ScenarioRuntimeData scenario)
    {
        if (result?.turn_evaluation?.scorecard == null ||
            scenario == null)
        {
            SetMessage("턴 피드백 데이터가 없습니다.");
            return;
        }

        lastResult = result;
        submittedScenario = scenario;
        FinalEvaluation = result.final_evaluation;

        CaptureTurnSummary(scenario);
        ApplyFeedback(result.turn_evaluation);

        if (saveCompletePanel != null)
            saveCompletePanel.SetActive(false);

        if (feedbackPanel != null)
        {
            feedbackPanel.SetActive(true);
            feedbackPanel.transform.SetAsLastSibling();
        }

        SetMessage("");
        SetButtonsInteractable(true);
    }

    private void ApplyFeedback(
        TurnEvaluation evaluation)
    {
        TurnScorecard card = evaluation.scorecard;

        SetText(
            titleText,
            $"TURN {evaluation.turn_no} 피드백 결과"
        );

        bool scored =
            card.status == "SCORED";

        SetText(
            totalScoreText,
            scored
                ? $"{card.turn_score:0.0} / 5.0"
                : "평가 확인 필요"
        );

        if (totalScoreStars != null)
        {
            totalScoreStars.SetScore(
                scored ? card.turn_score : 0f
            );
        }

        JObject feedback =
            card.feedback as JObject;

        SetText(
            goodPointsText,
            FormatPoints(
                feedback?["good_points"],
                "이번 평가에서 확인된 잘한 점이 없습니다."
            )
        );

        SetText(
            missedPointsText,
            FormatPoints(
                feedback?["missed_points"],
                "이번 평가에서 확인된 놓친 점이 없습니다."
            )
        );

        SetText(
            explanationText,
            feedback?["explanation"]?.ToString()
            ??
            (
                card.feedback?.Type ==
                    JTokenType.String
                    ? card.feedback.ToString()
                    : "피드백 내용이 없습니다."
            )
        );

        int totalTurns =
            submittedScenario.turnData.progress.total_turns;

        SetText(
            turnProgressText,
            $"TURN {evaluation.turn_no} / {totalTurns}"
        );

        ApplyMetricScores(card.metrics);
    }

    private void ApplyMetricScores(
        TurnMetric[] metrics)
    {
        if (metricSlots == null)
            return;

        foreach (MetricSlot slot in metricSlots)
        {
            if (slot == null)
                continue;

            TurnMetric metric =
                metrics == null
                    ? null
                    : Array.Find(
                        metrics,
                        item =>
                            item != null &&
                            item.metric ==
                                slot.metric.ToString()
                    );

            bool unavailable = metric == null;

            if (metric?.penalties != null)
            {
                unavailable |= Array.Exists(
                    metric.penalties,
                    penalty =>
                        penalty != null &&
                        penalty.cause ==
                            "LLM_UNAVAILABLE"
                );
            }

            float score =
                unavailable
                    ? 0f
                    : Mathf.Clamp(
                        metric.score,
                        0f,
                        5f
                    );

            SetText(
                slot.scoreText,
                unavailable
                    ? "평가 불가"
                    : $"{score:0.0}"
            );

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

            if (slot.stars != null)
                slot.stars.SetScore(score);

            SetText(
                slot.reasonText,
                unavailable
                    ? ""
                    : metric.reason ?? ""
            );
        }
    }

    private void CaptureTurnSummary(
        ScenarioRuntimeData scenario)
    {
        ClearChildren(orderContent);

        IReadOnlyList<ScenarioOrderSummary> orders =
            orderController != null
                ? orderController.CurrentTurnOrders
                : null;

        bool hasOrders =
            orders != null &&
            orders.Count > 0;

        if (emptyOrderText != null)
        {
            emptyOrderText.gameObject.SetActive(!hasOrders);
            emptyOrderText.text =
                "이번 턴에 주문한 종목이 없습니다.";
        }

        if (hasOrders &&
            orderContent != null &&
            orderPrefab != null)
        {
            foreach (ScenarioOrderSummary order in orders)
            {
                if (order == null)
                    continue;

                TurnEndOrderItem item =
                    Instantiate(
                        orderPrefab,
                        orderContent
                    );

                item.SetData(order);
            }
        }

        PortfolioInfo portfolio =
            scenario.turnData?.portfolio;

        SetText(
            cashWeightText,
            portfolio != null
                ? $"{portfolio.cash_weight_pct:0.0}%"
                : "-"
        );

        if (portfolio != null)
        {
            float investmentWeight = Mathf.Clamp(
                100f - portfolio.cash_weight_pct,
                0f,
                100f
            );

            SetText(
                investmentWeightText,
                investmentWeight.ToString("0.0") + "%"
            );
        }
        else
        {
            SetText(investmentWeightText, "-");
        }
    }

    // 피드백 패널의 다음 TURN으로 버튼에서 실행
    public void OpenSaveCompletePanel()
    {
        if (loadingNextTurn ||
            lastResult?.turn_evaluation == null ||
            submittedScenario == null)
        {
            return;
        }

        int completedTurn =
            lastResult.turn_evaluation.turn_no;

        string currentDate =
            submittedScenario.turnData?
                .progress?.market_date;

        string nextDate =
            submittedScenario.turnData?
                .progress?.next_market_date;

        SetText(
            savedTitleText,
            $"TURN {completedTurn} 기록이 저장되었습니다."
        );

        SetText(
            savedDescriptionText,
            lastResult.next_turn.HasValue
                ? "입력한 투자 판단과 근거가 저장되었습니다.\n" +
                  "다음 시장으로 이동합니다."
                : "입력한 투자 판단과 근거가 저장되었습니다.\n" +
                  "모든 턴을 완료했습니다."
        );

        SetText(
            savedCurrentDateText,
            FormatDate(currentDate)
        );

        SetText(
            savedCurrentTurnText,
            $"TURN {completedTurn} 시작"
        );

        bool hasNextTurn = lastResult.next_turn.HasValue;

        SetText(
            savedNextDateText,
            hasNextTurn ? FormatDate(nextDate) : "-"
        );

        SetText(
            savedNextTurnText,
            hasNextTurn
                ? $"TURN {lastResult.next_turn.Value} 시작"
                : "시나리오 완료"
        );

        if (feedbackPanel != null)
            feedbackPanel.SetActive(false);

        if (saveCompletePanel != null)
        {
            saveCompletePanel.SetActive(true);
            saveCompletePanel.transform.SetAsLastSibling();
        }
    }

    // 저장 완료 패널의 확인 버튼에서 실행
    public void ConfirmSaveAndContinue()
    {
        if (loadingNextTurn || lastResult == null)
            return;

        if (!lastResult.next_turn.HasValue)
        {
            if (saveCompletePanel != null)
                saveCompletePanel.SetActive(false);

            if (onFinalResultRequested != null &&
                onFinalResultRequested
                    .GetPersistentEventCount() > 0)
            {
                onFinalResultRequested.Invoke();
            }
            else
            {
                SetMessage(
                    "시나리오가 완료되었습니다. " +
                    "최종 결과 화면을 연결하세요."
                );
            }

            return;
        }

        if (playController == null ||
            submittedScenario?.session == null)
        {
            SetMessage(
                "ScenarioPlayController 연결을 확인하세요."
            );

            return;
        }

        loadingNextTurn = true;
        SetButtonsInteractable(false);
        SetNextTurnLoading(true);

        StartCoroutine(LoadNextTurn());
    }

    private IEnumerator LoadNextTurn()
    {
        SetMessage("다음 턴을 불러오는 중...");

        string sessionId =
            submittedScenario.session.session_id;

        string url =
            ServerConfig.HttpBaseUrl +
            "/api/sessions/" +
            UnityWebRequest.EscapeURL(sessionId) +
            "/turn";

        using (UnityWebRequest request =
               UnityWebRequest.Get(url))
        {
            request.timeout = 20;

            yield return request.SendWebRequest();

            CurrentTurnResponse response =
                Parse<CurrentTurnResponse>(
                    request.downloadHandler.text
                );

            bool success =
                request.result ==
                    UnityWebRequest.Result.Success &&
                response?.status == "ok" &&
                response.data?.progress != null;

            if (!success)
            {
                loadingNextTurn = false;
                SetButtonsInteractable(true);
                SetNextTurnLoading(false);

                SetMessage(
                    response?.message ??
                    "다음 턴 조회에 실패했습니다."
                );

                Debug.LogError(
                    $"[Next Turn] HTTP " +
                    $"{request.responseCode}\n" +
                    request.downloadHandler.text
                );

                yield break;
            }

            submittedScenario.turnData =
                response.data;

            if (response.data.session != null)
            {
                submittedScenario.session =
                    response.data.session;
            }

            playController.Initialize(
                submittedScenario
            );

            if (feedbackPanel != null)
                feedbackPanel.SetActive(false);

            if (saveCompletePanel != null)
                saveCompletePanel.SetActive(false);

            lastResult = null;
            loadingNextTurn = false;

            SetButtonsInteractable(true);
            SetNextTurnLoading(false);
            SetMessage("");
        }
    }

    private void SetNextTurnLoading(bool value)
    {
        if (loadingPanel == null)
            return;

        loadingPanel.SetActive(value);
        if (value)
            loadingPanel.transform.SetAsLastSibling();
    }

    private void SetButtonsInteractable(bool value)
    {
        if (nextTurnButton != null)
            nextTurnButton.interactable = value;

        if (saveCompleteConfirmButton != null)
            saveCompleteConfirmButton.interactable = value;

        if (saveCompleteCloseButton != null)
            saveCompleteCloseButton.interactable = value;
    }

    private static string FormatPoints(
        JToken token,
        string emptyMessage)
    {
        JArray items = token as JArray;

        if (items == null || items.Count == 0)
            return emptyMessage;

        List<string> lines =
            new List<string>();

        foreach (JToken item in items)
        {
            string value = item?.ToString();

            if (!string.IsNullOrWhiteSpace(value))
                lines.Add("• " + value);
        }

        return lines.Count > 0
            ? string.Join("\n", lines)
            : emptyMessage;
    }

    private static string FormatDate(
        string isoDate)
    {
        if (DateTime.TryParse(
                isoDate,
                out DateTime date))
        {
            return date.ToString("yyyy.MM.dd");
        }

        return string.IsNullOrWhiteSpace(isoDate)
            ? "-"
            : isoDate;
    }

    private static T Parse<T>(string json)
        where T : class
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(
                json
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[TurnFeedback] JSON 오류: " +
                exception.Message
            );

            return null;
        }
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

    private static void ClearChildren(
        Transform content)
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                content.GetChild(i).gameObject
            );
        }
    }

    private void OnDestroy()
    {
        if (nextTurnButton != null)
        {
            nextTurnButton.onClick.RemoveListener(
                OpenSaveCompletePanel
            );
        }

        if (saveCompleteConfirmButton != null)
        {
            saveCompleteConfirmButton.onClick.RemoveListener(
                ConfirmSaveAndContinue
            );
        }

        if (saveCompleteCloseButton != null)
        {
            saveCompleteCloseButton.onClick.RemoveListener(
                ConfirmSaveAndContinue
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
        public Slider scoreSlider;
        public ScenarioStarRatingView stars;
        public TMP_Text reasonText;
    }
}

