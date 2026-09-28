using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class TurnEndBasisController : MonoBehaviour
{
    [Header("서버")]

    [Header("연결")]
    [SerializeField]
    private ScenarioOrderController orderController;

    [SerializeField]
    private ScenarioTurnFeedbackController feedbackController;

    [Header("패널")]
    [SerializeField]
    private GameObject basisPanel;

    [Header("스크롤 위치 초기화")]
    [Tooltip("근거 입력 패널 ScrollRect의 Content를 연결")]
    [SerializeField]
    private RectTransform basisContent;

    [Header("로딩")]
    [SerializeField]
    private GameObject loadingPanel;

    [Header("제출 버튼")]
    [SerializeField]
    private Button submitButton;

    [Header("이번 턴 주문 목록")]
    [SerializeField]
    private Transform orderContent;

    [SerializeField]
    private TurnEndOrderItem orderPrefab;

    [SerializeField]
    private TMP_Text emptyOrderText;

    [Header("상단 제목")]
    [SerializeField]
    private TMP_Text turnTitleText;

    [Header("선택형 질문 영역 - 최대 5개")]
    [SerializeField]
    private QuestionSlot[] questionSlots;

    [Header("선택 버튼 프리팹")]
    [SerializeField]
    private BasisButtonItem basisButtonPrefab;

    [Header("자유 근거 입력")]
    [SerializeField]
    private TMP_InputField reasoningInput;

    [SerializeField]
    private int maxReasoningLength = 500;

    [Header("상태 및 경고 문구")]
    [SerializeField]
    private TMP_Text warningText;

    private readonly List<QuestionRuntime> questions =
        new List<QuestionRuntime>();

    private readonly HashSet<string> submittedTurns =
        new HashSet<string>();

    private QuestionInfo freeTextQuestion;
    private bool submitting;

    private void Awake()
    {
        if (submitButton != null)
            submitButton.onClick.AddListener(SubmitBasis);

        if (reasoningInput != null)
        {
            reasoningInput.contentType =
                TMP_InputField.ContentType.Standard;

            reasoningInput.lineType =
                TMP_InputField.LineType.MultiLineNewline;

            reasoningInput.characterLimit =
                Mathf.Max(1, maxReasoningLength);
        }

        if (basisPanel != null)
            basisPanel.SetActive(false);

    }

    // 시나리오 화면의 턴 종료 버튼에 연결
    public void OpenTurnEndBasis()
    {
        if (submitting)
            return;

        ScenarioRuntimeData scenario =
            ScenarioDataManager.Instance?.SelectedScenario;

        if (scenario?.turnData?.progress == null)
        {
            SetWarning("진행 중인 시나리오를 찾을 수 없습니다.");
            return;
        }

        if (basisPanel == null)
        {
            Debug.LogError(
                "[TurnEndBasis] Basis Panel을 연결하세요.",
                this
            );

            return;
        }

        int turn = scenario.turnData.progress.current_turn;
        string turnKey = CreateTurnKey(scenario, turn);

        if (submittedTurns.Contains(turnKey))
        {
            SetWarning("이미 제출한 턴입니다.");
            return;
        }

        basisPanel.SetActive(true);
        basisPanel.transform.SetAsLastSibling();

        SetText(
            turnTitleText,
            $"TURN {turn} 종료 · 근거 입력"
        );

        if (reasoningInput != null)
            reasoningInput.SetTextWithoutNotify("");

        SetWarning("");

        CreateOrderItems();
        CreateQuestionItems(scenario.turnData.questions);
        SetFreeTextQuestion(scenario.turnData.questions);

        ResetContentY(basisContent);
    }

    private static void ResetContentY(RectTransform content)
    {
        if (content == null)
            return;

        Canvas.ForceUpdateCanvases();
        Vector2 position = content.anchoredPosition;
        position.y = 0f;
        content.anchoredPosition = position;
    }

    // Basis 패널의 제출하기 버튼에서 실행
    public void SubmitBasis()
    {
        if (submitting)
            return;

        ScenarioRuntimeData scenario =
            ScenarioDataManager.Instance?.SelectedScenario;

        if (scenario?.session == null ||
            scenario.turnData?.progress == null)
        {
            SetWarning("진행 중인 시나리오가 없습니다.");
            return;
        }

        if (feedbackController == null)
        {
            SetWarning(
                "ScenarioTurnFeedbackController를 연결하세요."
            );

            return;
        }

        int turn = scenario.turnData.progress.current_turn;
        string turnKey = CreateTurnKey(scenario, turn);

        if (submittedTurns.Contains(turnKey))
        {
            SetWarning("이미 제출한 턴입니다.");
            return;
        }

        if (!TryBuildAnswers(
                out List<ScenarioTurnAnswer> answers,
                out string error))
        {
            SetWarning(error);
            return;
        }

        submittedTurns.Add(turnKey);
        SetSubmitting(true);
        SetWarning("판단 근거를 저장하고 분석하고 있습니다...");

        StartCoroutine(
            SubmitRoutine(
                scenario,
                turn,
                turnKey,
                answers
            )
        );
    }

    private IEnumerator SubmitRoutine(
        ScenarioRuntimeData scenario,
        int submittedTurn,
        string turnKey,
        List<ScenarioTurnAnswer> answers)
    {
        string sessionId = scenario.session.session_id;

        string url =
            ServerConfig.HttpBaseUrl +
            "/api/sessions/" +
            UnityWebRequest.EscapeURL(sessionId) +
            "/turn/submit";

        string json = JsonConvert.SerializeObject(
            new ScenarioTurnSubmitRequest
            {
                answers = answers
            }
        );

        using (UnityWebRequest request =
               new UnityWebRequest(
                   url,
                   UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler =
                new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(json)
                );

            request.downloadHandler =
                new DownloadHandlerBuffer();

            request.timeout = 180;

            request.SetRequestHeader(
                "Content-Type",
                "application/json"
            );

            request.SetRequestHeader(
                "Accept",
                "application/json"
            );

            yield return request.SendWebRequest();

            TurnSubmitResponse response =
                Parse<TurnSubmitResponse>(
                    request.downloadHandler.text
                );

            bool success =
                request.result ==
                    UnityWebRequest.Result.Success &&
                response?.status == "ok" &&
                response.data?.turn_evaluation?.scorecard != null &&
                response.data.turn_evaluation.turn_no ==
                    submittedTurn;

            if (!success)
            {
                bool canRetry =
                    request.responseCode == 400 ||
                    request.responseCode == 422;

                if (canRetry)
                    submittedTurns.Remove(turnKey);

                SetSubmitting(false);

                SetWarning(
                    response?.message ??
                    (
                        canRetry
                            ? "입력한 답변을 확인하세요."
                            : "서버 처리 결과를 확인할 수 없습니다."
                    )
                );

                Debug.LogError(
                    $"[Turn Submit] HTTP " +
                    $"{request.responseCode}\n" +
                    request.downloadHandler.text
                );

                yield break;
            }

            // 서버는 제출 성공 시 세션을 다음 턴으로 진행합니다.
            if (response.data.session != null)
                scenario.session = response.data.session;

            SetSubmitting(false);
            SetWarning("");

            CloseBasisPanel();

            // 피드백 컨트롤러에는 결과만 전달
            feedbackController.ShowFeedbackResult(
                response.data,
                scenario
            );
        }
    }

    private void CreateOrderItems()
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

        if (!hasOrders ||
            orderPrefab == null ||
            orderContent == null)
        {
            return;
        }

        foreach (ScenarioOrderSummary order in orders)
        {
            if (order == null)
                continue;

            TurnEndOrderItem item =
                Instantiate(orderPrefab, orderContent);

            item.SetData(order);
        }
    }

    private void CreateQuestionItems(
        QuestionInfo[] sourceQuestions)
    {
        questions.Clear();

        if (questionSlots != null)
        {
            foreach (QuestionSlot slot in questionSlots)
            {
                if (slot == null)
                    continue;

                if (slot.root != null)
                    slot.root.SetActive(false);

                ClearChildren(slot.optionContent);
            }
        }

        if (sourceQuestions == null ||
            questionSlots == null)
        {
            return;
        }

        int slotIndex = 0;

        foreach (QuestionInfo question in sourceQuestions)
        {
            if (question == null ||
                question.type == "free")
            {
                continue;
            }

            if (slotIndex >= questionSlots.Length)
            {
                Debug.LogWarning(
                    "[TurnEndBasis] 질문 슬롯이 부족합니다.",
                    this
                );

                break;
            }

            QuestionSlot slot = questionSlots[slotIndex];
            slotIndex++;

            if (slot == null)
                continue;

            if (slot.root != null)
                slot.root.SetActive(true);

            SetText(
                slot.questionText,
                $"Q{slotIndex}. {question.text}"
            );

            int maxSelect =
                question.type == "single"
                    ? 1
                    : Mathf.Max(1, question.max_select);

            SetText(
                slot.maxSelectText,
                maxSelect == 1
                    ? "판단 태그 (단일 선택)"
                    : $"판단 태그 (최대 {maxSelect}개 선택)"
            );

            QuestionRuntime runtime =
                new QuestionRuntime
                {
                    question = question,
                    maxSelect = maxSelect
                };

            questions.Add(runtime);

            if (question.options == null ||
                slot.optionContent == null ||
                basisButtonPrefab == null)
            {
                continue;
            }

            foreach (string option in question.options)
            {
                BasisButtonItem item =
                    Instantiate(
                        basisButtonPrefab,
                        slot.optionContent
                    );

                item.SetData(
                    option,
                    clicked =>
                        ToggleOption(runtime, clicked)
                );

                runtime.buttons.Add(item);
            }
        }
    }

    private void SetFreeTextQuestion(
        QuestionInfo[] sourceQuestions)
    {
        freeTextQuestion = null;

        if (sourceQuestions != null)
        {
            foreach (QuestionInfo question in sourceQuestions)
            {
                if (question != null &&
                    question.type == "free")
                {
                    freeTextQuestion = question;
                    break;
                }
            }
        }

        if (reasoningInput != null)
        {
            reasoningInput.characterLimit =
                Mathf.Max(1, maxReasoningLength);
        }
    }

    private void ToggleOption(
        QuestionRuntime runtime,
        BasisButtonItem clicked)
    {
        if (runtime == null || clicked == null)
            return;

        if (clicked.IsSelected)
        {
            clicked.SetSelected(false);

            runtime.selectedOptions.Remove(
                clicked.Option
            );

            SetWarning("");
            return;
        }

        if (runtime.maxSelect == 1)
        {
            foreach (BasisButtonItem button in
                     runtime.buttons)
            {
                if (button != null)
                    button.SetSelected(false);
            }

            runtime.selectedOptions.Clear();
        }
        else if (
            runtime.selectedOptions.Count >=
            runtime.maxSelect)
        {
            SetWarning(
                $"이 항목은 최대 " +
                $"{runtime.maxSelect}개까지 선택할 수 있습니다."
            );

            return;
        }

        clicked.SetSelected(true);
        runtime.selectedOptions.Add(clicked.Option);

        SetWarning("");
    }

    private bool TryBuildAnswers(
        out List<ScenarioTurnAnswer> answers,
        out string error)
    {
        answers = new List<ScenarioTurnAnswer>();
        error = "";

        ScenarioRuntimeData scenario =
            ScenarioDataManager.Instance?.SelectedScenario;

        QuestionInfo[] source =
            scenario?.turnData?.questions;

        if (source == null || source.Length == 0)
        {
            error = "질문 데이터가 없습니다.";
            return false;
        }

        foreach (QuestionInfo question in source)
        {
            if (question == null)
                continue;

            if (question.type == "free")
            {
                string freeText =
                    reasoningInput != null
                        ? reasoningInput.text.Trim()
                        : "";

                if (freeTextQuestion == null ||
                    freeTextQuestion.question_id !=
                        question.question_id ||
                    string.IsNullOrWhiteSpace(freeText))
                {
                    error = "판단 근거를 입력하세요.";
                    return false;
                }

                answers.Add(
                    new ScenarioTurnAnswer
                    {
                        question_id =
                            question.question_id,

                        selected =
                            Array.Empty<string>(),

                        text = freeText
                    }
                );

                continue;
            }

            QuestionRuntime runtime =
                questions.Find(
                    item =>
                        item.question.question_id ==
                        question.question_id
                );

            if (runtime == null ||
                runtime.selectedOptions.Count == 0)
            {
                error =
                    $"답변을 선택하세요: {question.text}";

                return false;
            }

            if (runtime.selectedOptions.Count >
                runtime.maxSelect)
            {
                error =
                    $"최대 {runtime.maxSelect}개까지 " +
                    "선택할 수 있습니다.";

                return false;
            }

            answers.Add(
                new ScenarioTurnAnswer
                {
                    question_id =
                        question.question_id,

                    selected =
                        runtime.selectedOptions.ToArray(),

                    text = ""
                }
            );
        }

        return true;
    }

    public void CloseBasisPanel()
    {
        if (basisPanel != null)
            basisPanel.SetActive(false);
    }

    private void SetSubmitting(bool value)
    {
        submitting = value;

        if (submitButton != null)
            submitButton.interactable = !value;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(value);
            if (value)
                loadingPanel.transform.SetAsLastSibling();
        }
    }

    private void SetWarning(string message)
    {
        SetText(warningText, message);
    }

    private static string CreateTurnKey(
        ScenarioRuntimeData scenario,
        int turn)
    {
        return scenario.session.session_id +
               ":" +
               turn;
    }

    private static T Parse<T>(string json)
        where T : class
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[TurnEndBasis] JSON 오류: " +
                exception.Message
            );

            return null;
        }
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

    private static void SetText(
        TMP_Text target,
        string value)
    {
        if (target != null)
            target.text = value;
    }

    private void OnDestroy()
    {
        if (submitButton != null)
            submitButton.onClick.RemoveListener(
                SubmitBasis
            );
    }

    [Serializable]
    private class QuestionSlot
    {
        public GameObject root;
        public TMP_Text questionText;
        public TMP_Text maxSelectText;
        public Transform optionContent;
    }

    private class QuestionRuntime
    {
        public QuestionInfo question;
        public int maxSelect;

        public readonly List<string> selectedOptions =
            new List<string>();

        public readonly List<BasisButtonItem> buttons =
            new List<BasisButtonItem>();
    }
}

// 서버 제출 요청 및 응답 모델

[Serializable]
public class ScenarioTurnAnswer
{
    public string question_id;
    public string[] selected;
    public string text;
}

[Serializable]
public class ScenarioTurnSubmitRequest
{
    public List<ScenarioTurnAnswer> answers;
}

[Serializable]
public class TurnSubmitResponse
{
    public string status;
    public string message;
    public TurnSubmitData data;
}

[Serializable]
public class TurnSubmitData
{
    public SessionInfo session;
    public TurnEvaluation turn_evaluation;
    public int? next_turn;
    public JToken final_evaluation;
}

[Serializable]
public class TurnEvaluation
{
    public string evaluation_id;
    public string session_id;
    public int turn_no;
    public TurnScorecard scorecard;
}

[Serializable]
public class TurnScorecard
{
    public string status;
    public float turn_score;
    public TurnMetric[] metrics;
    public JToken feedback;
}

[Serializable]
public class TurnMetric
{
    public string metric;
    public float score;
    public string reason;
    public TurnPenalty[] penalties;
}

[Serializable]
public class TurnPenalty
{
    public float amount;
    public string cause;
    public string evidence;
}

