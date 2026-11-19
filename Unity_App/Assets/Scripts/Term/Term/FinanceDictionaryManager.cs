using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinanceDictionaryManager : MonoBehaviour
{
    [Header("JSON")]
    [SerializeField] private TextAsset jsonFile;

    [Header("Term Button Prefab")]
    [SerializeField] private FinanceTermButton buttonPrefab;
    [SerializeField] private Transform contentParent;

    [Header("Category ScrollView")]
    [SerializeField] private Button categoryButtonPrefab;
    [SerializeField] private Transform categoryContentParent;

    [Header("Search")]
    [SerializeField] private TMP_InputField searchInput;

    [Header("Detail Panel Root")]
    [SerializeField] private GameObject detailPanel;

    [Header("Detail Texts")]
    [SerializeField] private TextMeshProUGUI detailCategoryText;
    [SerializeField] private TextMeshProUGUI detailDifficultyText;
    [SerializeField] private TextMeshProUGUI detailTermText;
    [SerializeField] private TextMeshProUGUI detailDescriptionText;
    [SerializeField] private TextMeshProUGUI detailExampleText;
    [SerializeField] private TextMeshProUGUI detailKeyPointsText;
    [SerializeField] private TextMeshProUGUI detailCautionText;

    [Header("Detail Difficulty Background")]
    [SerializeField] private Image detailDifficultyBackgroundImage;

    [Header("Detail Related Terms")]
    [SerializeField] private TextMeshProUGUI detailRelatedTermsText;

    private readonly List<FinanceTermData> allTerms =
        new List<FinanceTermData>();

    private readonly List<FinanceTermButton> createdButtons =
        new List<FinanceTermButton>();

    private readonly List<Button> createdCategoryButtons =
        new List<Button>();

    private const string AllOption = "전체";

    private string selectedCategory = AllOption;

    private void Awake()
    {
        CloseDetailPanel();
    }

    private void Start()
    {
        LoadJsonData();

        CreateCategoryButtons();
        CreateButtonsOnce();

        SetupEvents();

        RefreshList();
        RefreshCategoryButtonState();
    }

    private void LoadJsonData()
    {
        if (jsonFile == null)
        {
            Debug.LogError("JSON 파일이 연결되지 않았습니다.");
            return;
        }

        FinanceTermData[] loadedData =
            JsonArrayHelper.FromJsonArray<FinanceTermData>(jsonFile.text);

        if (loadedData == null || loadedData.Length == 0)
        {
            Debug.LogError("JSON 데이터 로드 실패");
            return;
        }

        allTerms.Clear();
        allTerms.AddRange(loadedData);
    }

    private void SetupEvents()
    {
        if (searchInput != null)
        {
            searchInput.onSubmit.AddListener(_ => RefreshList());

            // 입력할 때마다 바로 검색하고 싶다면 유지
            searchInput.onValueChanged.AddListener(_ => RefreshList());
        }
    }

    // =========================================================
    // 카테고리 버튼
    // =========================================================

    private void CreateCategoryButtons()
    {
        if (categoryButtonPrefab == null)
        {
            Debug.LogError("카테고리 버튼 프리팹이 연결되지 않았습니다.");
            return;
        }

        if (categoryContentParent == null)
        {
            Debug.LogError("Category Content Parent가 연결되지 않았습니다.");
            return;
        }

        ClearCategoryButtons();

        List<string> categories = allTerms
            .Select(data => data.category)
            .Where(category => !string.IsNullOrEmpty(category))
            .Distinct()
            .OrderBy(category => category)
            .ToList();

        categories.Insert(0, AllOption);

        foreach (string category in categories)
        {
            Button button =
                Instantiate(categoryButtonPrefab, categoryContentParent);

            TextMeshProUGUI buttonText =
                button.GetComponentInChildren<TextMeshProUGUI>();

            if (buttonText != null)
            {
                buttonText.text = category;
            }

            string capturedCategory = category;

            button.onClick.AddListener(() =>
            {
                SelectCategory(capturedCategory);
            });

            createdCategoryButtons.Add(button);
        }
    }

    private void ClearCategoryButtons()
    {
        createdCategoryButtons.Clear();

        if (categoryContentParent == null)
            return;

        for (int i = categoryContentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(categoryContentParent.GetChild(i).gameObject);
        }
    }

    private void SelectCategory(string category)
    {
        selectedCategory = category;

        RefreshList();
        RefreshCategoryButtonState();
    }

    private void RefreshCategoryButtonState()
    {
        Color selectedBackground;
        Color selectedText;

        Color normalBackground;
        Color normalText;

        ColorUtility.TryParseHtmlString("#F66B24", out selectedBackground);
        ColorUtility.TryParseHtmlString("#FFFFFF", out selectedText);

        ColorUtility.TryParseHtmlString("#FFF0E9", out normalBackground);
        ColorUtility.TryParseHtmlString("#F66B24", out normalText);

        foreach (Button button in createdCategoryButtons)
        {
            if (button == null)
                continue;

            TextMeshProUGUI text =
                button.GetComponentInChildren<TextMeshProUGUI>();

            if (text == null)
                continue;

            bool selected = text.text == selectedCategory;

            if (selected)
            {
                button.image.color = selectedBackground;
                text.color = selectedText;
            }
            else
            {
                button.image.color = normalBackground;
                text.color = normalText;
            }
        }
    }

    // =========================================================
    // 금융 용어 버튼
    // =========================================================

    private void CreateButtonsOnce()
    {
        if (buttonPrefab == null)
        {
            Debug.LogError("버튼 프리팹이 연결되지 않았습니다.");
            return;
        }

        if (contentParent == null)
        {
            Debug.LogError("Content Parent가 연결되지 않았습니다.");
            return;
        }

        ClearButtons();

        foreach (FinanceTermData data in allTerms)
        {
            FinanceTermButton button =
                Instantiate(buttonPrefab, contentParent);

            button.Bind(data, OpenDetailPanel);

            createdButtons.Add(button);
        }
    }

    private void ClearButtons()
    {
        createdButtons.Clear();

        if (contentParent == null)
            return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }
    }

    // =========================================================
    // 검색 / 카테고리 필터
    // =========================================================

    public void RefreshList()
    {
        string keyword =
            Normalize(searchInput != null ? searchInput.text : "");

        List<SearchResult> results =
            new List<SearchResult>();

        for (int i = 0; i < createdButtons.Count; i++)
        {
            FinanceTermButton button = createdButtons[i];

            if (button == null)
                continue;

            FinanceTermData data = button.Data;

            // 카테고리 필터
            if (!MatchCategory(data, selectedCategory))
            {
                button.gameObject.SetActive(false);
                continue;
            }

            // 검색 점수
            int score = GetSearchScore(data, keyword);

            if (score <= 0)
            {
                button.gameObject.SetActive(false);
                continue;
            }

            button.gameObject.SetActive(true);

            results.Add(
                new SearchResult(
                    button,
                    score,
                    i
                )
            );
        }

        // 검색어 없으면 JSON 원래 순서
        if (string.IsNullOrEmpty(keyword))
        {
            results = results
                .OrderBy(result => result.originalIndex)
                .ToList();
        }

        // 검색하면 검색 정확도 높은 순
        else
        {
            results = results
                .OrderByDescending(result => result.score)
                .ThenBy(result => result.originalIndex)
                .ToList();
        }

        for (int i = 0; i < results.Count; i++)
        {
            results[i].button.transform.SetSiblingIndex(i);
        }
    }

    private bool MatchCategory(
        FinanceTermData data,
        string category)
    {
        if (data == null)
            return false;

        if (string.IsNullOrEmpty(category) ||
            category == AllOption)
        {
            return true;
        }

        return data.category == category;
    }

    // =========================================================
    // 검색 점수
    // =========================================================

    private int GetSearchScore(
        FinanceTermData data,
        string keyword)
    {
        if (data == null)
            return 0;

        if (string.IsNullOrEmpty(keyword))
        {
            return 1;
        }

        string term = Normalize(data.term);
        string category = Normalize(data.category);
        string difficulty = Normalize(data.difficulty);
        string shortDefinition = Normalize(data.shortDefinition);
        string description = Normalize(data.description);
        string example = Normalize(data.example);
        string caution = Normalize(data.caution);

        // 용어 정확히 일치
        if (term == keyword)
        {
            return 100;
        }

        // 용어가 검색어로 시작
        if (term.StartsWith(keyword))
        {
            return 90;
        }

        // 용어 포함
        if (term.Contains(keyword))
        {
            return 80;
        }

        // 짧은 정의
        if (shortDefinition.Contains(keyword))
        {
            return 70;
        }

        // 설명
        if (description.Contains(keyword))
        {
            return 60;
        }

        // 예시
        if (example.Contains(keyword))
        {
            return 50;
        }

        // 카테고리 / 난이도
        if (category.Contains(keyword) ||
            difficulty.Contains(keyword))
        {
            return 40;
        }

        // 핵심 포인트
        if (ContainsList(data.keyPoints, keyword))
        {
            return 30;
        }

        // 주의사항
        if (caution.Contains(keyword))
        {
            return 20;
        }

        // 관련 용어
        if (ContainsList(data.relatedTerms, keyword))
        {
            return 10;
        }

        return 0;
    }

    private bool ContainsList(
        List<string> list,
        string keyword)
    {
        if (list == null)
            return false;

        foreach (string item in list)
        {
            if (Contains(item, keyword))
            {
                return true;
            }
        }

        return false;
    }

    private bool Contains(
        string source,
        string keyword)
    {
        if (string.IsNullOrEmpty(source))
            return false;

        return Normalize(source).Contains(keyword);
    }

    private string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Trim().ToLower();
    }

    // =========================================================
    // 상세창
    // =========================================================

    private void OpenDetailPanel(FinanceTermData data)
    {
        if (data == null)
            return;

        if (detailPanel != null)
        {
            detailPanel.SetActive(true);
        }

        SetText(detailTermText, data.term);
        SetText(detailCategoryText, data.category);

        // 난이도 필터는 제거했지만
        // 상세정보에는 난이도 표시 유지
        SetText(detailDifficultyText, data.difficulty);

        SetDetailDifficultyColor(data.difficulty);

        SetText(detailDescriptionText, data.description);
        SetText(detailExampleText, data.example);
        SetText(
            detailKeyPointsText,
            MakeKeyPointsText(data.keyPoints)
        );

        SetText(detailCautionText, data.caution);

        SetRelatedTerms(data.relatedTerms);
    }

    private void SetDetailDifficultyColor(string difficulty)
    {
        if (detailDifficultyBackgroundImage == null)
            return;

        string colorHex = "#FFFFFF";

        if (difficulty == "초급")
        {
            colorHex = "#C6F6D5";
        }
        else if (difficulty == "중급")
        {
            colorHex = "#E9D8FD";
        }

        if (ColorUtility.TryParseHtmlString(
            colorHex,
            out Color color))
        {
            detailDifficultyBackgroundImage.color = color;
        }
    }

    public void CloseDetailPanel()
    {
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }
    }

    // =========================================================
    // 초기화
    // =========================================================

    public void ResetDictionaryState()
    {
        CloseDetailPanel();

        if (searchInput != null)
        {
            searchInput.SetTextWithoutNotify("");
        }

        selectedCategory = AllOption;

        RefreshList();
        RefreshCategoryButtonState();
    }

    // =========================================================
    // Text
    // =========================================================

    private void SetText(
        TextMeshProUGUI target,
        string value)
    {
        if (target == null)
            return;

        target.text =
            string.IsNullOrEmpty(value)
            ? "-"
            : value;
    }

    private string MakeKeyPointsText(
        List<string> keyPoints)
    {
        if (keyPoints == null ||
            keyPoints.Count == 0)
        {
            return "- 없음";
        }

        StringBuilder sb = new StringBuilder();

        foreach (string point in keyPoints)
        {
            sb.AppendLine("● " + point);
        }

        return sb.ToString();
    }

    private void SetRelatedTerms(
        List<string> relatedTerms)
    {
        SetText(
            detailRelatedTermsText,
            MakeRelatedTermsText(relatedTerms)
        );
    }

    private string MakeRelatedTermsText(
        List<string> relatedTerms)
    {
        if (relatedTerms == null ||
            relatedTerms.Count == 0)
        {
            return "- 없음";
        }

        StringBuilder sb = new StringBuilder();

        foreach (string term in relatedTerms)
        {
            sb.AppendLine("● " + term);
        }

        return sb.ToString();
    }

    // =========================================================
    // 검색 결과 클래스
    // =========================================================

    private class SearchResult
    {
        public FinanceTermButton button;
        public int score;
        public int originalIndex;

        public SearchResult(
            FinanceTermButton button,
            int score,
            int originalIndex)
        {
            this.button = button;
            this.score = score;
            this.originalIndex = originalIndex;
        }
    }
}