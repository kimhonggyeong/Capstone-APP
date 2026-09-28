using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewsRowUI : MonoBehaviour
{
    [Header("뉴스 표시")]
    public TMP_Text sourceText;
    public TMP_Text dateText;
    public TMP_Text descriptionText;

    [Header("원문 열기")]
    public Button openButton;

    public NewsItem CurrentItem
    {
        get;
        private set;
    }


    private void Awake()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(
                OpenCurrentNews
            );
        }
    }


    private void OnDestroy()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(
                OpenCurrentNews
            );
        }
    }


    public void SetData(
        NewsItem item
    )
    {
        CurrentItem = item;

        if (item == null)
        {
            Clear();
            return;
        }

        gameObject.SetActive(true);

        if (sourceText != null)
        {
            sourceText.text =
                string.IsNullOrWhiteSpace(
                    item.source
                )
                    ? "뉴스"
                    : item.source;
        }

        if (dateText != null)
        {
            dateText.text =
                FormatDate(
                    item.published_at
                );
        }

        if (descriptionText != null)
        {
            descriptionText.text =
                item.description ?? "";
        }

        if (openButton != null)
        {
            openButton.interactable =
                !string.IsNullOrWhiteSpace(
                    item.link
                );
        }
    }


    private string FormatDate(
        string rawDate
    )
    {
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            return "";
        }

        /*
         * 서버 응답:
         * 2026.07.27 10:30
         *
         * 화면:
         * 2026.07.27
         */
        if (rawDate.Length >= 10)
        {
            return rawDate.Substring(
                0,
                10
            );
        }

        return rawDate;
    }


    private void OpenCurrentNews()
    {
        if (
            CurrentItem == null ||
            string.IsNullOrWhiteSpace(
                CurrentItem.link
            )
        )
        {
            return;
        }

        Debug.Log(
            "[뉴스 원문 열기] " +
            CurrentItem.link
        );

        Application.OpenURL(
            CurrentItem.link
        );
    }


    public void Clear()
    {
        CurrentItem = null;

        if (sourceText != null)
        {
            sourceText.text = "";
        }

        if (dateText != null)
        {
            dateText.text = "";
        }

        if (descriptionText != null)
        {
            descriptionText.text = "";
        }

        if (openButton != null)
        {
            openButton.interactable =
                false;
        }
    }
}