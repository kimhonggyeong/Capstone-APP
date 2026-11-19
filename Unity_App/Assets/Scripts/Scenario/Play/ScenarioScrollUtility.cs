using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class ScenarioScrollUtility
{
    public static IEnumerator ResetToStart(
        ScrollRect[] scrollRects,
        RectTransform primaryContent = null)
    {
        ResetPositions(scrollRects, primaryContent);

        // Destroy로 제거되는 이전 목록까지 레이아웃에 반영한 뒤 다시 맞춥니다.
        yield return null;

        ResetPositions(scrollRects, primaryContent);
    }

    private static void ResetPositions(
        ScrollRect[] scrollRects,
        RectTransform primaryContent)
    {
        Canvas.ForceUpdateCanvases();

        if (primaryContent != null)
        {
            ScrollRect primaryScroll =
                primaryContent.GetComponentInParent<ScrollRect>(true);

            if (primaryScroll != null && primaryScroll.content == primaryContent)
            {
                ResetPosition(primaryScroll);
            }
            else
            {
                Vector2 position = primaryContent.anchoredPosition;
                position.y = 0f;
                primaryContent.anchoredPosition = position;
            }
        }

        if (scrollRects == null)
            return;

        foreach (ScrollRect scrollRect in scrollRects)
            ResetPosition(scrollRect);
    }

    private static void ResetPosition(ScrollRect scrollRect)
    {
        if (scrollRect == null || scrollRect.content == null)
            return;

        scrollRect.StopMovement();

        // 비활성 패널은 해당 패널이 열려 레이아웃을 계산할 때 초기화합니다.
        if (!scrollRect.isActiveAndEnabled)
            return;

        if (scrollRect.horizontal)
            scrollRect.horizontalNormalizedPosition = 0f;

        if (scrollRect.vertical)
            scrollRect.verticalNormalizedPosition = 1f;
    }
}
