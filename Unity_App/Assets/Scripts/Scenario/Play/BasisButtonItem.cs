using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BasisButtonItem : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Image background;

    [SerializeField] private Color normalColor = Color.black;

    [SerializeField]
    private Color selectedColor =
        new Color32(255, 107, 26, 255);

    [SerializeField]
    private Color selectedBackground =
        new Color32(255, 245, 238, 255);

    private Color normalBackground;
    private Action<BasisButtonItem> clickCallback;

    public string Option { get; private set; }
    public bool IsSelected { get; private set; }

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (labelText == null)
            labelText = GetComponentInChildren<TMP_Text>(true);

        if (background == null)
            background = GetComponent<Image>();

        if (background != null)
            normalBackground = background.color;
    }

    public void SetData(
        string option,
        Action<BasisButtonItem> onClick)
    {
        Option = option;
        clickCallback = onClick;

        if (labelText != null)
            labelText.text = option;

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (labelText != null)
        {
            labelText.color = selected
                ? selectedColor
                : normalColor;
        }

        if (background != null)
        {
            background.color = selected
                ? selectedBackground
                : normalBackground;
        }
    }

    private void HandleClick()
    {
        clickCallback?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }
}