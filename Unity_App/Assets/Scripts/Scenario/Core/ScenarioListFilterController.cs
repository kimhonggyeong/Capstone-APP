using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioListFilterController : MonoBehaviour
{
    private enum FilterType
    {
        All,
        InProgress,
        Completed,
        AllScenarios
    }

    [Header("필터 버튼")]
    [SerializeField] private Button allButton;
    [SerializeField] private Button inProgressButton;
    [SerializeField] private Button completedButton;
    [SerializeField] private Button allScenariosButton;

    [Header("진행 중 시나리오")]
    [SerializeField] private GameObject divider1;
    [SerializeField] private GameObject content1;

    [Header("완료된 시나리오")]
    [SerializeField] private GameObject divider2;
    [SerializeField] private GameObject content2;

    [Header("전체 시나리오")]
    [SerializeField] private GameObject divider3;
    [SerializeField] private GameObject content3;

    [Header("버튼 색상")]
    [SerializeField]
    private Color selectedButtonColor =
        new Color32(0xF6, 0x6B, 0x24, 0xFF);

    [SerializeField]
    private Color selectedTextColor =
        new Color32(0xF6, 0x6B, 0x24, 0xFF);

    [SerializeField]
    private Color normalButtonColor =
        new Color32(0xF5, 0xEB, 0xDA, 0xFF);

    [SerializeField]
    private Color normalTextColor = Color.black;

    [Header("초기 필터")]
    [SerializeField] private bool showAllOnEnable = true;

    private FilterType currentFilter;

    private void Awake()
    {
        AddButtonListeners();
    }

    private void OnEnable()
    {
        if (showAllOnEnable)
            ShowAll();
        else
            ApplyFilter(currentFilter);
    }

    public void ShowAll()
    {
        ApplyFilter(FilterType.All);
    }

    public void ShowInProgress()
    {
        ApplyFilter(FilterType.InProgress);
    }

    public void ShowCompleted()
    {
        ApplyFilter(FilterType.Completed);
    }

    public void ShowAllScenarios()
    {
        ApplyFilter(FilterType.AllScenarios);
    }

    private void ApplyFilter(FilterType filter)
    {
        currentFilter = filter;

        bool showProgress =
            filter == FilterType.All ||
            filter == FilterType.InProgress;

        bool showCompleted =
            filter == FilterType.All ||
            filter == FilterType.Completed;

        bool showScenarioList =
            filter == FilterType.All ||
            filter == FilterType.AllScenarios;

        SetGroupActive(
            divider1,
            content1,
            showProgress
        );

        SetGroupActive(
            divider2,
            content2,
            showCompleted
        );

        SetGroupActive(
            divider3,
            content3,
            showScenarioList
        );

        UpdateButtonVisuals(filter);

        Canvas.ForceUpdateCanvases();
    }

    private void UpdateButtonVisuals(FilterType filter)
    {
        SetButtonVisual(
            allButton,
            filter == FilterType.All
        );

        SetButtonVisual(
            inProgressButton,
            filter == FilterType.InProgress
        );

        SetButtonVisual(
            completedButton,
            filter == FilterType.Completed
        );

        SetButtonVisual(
            allScenariosButton,
            filter == FilterType.AllScenarios
        );
    }

    private void SetButtonVisual(
        Button button,
        bool selected)
    {
        if (button == null)
            return;

        Color buttonColor = selected
            ? selectedButtonColor
            : normalButtonColor;

        Color textColor = selected
            ? selectedTextColor
            : normalTextColor;

        // Button의 Target Graphic 색상 변경
        if (button.targetGraphic != null)
            button.targetGraphic.color = buttonColor;

        // 버튼 상태 변화로 색상이 덮어써지지 않도록 설정
        ColorBlock colors = button.colors;

        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonColor;
        colors.selectedColor = buttonColor;
        colors.disabledColor = buttonColor;
        colors.pressedColor = selected
            ? selectedButtonColor
            : normalButtonColor;

        colors.colorMultiplier = 1f;
        button.colors = colors;

        // 버튼 자식의 TMP_Text를 자동으로 찾아 변경
        TMP_Text buttonText =
            button.GetComponentInChildren<TMP_Text>(true);

        if (buttonText != null)
            buttonText.color = textColor;
    }

    private static void SetGroupActive(
        GameObject divider,
        GameObject content,
        bool active)
    {
        if (divider != null)
            divider.SetActive(active);

        if (content != null)
            content.SetActive(active);
    }

    private void AddButtonListeners()
    {
        if (allButton != null)
            allButton.onClick.AddListener(ShowAll);

        if (inProgressButton != null)
            inProgressButton.onClick.AddListener(
                ShowInProgress
            );

        if (completedButton != null)
            completedButton.onClick.AddListener(
                ShowCompleted
            );

        if (allScenariosButton != null)
            allScenariosButton.onClick.AddListener(
                ShowAllScenarios
            );
    }

    private void OnDestroy()
    {
        if (allButton != null)
            allButton.onClick.RemoveListener(ShowAll);

        if (inProgressButton != null)
            inProgressButton.onClick.RemoveListener(
                ShowInProgress
            );

        if (completedButton != null)
            completedButton.onClick.RemoveListener(
                ShowCompleted
            );

        if (allScenariosButton != null)
            allScenariosButton.onClick.RemoveListener(
                ShowAllScenarios
            );
    }
}