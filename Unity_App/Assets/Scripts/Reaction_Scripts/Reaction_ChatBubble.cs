using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Reaction_ChatBubble : MonoBehaviour
{
    public TMP_Text messageText;
    public Button button;

    public void SetText(string text)
    {
        messageText.text = text;

        if (button != null)
        {
            button.interactable = false;
        }
    }

    public void SetButton(string text, UnityEngine.Events.UnityAction onClick)
    {
        messageText.text = text;

        if (button != null)
        {
            button.gameObject.SetActive(true);
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
        }
    }
}