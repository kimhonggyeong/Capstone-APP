using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AiCompareView : MonoBehaviour
{
    public TMP_Text userJudgmentText;
    public TMP_Text aiJudgmentText;
    public TMP_Text confidenceText;
    public TMP_Text explanationText;
    public TMP_Text directFactorsText;
    public TMP_Text indirectFactorsText;

    [Header("레이아웃 갱신")]
    [Tooltip("비교 화면 전체 또는 세로 Layout Group이 붙은 최상위 RectTransform")]
    public RectTransform layoutRoot;

    private Coroutine refreshRoutine;
    private bool refreshPending;

    public void SetData(AiCompareResponse data, AiJudgmentResponse latest)
    {
        if (data == null) return;
        if (userJudgmentText != null) userJudgmentText.text = data.user_judge ?? "-";
        if (aiJudgmentText != null) aiJudgmentText.text = data.ai_judge ?? "-";

        // 화면에 고정된 "신뢰도" 라벨이 있으므로 값만 표시한다.
        if (confidenceText != null)
            confidenceText.text = latest != null ? $"{latest.confidence:0.#}%" : "";

        if (explanationText != null) explanationText.text = data.explanation ?? "";
        if (directFactorsText != null)
            directFactorsText.text = JoinFactors(data.highlighted_factors, "직접요인");
        if (indirectFactorsText != null)
            indirectFactorsText.text = JoinFactors(data.highlighted_factors, "간접요인");

        refreshPending = true;
        TryStartLayoutRefresh();
    }

    private void OnEnable()
    {
        TryStartLayoutRefresh();
    }

    private void TryStartLayoutRefresh()
    {
        if (!refreshPending || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (refreshRoutine != null) StopCoroutine(refreshRoutine);
        refreshRoutine = StartCoroutine(RefreshLayout());
    }
    private IEnumerator RefreshLayout()
    {
        // 패널 활성화와 텍스트 변경이 같은 프레임에 일어날 때 한 프레임 기다린다.
        yield return null;
        ForceTextMeshes();
        Canvas.ForceUpdateCanvases();
        RebuildLayout();

        // ContentSizeFitter/VerticalLayoutGroup의 후속 계산까지 한 번 더 반영한다.
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        RebuildLayout();
        refreshRoutine = null;
        refreshPending = false;
    }

    private void ForceTextMeshes()
    {
        ForceText(userJudgmentText);
        ForceText(aiJudgmentText);
        ForceText(confidenceText);
        ForceText(explanationText);
        ForceText(directFactorsText);
        ForceText(indirectFactorsText);
    }

    private static void ForceText(TMP_Text target)
    {
        if (target != null) target.ForceMeshUpdate(true, true);
    }

    private void RebuildLayout()
    {
        RectTransform target = layoutRoot != null
            ? layoutRoot
            : transform as RectTransform;
        if (target != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);
    }

    private static string JoinFactors(Dictionary<string, List<string>> groups, string key)
    {
        if (groups == null || !groups.ContainsKey(key) || groups[key] == null || groups[key].Count == 0)
            return "• 해당 요인 없음";
        return "• " + string.Join("\n• ", groups[key]);
    }

    private void OnDisable()
    {
        if (refreshRoutine == null) return;
        StopCoroutine(refreshRoutine);
        refreshRoutine = null;
        refreshPending = true;
    }
}