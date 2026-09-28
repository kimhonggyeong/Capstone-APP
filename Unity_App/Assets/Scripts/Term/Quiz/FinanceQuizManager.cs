using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinanceQuizManager : MonoBehaviour
{
    [Header("JSON")]
    [SerializeField] private TextAsset quizJson;

    [Header("Panels")]
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private GameObject quizTestPanel;
    [SerializeField] private GameObject answerPanel;
    [SerializeField] private GameObject quizIntroPanel;

    [Header("Quiz Intro UI")]
    [SerializeField] private TextMeshProUGUI introCategoryText;
    [SerializeField] private TextMeshProUGUI introTypeText;
    [SerializeField] private TextMeshProUGUI introMessageText;
    [SerializeField] private Button introStartButton;

    [Header("Quiz Top UI")]
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private Image difficultyBackgroundImage;

    [Header("Progress UI")]
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Slider progressSlider;

    [Header("Quiz UI")]
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private Button[] optionButtons;
    [SerializeField] private TextMeshProUGUI[] optionTexts;

    [Header("Check Button")]
    [SerializeField] private Button checkAnswerButton;

    [Header("Answer UI")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI correctAnswerText;
    [SerializeField] private TextMeshProUGUI explanationText;
    [SerializeField] private Button nextButton;

    [Header("Result Panel")]
    [SerializeField] private GameObject quizResultPanel;
    [SerializeField] private TextMeshProUGUI resultCorrectCountText;
    [SerializeField] private TextMeshProUGUI resultAccuracyText;
    [SerializeField] private TextMeshProUGUI resultBestStreakText;
    [SerializeField] private Button retryButton;

    [Header("Fail Panel")]
    [SerializeField] private GameObject quizFailPanel;
    [SerializeField] private Button failRetryButton;

    [Header("Difficulty Dropdown")]
    [SerializeField] private TMP_Dropdown difficultyDropdown;

    [Header("Quiz Select Buttons")]
    [SerializeField] private FinanceQuizButton[] quizButtons;

    [Header("Quiz Status")]
    [SerializeField] private TextMeshProUGUI correctCountText;
    [SerializeField] private TextMeshProUGUI wrongCountText;
    [SerializeField] private TextMeshProUGUI remainingCountText;

    private readonly List<FinanceQuizData> allQuizzes = new();
    private List<FinanceQuizData> currentQuizzes = new();

    private string currentCategory;
    private string currentType;
    private int currentIndex;
    private int selectedIndex = -1;
    private string pendingCategory;
    private string pendingType;
    private int correctCount;
    private int wrongCount;
    private int currentStreak;
    private int bestStreak;

    private const int FailWrongLimit = 5;
    private const string AllOption = "전체";

    private readonly Color normalOptionColor = new Color32(0xDD, 0xCA, 0xB2, 0xFF);

    private readonly Color selectedOptionColor = new Color32(0xF6, 0x6B, 0x24, 0xFF);
    private void Start()
    {
        LoadQuizJson();
        SetupDifficultyDropdown();

        InitializeQuizButtons();

        CloseQuizWindow();

        if (checkAnswerButton != null)
        {
            checkAnswerButton.onClick.RemoveAllListeners();
            checkAnswerButton.onClick.AddListener(CheckAnswer);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(NextQuestion);
        }

        if (introStartButton != null)
        {
            introStartButton.onClick.RemoveAllListeners();
            introStartButton.onClick.AddListener(StartQuizFromIntro);
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(RetryQuiz);
        }

        if (failRetryButton != null)
        {
            failRetryButton.onClick.RemoveAllListeners();
            failRetryButton.onClick.AddListener(RetryQuiz);
        }
    }

    private void LoadQuizJson()
    {
        if (quizJson == null)
        {
            Debug.LogError("quizJson이 비어 있음. Inspector에 JSON TextAsset 넣어야 함.");
            return;
        }

        FinanceQuizData[] loadedData =
            JsonArrayHelper.FromJsonArray<FinanceQuizData>(quizJson.text);

        if (loadedData == null || loadedData.Length == 0)
        {
            Debug.LogError("퀴즈 JSON 로드 실패");
            return;
        }

        allQuizzes.Clear();
        allQuizzes.AddRange(loadedData);

        Debug.Log($"퀴즈 로드 완료: {allQuizzes.Count}개");
    }

    private void SetupDifficultyDropdown()
    {
        if (difficultyDropdown == null)
            return;

        List<string> options = new List<string>
    {
        "초급",
        "중급",
        "고급",
        AllOption
    };

        difficultyDropdown.ClearOptions();
        difficultyDropdown.AddOptions(options);
        difficultyDropdown.value = 0; // 처음 기본값: 초급
        difficultyDropdown.RefreshShownValue();
    }

    // 시작 버튼에서 이 함수 연결
    // 예: StartQuizByNumber(1)
    public void StartQuizByNumber(int number)
    {
        QuizTarget target = GetQuizTargetByNumber(number);

        if (target == null)
        {
            Debug.LogWarning($"잘못된 퀴즈 번호: {number}");
            return;
        }

        OpenQuizIntroPanel(target.category, target.type);

        VibrateShort();
    }

    public void VibrateShort()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    private void OpenQuizIntroPanel(string category, string type)
    {
        pendingCategory = category;
        pendingType = type;

        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (quizIntroPanel != null)
            quizIntroPanel.SetActive(true);

        if (quizTestPanel != null)
            quizTestPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        SetText(introCategoryText, category);
        SetText(introTypeText, GetTypeDisplayName(type));
        SetText(introMessageText, GetIntroMessage(category, type));
    }

    private void StartQuizFromIntro()
    {
        if (string.IsNullOrEmpty(pendingCategory) || string.IsNullOrEmpty(pendingType))
        {
            Debug.LogWarning("시작할 퀴즈 정보가 없습니다.");
            return;
        }

        if (quizIntroPanel != null)
            quizIntroPanel.SetActive(false);

        StartQuiz(pendingCategory, pendingType);
    }

    private void StartQuiz(string category, string type)
    {
        currentCategory = category;
        currentType = type;
        currentIndex = 0;
        selectedIndex = -1;
        correctCount = 0;
        wrongCount = 0;
        currentStreak = 0;
        bestStreak = 0;

        string selectedDifficulty = GetSelectedDifficulty();

        currentQuizzes = allQuizzes
            .Where(q => q.category == currentCategory)
            .Where(q => q.type == currentType)
            .Where(q => selectedDifficulty == AllOption || q.difficulty == selectedDifficulty)
            .ToList();

        if (currentQuizzes.Count == 0)
        {
            Debug.LogWarning($"문제 없음: {currentCategory} / {currentType} / {selectedDifficulty}");
            return;
        }

        if (quizPanel != null)
            quizPanel.SetActive(true);

        if (quizTestPanel != null)
            quizTestPanel.SetActive(true);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (quizResultPanel != null)
            quizResultPanel.SetActive(false);

        if (quizFailPanel != null)
            quizFailPanel.SetActive(false);

        SetupProgress();
        UpdateQuizStatus();
        ShowQuestion();
    }
    private void SetupProgress()
    {
        if (progressSlider != null)
        {
            progressSlider.minValue = 0;
            progressSlider.maxValue = currentQuizzes.Count;
            progressSlider.value = 0;
            progressSlider.wholeNumbers = true;
            progressSlider.interactable = false;
        }

        UpdateProgress();
    }

    private void UpdateProgress()
    {
        int currentNumber = currentIndex + 1;
        int totalCount = currentQuizzes != null ? currentQuizzes.Count : 0;

        if (progressText != null)
        {
            progressText.text = $"{currentNumber} / {totalCount}";
        }

        if (progressSlider != null)
        {
            progressSlider.value = currentNumber;
        }
    }
    private void ShowQuestion()
    {
        if (currentQuizzes == null || currentQuizzes.Count == 0)
            return;

        FinanceQuizData quiz = currentQuizzes[currentIndex];

        selectedIndex = -1;

        SetText(categoryText, quiz.category);
        SetText(difficultyText, quiz.difficulty);
        SetDifficultyBackgroundColor(quiz.difficulty);
        SetText(questionText, quiz.question);

        UpdateProgress();

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (checkAnswerButton != null)
            checkAnswerButton.interactable = false;

        if (nextButton != null)
            nextButton.gameObject.SetActive(false);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int optionIndex = i;

            if (i < quiz.options.Count)
            {
                optionButtons[i].gameObject.SetActive(true);
                optionButtons[i].interactable = true;
                optionButtons[i].image.color = normalOptionColor;

                optionTexts[i].text = quiz.options[i];

                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() =>
                {
                    SelectOption(optionIndex);
                });
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private void SetDifficultyBackgroundColor(string difficulty)
    {
        if (difficultyBackgroundImage == null)
            return;

        string colorHex;

        switch (difficulty)
        {
            case "초급":
                colorHex = "#C6F6D5";
                break;

            case "중급":
                colorHex = "#E9D8FD";
                break;

            default:
                colorHex = "#FFFFFF";
                break;
        }

        if (ColorUtility.TryParseHtmlString(colorHex, out Color color))
        {
            difficultyBackgroundImage.color = color;
        }
    }
    private void SelectOption(int index)
    {
        selectedIndex = index;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].image.color = normalOptionColor;
        }

        optionButtons[index].image.color = selectedOptionColor;

        if (checkAnswerButton != null)
            checkAnswerButton.interactable = true;
    }

    public void CheckAnswer()
    {
        if (selectedIndex < 0)
        {
            Debug.Log("보기를 먼저 선택해야 함.");
            return;
        }

        FinanceQuizData quiz = currentQuizzes[currentIndex];

        bool isCorrect = selectedIndex == quiz.answerIndex;
        UpdateQuizResultStats(isCorrect);

        if (answerPanel != null)
            answerPanel.SetActive(true);

        SetText(resultText, isCorrect ? "정답!" : "오답!");
        SetResultTextColor(isCorrect);

        SetText(correctAnswerText, $"정답: {quiz.options[quiz.answerIndex]}");
        SetText(explanationText, quiz.explanation);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].interactable = false;

            if (i == quiz.answerIndex)
            {
                optionButtons[i].image.color = selectedOptionColor;
            }
            else if (i == selectedIndex)
            {
                optionButtons[i].image.color = selectedOptionColor;
            }
            else
            {
                optionButtons[i].image.color = normalOptionColor;
            }
        }

        if (checkAnswerButton != null)
            checkAnswerButton.interactable = false;

        if (!isCorrect && wrongCount >= FailWrongLimit)
        {
            ShowQuizFailPanel();
            return;
        }

        if (nextButton != null)
            nextButton.gameObject.SetActive(true);
    }

    private void UpdateQuizResultStats(bool isCorrect)
    {
        if (isCorrect)
        {
            correctCount++;
            currentStreak++;

            if (currentStreak > bestStreak)
            {
                bestStreak = currentStreak;
            }
        }
        else
        {
            wrongCount++;
            currentStreak = 0;
        }

        UpdateQuizStatus();
    }

    public void NextQuestion()
    {
        currentIndex++;

        if (currentIndex >= currentQuizzes.Count)
        {
            Debug.Log("해당 타입 문제 끝.");

            ShowQuizResultPanel();
            return;
        }

        ShowQuestion();
    }

    private void ShowQuizResultPanel()
    {
        if (wrongCount >= FailWrongLimit)
        {
            ShowQuizFailPanel();
            return;
        }

        if (quizPanel != null)
            quizPanel.SetActive(true);

        if (quizTestPanel != null)
            quizTestPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (quizResultPanel != null)
            quizResultPanel.SetActive(true);

        if (quizFailPanel != null)
            quizFailPanel.SetActive(false);

        int totalCount = currentQuizzes != null ? currentQuizzes.Count : 0;
        int accuracy = totalCount > 0 ? Mathf.RoundToInt((float)correctCount / totalCount * 100f) : 0;

        SetText(resultCorrectCountText, $"{correctCount}/{totalCount}");
        SetText(resultAccuracyText, $"{accuracy}%");
        SetText(resultBestStreakText, bestStreak.ToString());
    }


    private void ShowQuizFailPanel()
    {
        if (quizPanel != null)
            quizPanel.SetActive(true);

        if (quizTestPanel != null)
            quizTestPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (quizResultPanel != null)
            quizResultPanel.SetActive(false);

        if (quizFailPanel != null)
            quizFailPanel.SetActive(true);
    }

    private void RetryQuiz()
    {
        if (string.IsNullOrEmpty(currentCategory) || string.IsNullOrEmpty(currentType))
        {
            Debug.LogWarning("다시 풀 퀴즈 정보가 없습니다.");
            return;
        }

        if (quizResultPanel != null)
            quizResultPanel.SetActive(false);

        if (quizFailPanel != null)
            quizFailPanel.SetActive(false);

        StartQuiz(currentCategory, currentType);
    }

    public void CloseQuizWindow()
    {
        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (quizIntroPanel != null)
            quizIntroPanel.SetActive(false);

        if (quizTestPanel != null)
            quizTestPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (quizResultPanel != null)
            quizResultPanel.SetActive(false);

        if (quizFailPanel != null)
            quizFailPanel.SetActive(false);
    }

    private string GetTypeDisplayName(string type)
    {
        switch (type)
        {
            case "definition":
                return "1단계: 용어 익히기";

            case "concept":
                return "2단계: 개념 이해하기";

            case "scenario":
                return "3단계: 실전 적용하기";

            default:
                return type;
        }
    }

    private string GetIntroMessage(string category, string type)
    {
        if (category == "저축·예금")
        {
            if (type == "definition")
                return "예금, 적금, 이자 같은\n기본 용어를 배워보자.";
            if (type == "concept")
                return "저축과 이자의 핵심 개념을\n이해해보자.";
            if (type == "scenario")
                return "나에게 맞는 저축 방법을\n판단해보자.";
        }

        if (category == "주식 기초")
        {
            if (type == "definition")
                return "주가, 배당 같은 주식\n기본 용어를 배워보자.";
            if (type == "concept")
                return "주식과 기업을 보는\n기본 개념을 이해해보자.";
            if (type == "scenario")
                return "주식 투자 상황에서\n올바르게 판단해보자.";
        }

        if (category == "ETF·펀드")
        {
            if (type == "definition")
                return "ETF와 펀드의\n기본 용어를 배워보자.";
            if (type == "concept")
                return "ETF와 펀드의 구조와 특징을\n이해해보자.";
            if (type == "scenario")
                return "상황에 맞는 투자 상품을\n판단해보자.";
        }

        if (category == "채권·금리")
        {
            if (type == "definition")
                return "채권과 금리의\n기본 용어를 배워보자.";
            if (type == "concept")
                return "금리와 채권 가격의 관계를\n이해해보자.";
            if (type == "scenario")
                return "금리 변화에 따른 상황을\n판단해보자.";
        }

        if (category == "주식 거래")
        {
            if (type == "definition")
                return "매수, 매도, 호가 같은\n거래 용어를 배워보자.";
            if (type == "concept")
                return "주식 주문과 거래 방식을\n이해해보자.";
            if (type == "scenario")
                return "실제 거래 상황에서\n주문을 판단해보자.";
        }

        if (category == "시장 지표")
        {
            if (type == "definition")
                return "주가지수와 환율 같은\n시장 지표를 배워보자.";
            if (type == "concept")
                return "시장 지표가 의미하는 것을\n이해해보자.";
            if (type == "scenario")
                return "시장 지표를 보고\n상황을 판단해보자.";
        }

        if (category == "자산관리")
        {
            if (type == "definition")
                return "분산투자와 포트폴리오\n용어를 배워보자.";
            if (type == "concept")
                return "자산을 나누고 관리하는\n방법을 이해해보자.";
            if (type == "scenario")
                return "상황에 맞는 자산 관리\n방법을 판단해보자.";
        }

        if (category == "금융 위험·사기 예방")
        {
            if (type == "definition")
                return "금융 위험과 사기 관련\n용어를 배워보자.";
            if (type == "concept")
                return "금융 위험과 사기의 특징을\n이해해보자.";
            if (type == "scenario")
                return "위험한 금융 상황을 구별하고\n대처해보자.";
        }

        return "금융 지식을\n배워보자.";
    }

    public void ResetQuizState()
    {
        currentCategory = null;
        currentType = null;
        currentIndex = 0;
        selectedIndex = -1;
        correctCount = 0;
        wrongCount = 0;
        currentStreak = 0;
        bestStreak = 0;

        if (currentQuizzes != null)
            currentQuizzes.Clear();

        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (quizTestPanel != null)
            quizTestPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (quizResultPanel != null)
            quizResultPanel.SetActive(false);

        if (quizFailPanel != null)
            quizFailPanel.SetActive(false);

        if (difficultyBackgroundImage != null)
            difficultyBackgroundImage.color = Color.white;

        if (progressText != null)
            progressText.text = "0 / 0";

        if (progressSlider != null)
        {
            progressSlider.minValue = 0;
            progressSlider.maxValue = 1;
            progressSlider.value = 0;
            progressSlider.wholeNumbers = true;
            progressSlider.interactable = false;
        }

        SetText(categoryText, "-");
        SetText(difficultyText, "-");
        SetText(questionText, "-");
        SetText(resultText, "-");
        SetText(correctAnswerText, "-");
        SetText(explanationText, "-");
        SetText(resultCorrectCountText, "0/0");
        SetText(resultAccuracyText, "0%");
        SetText(resultBestStreakText, "0");

        if (checkAnswerButton != null)
            checkAnswerButton.interactable = false;

        if (nextButton != null)
            nextButton.gameObject.SetActive(false);

        ResetDifficultyDropdown();
        ResetOptionButtons();
    }

    private void ResetDifficultyDropdown()
    {
        if (difficultyDropdown == null) return;
        if (difficultyDropdown.options == null || difficultyDropdown.options.Count == 0) return;

        difficultyDropdown.SetValueWithoutNotify(0);
        difficultyDropdown.RefreshShownValue();
    }

    private void ResetOptionButtons()
    {
        if (optionButtons == null) return;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue;

            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].interactable = true;
            optionButtons[i].image.color = Color.white;
            optionButtons[i].gameObject.SetActive(false);

            if (optionTexts != null && i < optionTexts.Length && optionTexts[i] != null)
            {
                optionTexts[i].text = "";
            }
        }
    }

    private QuizTarget GetQuizTargetByNumber(int number)
    {
        switch (number)
        {
            case 1: return new QuizTarget("저축·예금", "definition");
            case 2: return new QuizTarget("저축·예금", "concept");
            case 3: return new QuizTarget("저축·예금", "scenario");

            case 4: return new QuizTarget("주식 기초", "definition");
            case 5: return new QuizTarget("주식 기초", "concept");
            case 6: return new QuizTarget("주식 기초", "scenario");

            case 7: return new QuizTarget("ETF·펀드", "definition");
            case 8: return new QuizTarget("ETF·펀드", "concept");
            case 9: return new QuizTarget("ETF·펀드", "scenario");

            case 10: return new QuizTarget("채권·금리", "definition");
            case 11: return new QuizTarget("채권·금리", "concept");
            case 12: return new QuizTarget("채권·금리", "scenario");

            case 13: return new QuizTarget("주식 거래", "definition");
            case 14: return new QuizTarget("주식 거래", "concept");
            case 15: return new QuizTarget("주식 거래", "scenario");

            case 16: return new QuizTarget("시장 지표", "definition");
            case 17: return new QuizTarget("시장 지표", "concept");
            case 18: return new QuizTarget("시장 지표", "scenario");

            case 19: return new QuizTarget("자산관리", "definition");
            case 20: return new QuizTarget("자산관리", "concept");
            case 21: return new QuizTarget("자산관리", "scenario");

            case 22: return new QuizTarget("금융 위험·사기 예방", "definition");
            case 23: return new QuizTarget("금융 위험·사기 예방", "concept");
            case 24: return new QuizTarget("금융 위험·사기 예방", "scenario");

            default:
                return null;
        }
    }

    private string GetSelectedDifficulty()
    {
        if (difficultyDropdown == null)
            return AllOption;

        if (difficultyDropdown.options == null || difficultyDropdown.options.Count == 0)
            return AllOption;

        string value = difficultyDropdown.options[difficultyDropdown.value].text;

        if (string.IsNullOrEmpty(value))
            return AllOption;

        return value;
    }

    private void SetText(TextMeshProUGUI target, string value)
    {
        if (target == null)
            return;

        target.text = string.IsNullOrEmpty(value) ? "-" : value;
    }

    private class QuizTarget
    {
        public string category;
        public string type;

        public QuizTarget(string category, string type)
        {
            this.category = category;
            this.type = type;
        }
    }
    private void SetResultTextColor(bool isCorrect)
    {
        if (resultText == null)
            return;

        string colorHex = isCorrect ? "#2F855A" : "#C53030";

        if (ColorUtility.TryParseHtmlString(colorHex, out Color color))
        {
            resultText.color = color;
        }
    }
    private void InitializeQuizButtons()
    {
        if (quizButtons == null)
            return;

        foreach (FinanceQuizButton quizButton in quizButtons)
        {
            if (quizButton != null)
            {
                quizButton.Initialize(this);
            }
        }
    }
    public void SetupQuizButtonText(
    int number,
    TextMeshProUGUI typeText,
    TextMeshProUGUI messageText)
    {
        QuizTarget target = GetQuizTargetByNumber(number);

        if (target == null)
        {
            Debug.LogWarning($"잘못된 퀴즈 번호: {number}");
            return;
        }

        if (typeText != null)
        {
            typeText.text = GetTypeDisplayName(target.type);
        }

        if (messageText != null)
        {
            messageText.text = GetIntroMessage(
                target.category,
                target.type
            );
        }
    }
    public void StartQuizDirectByNumber(int number)
    {
        QuizTarget target = GetQuizTargetByNumber(number);

        if (target == null)
        {
            Debug.LogWarning($"잘못된 퀴즈 번호: {number}");
            return;
        }

        // IntroPanel을 거치지 않고 바로 시작
        StartQuiz(target.category, target.type);

        VibrateShort();
    }
    private void UpdateQuizStatus()
    {
        int totalCount = currentQuizzes != null ? currentQuizzes.Count : 0;
        int remainingCount = totalCount - correctCount - wrongCount;

        SetText(correctCountText, $"{correctCount}개");
        SetText(wrongCountText, $"{wrongCount}개");
        SetText(remainingCountText, $"{remainingCount}개");
    }
}