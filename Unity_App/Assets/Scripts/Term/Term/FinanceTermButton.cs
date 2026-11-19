using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinanceTermButton : MonoBehaviour
{
    [Header("Button Texts")]
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private TextMeshProUGUI termText;
    [SerializeField] private TextMeshProUGUI shortDefinitionText;

    [Header("Difficulty Background")]
    [SerializeField] private Image difficultyBackgroundImage;

    private Button button;
    private FinanceTermData data;
    private Action<FinanceTermData> onClick;

    public FinanceTermData Data => data;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }
    }

    public void Bind(FinanceTermData newData, Action<FinanceTermData> clickAction)
    {
        data = newData;
        onClick = clickAction;

        SetText(categoryText, data.category);
        SetText(difficultyText, data.difficulty);
        SetText(termText, data.term);
        SetText(shortDefinitionText, data.shortDefinition);
        SetDifficultyColor(data.difficulty);
    }

    private void SetDifficultyColor(string difficulty)
    {
        if (difficultyBackgroundImage == null) return;

        string colorHex = "#FFFFFF";

        if (difficulty == "초급")
        {
            colorHex = "#C6F6D5";
        }
        else if (difficulty == "중급")
        {
            colorHex = "#E9D8FD";
        }

        if (ColorUtility.TryParseHtmlString(colorHex, out Color color))
        {
            difficultyBackgroundImage.color = color;
        }
    }

    private void HandleClick()
    {
        if (data != null)
        {
            onClick?.Invoke(data);
        }
    }

    private void SetText(TextMeshProUGUI target, string value)
    {
        if (target == null) return;

        target.text = string.IsNullOrEmpty(value) ? "-" : value;
    }
}