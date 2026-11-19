using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinancePanelManager : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject dictionaryPanel;
    [SerializeField] private GameObject quizPanel;

    [Header("Managers")]
    [SerializeField] private FinanceDictionaryManager dictionaryManager;
    [SerializeField] private FinanceQuizManager quizManager;

    [Header("Move Buttons")]
    [SerializeField] private Button dictionaryButton;
    [SerializeField] private Button quizButton;

    [Header("Start Option")]
    [SerializeField] private bool openDictionaryOnStart = true;

    private readonly Color selectedButtonColor = new Color32(0xF6, 0x6B, 0x24, 0xFF); // #F66B24
    private readonly Color normalButtonColor = Color.white;

    private readonly Color selectedTextColor = Color.white;
    private readonly Color normalTextColor = Color.black;

    private void Start()
    {
        if (openDictionaryOnStart)
        {
            OpenDictionaryPanel();
        }
        else
        {
            OpenQuizPanel();
        }
    }

    public void OpenDictionaryPanel()
    {
        if (dictionaryPanel != null)
            dictionaryPanel.SetActive(true);

        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (quizManager != null)
            quizManager.ResetQuizState();

        if (dictionaryManager != null)
            dictionaryManager.ResetDictionaryState();

        SetSelectedButton(dictionaryButton);
    }

    public void OpenQuizPanel()
    {
        if (dictionaryPanel != null)
            dictionaryPanel.SetActive(false);

        if (quizPanel != null)
            quizPanel.SetActive(true);

        if (dictionaryManager != null)
            dictionaryManager.ResetDictionaryState();

        if (quizManager != null)
            quizManager.ResetQuizState();

        SetSelectedButton(quizButton);
    }

    private void SetSelectedButton(Button selectedButton)
    {
        SetButtonColor(dictionaryButton, dictionaryButton == selectedButton);
        SetButtonColor(quizButton, quizButton == selectedButton);

        if (selectedButton != null)
        {
            selectedButton.transform.SetAsLastSibling();
        }
    }

    private void SetButtonColor(Button button, bool isSelected)
    {
        if (button == null)
            return;

        Image buttonImage = button.GetComponent<Image>();

        if (buttonImage != null)
        {
            buttonImage.color = isSelected ? selectedButtonColor : normalButtonColor;
        }

        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();

        if (buttonText != null)
        {
            buttonText.color = isSelected ? selectedTextColor : normalTextColor;
        }
    }
}