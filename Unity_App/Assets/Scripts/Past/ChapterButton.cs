using UnityEngine;
using UnityEngine.UI;

public class ChapterButton : MonoBehaviour
{
    public ChapterMapZoomController controller;
    public RectTransform targetPoint;
    public int chapterIndex;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        controller.OnClickChapterByIndex(targetPoint, chapterIndex);
    }
}