using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class FinanceQuizButton : MonoBehaviour
{
    [Header("Quiz")]
    [SerializeField] private int quizNumber = 1;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI introTypeText;
    [SerializeField] private TextMeshProUGUI introMessageText;

    [Header("Changed Button Prefab")]
    [SerializeField] private GameObject changedButtonPrefab;

    public FinanceQuizManager quizManager;

    private static FinanceQuizButton currentSelectedButton;

    private GameObject spawnedChangedButton;

    private void Start()
    {
        Initialize(quizManager);
    }

    public void Initialize(FinanceQuizManager manager)
    {
        quizManager = manager;

        if (quizManager == null)
            return;

        UpdateText();
    }

    private void UpdateText()
    {
        quizManager.SetupQuizButtonText(
            quizNumber,
            introTypeText,
            introMessageText
        );
    }

    public void SelectQuizButton()
    {
        if (currentSelectedButton != null &&
            currentSelectedButton != this)
        {
            currentSelectedButton.ResetButton();
        }

        currentSelectedButton = this;

        ShowChangedButton();
    }

    private void ShowChangedButton()
    {
        if (changedButtonPrefab == null)
        {
            Debug.LogWarning(
                "Changed Button Prefab이 연결되지 않았습니다."
            );
            return;
        }

        if (spawnedChangedButton != null)
            return;

        Transform parent = transform.parent;
        int siblingIndex = transform.GetSiblingIndex();

        spawnedChangedButton = Instantiate(
            changedButtonPrefab,
            parent
        );

        spawnedChangedButton.transform.SetSiblingIndex(
            siblingIndex
        );

        RectTransform oldRect =
            GetComponent<RectTransform>();

        RectTransform newRect =
            spawnedChangedButton.GetComponent<RectTransform>();

        if (oldRect != null && newRect != null)
        {
            newRect.anchorMin = oldRect.anchorMin;
            newRect.anchorMax = oldRect.anchorMax;
            newRect.pivot = oldRect.pivot;

            newRect.anchoredPosition =
                oldRect.anchoredPosition
                + new Vector2(40f, 0f);

            newRect.localScale = Vector3.one;
        }


        // =========================
        // 텍스트 자동 입력
        // =========================

        TextMeshProUGUI[] texts =
            spawnedChangedButton
                .GetComponentsInChildren<TextMeshProUGUI>(true);

        TextMeshProUGUI changedTypeText = null;
        TextMeshProUGUI changedMessageText = null;

        foreach (TextMeshProUGUI text in texts)
        {
            if (text.gameObject.name == "IntroTypeText")
            {
                changedTypeText = text;
            }
            else if (
                text.gameObject.name == "IntroMessageText"
            )
            {
                changedMessageText = text;
            }
        }

        if (quizManager != null)
        {
            quizManager.SetupQuizButtonText(
                quizNumber,
                changedTypeText,
                changedMessageText
            );
        }


        // =========================
        // 시작 버튼 자동 연결
        // =========================

        Button[] buttons =
            spawnedChangedButton
                .GetComponentsInChildren<Button>(true);

        bool foundStartButton = false;

        foreach (Button button in buttons)
        {
            if (button.gameObject.name == "StartButton")
            {
                foundStartButton = true;

                button.onClick.RemoveAllListeners();

                int currentQuizNumber = quizNumber;

                button.onClick.AddListener(() =>
                {
                    if (quizManager == null)
                    {
                        Debug.LogWarning(
                            "FinanceQuizManager가 없습니다."
                        );
                        return;
                    }

                    Debug.Log(
                        $"퀴즈 시작: {currentQuizNumber}"
                    );

                    quizManager.StartQuizDirectByNumber(
                        currentQuizNumber
                    );
                });

                Debug.Log(
                    $"StartButton 연결 완료: Quiz {currentQuizNumber}"
                );

                break;
            }
        }

        if (!foundStartButton)
        {
            Debug.LogWarning(
                "프리팹 내부에서 StartButton을 찾지 못했습니다."
            );
        }


        // 원래 버튼 숨기기
        gameObject.SetActive(false);
    }

    public void ResetButton()
    {
        if (spawnedChangedButton != null)
        {
            Destroy(spawnedChangedButton);
            spawnedChangedButton = null;
        }

        gameObject.SetActive(true);
    }

    public void StartQuiz()
    {
        if (quizManager == null)
        {
            Debug.LogWarning(
                "FinanceQuizManager가 연결되지 않았습니다."
            );
            return;
        }

        quizManager.StartQuizDirectByNumber(quizNumber);
    }

    public void SetQuizNumber(int number)
    {
        quizNumber = number;

        // 이미 Manager가 연결돼 있으면
        // 번호 변경 직후 텍스트도 갱신
        if (quizManager != null)
        {
            UpdateText();
        }
    }
}