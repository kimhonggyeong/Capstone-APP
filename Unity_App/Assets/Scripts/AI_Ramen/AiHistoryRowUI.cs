using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AiHistoryRowUI : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text timeText;
    public TMP_Text judgmentText;
    public TMP_Text confidenceText;
    public TMP_Text reasonText;

    [Header("Judgment")]
    public Image judgmentBackground;
    public Color buyJudgmentColor =
        new Color32(255, 234, 231, 255);
    public Color holdJudgmentColor =
        new Color32(255, 242, 232, 255);
    public Color sellJudgmentColor =
        new Color32(232, 239, 255, 255);

    [Header("Automatic Height")]
    [Tooltip("비워두면 현재 History_Item의 RectTransform을 사용합니다.")]
    [SerializeField] private RectTransform historyItemRect;

    [Tooltip("목록의 Vertical Layout Group에 전달할 높이입니다. 비워두면 자동으로 찾거나 생성합니다.")]
    [SerializeField] private LayoutElement historyItemLayoutElement;

    [Tooltip("reasonText 아래쪽에 남길 여백")]
    [SerializeField] private float bottomPadding = 10f;

    [Tooltip("History_Item의 최소 높이")]
    [SerializeField] private float minimumItemHeight = 80f;

    [Tooltip("History_Item 상단부터 reasonText 상단까지의 거리")]
    [SerializeField] private float reasonTopOffset = 48.8f;

    private RectTransform reasonTextRect;
    private Coroutine rebuildCoroutine;

    private void Awake()
    {
        if (historyItemRect == null)
            historyItemRect = transform as RectTransform;

        if (historyItemLayoutElement == null)
            historyItemLayoutElement = GetComponent<LayoutElement>();

        if (historyItemLayoutElement == null)
            historyItemLayoutElement = gameObject.AddComponent<LayoutElement>();

        if (reasonText != null)
            reasonTextRect = reasonText.rectTransform;

        if (reasonTextRect != null)
        {
            // ContentSizeFitter와 코드가 동시에 높이를 제어하면 레이아웃이 크게 벌어집니다.
            ContentSizeFitter fitter =
                reasonTextRect.GetComponent<ContentSizeFitter>();

            if (fitter != null)
                fitter.enabled = false;

            reasonTextRect.anchorMin = new Vector2(
                reasonTextRect.anchorMin.x,
                1f
            );
            reasonTextRect.anchorMax = new Vector2(
                reasonTextRect.anchorMax.x,
                1f
            );
            reasonTextRect.pivot = new Vector2(
                reasonTextRect.pivot.x,
                1f
            );

            Vector2 position = reasonTextRect.anchoredPosition;
            position.y = -reasonTopOffset;
            reasonTextRect.anchoredPosition = position;
        }
    }

    public void SetData(AiHistoryEntry item)
    {
        if (item == null)
            return;

        if (timeText != null)
            timeText.text = FormatTime(item.time);

        if (judgmentText != null)
            judgmentText.text = item.judge ?? "-";

        ApplyJudgmentColor(item.judge);

        if (confidenceText != null)
        {
            confidenceText.text = item.confidence >= 0f
                ? $"신뢰도 {item.confidence:0.#}%"
                : "";
        }

        if (reasonText != null)
            reasonText.text = item.reason ?? "";

        RefreshHeight();

        // TMP와 부모 Layout Group의 다음 프레임 계산까지 반영합니다.
        if (rebuildCoroutine != null)
            StopCoroutine(rebuildCoroutine);

        rebuildCoroutine = StartCoroutine(
            RebuildHeightNextFrame()
        );
    }

    private void RefreshHeight()
    {
        if (historyItemRect == null ||
            reasonText == null ||
            reasonTextRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        reasonText.ForceMeshUpdate();

        float availableWidth = reasonTextRect.rect.width;

        if (availableWidth <= 0f)
            availableWidth = reasonText.preferredWidth;

        float preferredTextHeight = reasonText.GetPreferredValues(
            reasonText.text,
            availableWidth,
            Mathf.Infinity
        ).y;

        preferredTextHeight = Mathf.Max(
            preferredTextHeight,
            reasonText.fontSize
        );

        reasonTextRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            preferredTextHeight
        );

        float targetItemHeight = Mathf.Max(
            minimumItemHeight,
            reasonTopOffset + preferredTextHeight + bottomPadding
        );

        historyItemRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            targetItemHeight
        );

        historyItemLayoutElement.preferredHeight =
            targetItemHeight;

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            historyItemRect
        );

        RectTransform parent =
            historyItemRect.parent as RectTransform;

        if (parent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                parent
            );
        }
    }

    private IEnumerator RebuildHeightNextFrame()
    {
        yield return null;

        RefreshHeight();
        rebuildCoroutine = null;
    }

    private void ApplyJudgmentColor(string judgment)
    {
        if (judgmentBackground == null)
            return;

        switch (judgment)
        {
            case "매수":
                judgmentBackground.color =
                    buyJudgmentColor;
                break;

            case "매도":
                judgmentBackground.color =
                    sellJudgmentColor;
                break;

            case "관망":
            default:
                judgmentBackground.color =
                    holdJudgmentColor;
                break;
        }
    }

    private static string FormatTime(string value)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset parsed))
        {
            return value ?? "-";
        }

        return parsed
            .ToLocalTime()
            .ToString("yyyy.MM.dd  HH:mm");
    }
}
