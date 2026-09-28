using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChapterMapZoomController : MonoBehaviour
{
    [Header("Current Zoom Target")]
    public RectTransform chapterMap;

    [Header("Panels")]
    public GameObject[] chapterPanels;

    [Header("Zoom Settings")]
    public float zoomScale = 1.8f;
    public float zoomTime = 0.45f;

    [Header("Back Settings")]
    public float backShrinkScale = 0.92f;
    public float backShrinkTime = 0.15f;

    private Coroutine zoomRoutine;

    private int currentPanelIndex = 0;

    private Vector2[] originalPositions;
    private Vector3[] originalScales;

    private Stack<PanelHistory> panelHistory = new Stack<PanelHistory>();

    private class PanelHistory
    {
        public int panelIndex;
        public Vector2 zoomedPosition;
        public Vector3 zoomedScale;
    }

    private void Start()
    {
        originalPositions = new Vector2[chapterPanels.Length];
        originalScales = new Vector3[chapterPanels.Length];

        for (int i = 0; i < chapterPanels.Length; i++)
        {
            RectTransform rect = chapterPanels[i].GetComponent<RectTransform>();
            originalPositions[i] = rect.anchoredPosition;
            originalScales[i] = rect.localScale;
        }

        ShowOnlyPanel(0);
        SetCurrentMap(0);
    }

    public void OnClickChapterByIndex(RectTransform targetPoint, int nextPanelIndex)
    {
        if (nextPanelIndex < 0 || nextPanelIndex >= chapterPanels.Length)
            return;

        if (zoomRoutine != null)
            StopCoroutine(zoomRoutine);

        zoomRoutine = StartCoroutine(ZoomToTarget(targetPoint, nextPanelIndex));
    }

    private IEnumerator ZoomToTarget(RectTransform targetPoint, int nextPanelIndex)
    {
        RectTransform currentMap = chapterMap;

        Vector2 startPos = currentMap.anchoredPosition;
        Vector3 startScale = currentMap.localScale;

        Vector2 targetLocalPos = targetPoint.anchoredPosition;

        Vector2 endPos = -targetLocalPos * zoomScale;
        Vector3 endScale = originalScales[currentPanelIndex] * zoomScale;

        // 핵심: 뒤로가기 때 다시 이 확대 상태에서 축소되도록 저장
        panelHistory.Push(new PanelHistory
        {
            panelIndex = currentPanelIndex,
            zoomedPosition = endPos,
            zoomedScale = endScale
        });

        float t = 0f;

        while (t < zoomTime)
        {
            t += Time.deltaTime;
            float p = t / zoomTime;
            p = Mathf.SmoothStep(0f, 1f, p);

            currentMap.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
            currentMap.localScale = Vector3.Lerp(startScale, endScale, p);

            yield return null;
        }

        currentMap.anchoredPosition = endPos;
        currentMap.localScale = endScale;

        yield return new WaitForSeconds(0.15f);

        // 다음 패널로 넘어가기 전에 현재 패널은 원래 상태로 복구해둠
        currentMap.anchoredPosition = originalPositions[currentPanelIndex];
        currentMap.localScale = originalScales[currentPanelIndex];

        ShowOnlyPanel(nextPanelIndex);
        SetCurrentMap(nextPanelIndex);
    }

    public void OnClickBack()
    {
        if (panelHistory.Count <= 0)
            return;

        if (zoomRoutine != null)
            StopCoroutine(zoomRoutine);

        zoomRoutine = StartCoroutine(BackToPreviousPanel());
    }

    private IEnumerator BackToPreviousPanel()
    {
        PanelHistory history = panelHistory.Pop();

        // 현재 패널은 원래 상태로 복구
        chapterMap.anchoredPosition = originalPositions[currentPanelIndex];
        chapterMap.localScale = originalScales[currentPanelIndex];

        // 이전 패널로 먼저 전환
        ShowOnlyPanel(history.panelIndex);
        SetCurrentMap(history.panelIndex);

        // 핵심: 이전 패널을 "아까 클릭해서 확대됐던 상태"로 만들어놓고 시작
        chapterMap.anchoredPosition = history.zoomedPosition;
        chapterMap.localScale = history.zoomedScale;

        Vector2 startPos = chapterMap.anchoredPosition;
        Vector3 startScale = chapterMap.localScale;

        Vector3 shrinkScale = startScale * backShrinkScale;

        float t = 0f;

        // 1. 살짝 더 축소되는 느낌
        while (t < backShrinkTime)
        {
            t += Time.deltaTime;
            float p = t / backShrinkTime;
            p = Mathf.SmoothStep(0f, 1f, p);

            chapterMap.localScale = Vector3.Lerp(startScale, shrinkScale, p);

            yield return null;
        }

        // 2. 원래 위치와 크기로 복귀
        t = 0f;

        Vector2 shrinkStartPos = chapterMap.anchoredPosition;
        Vector3 shrinkStartScale = chapterMap.localScale;

        Vector2 endPos = originalPositions[currentPanelIndex];
        Vector3 endScale = originalScales[currentPanelIndex];

        while (t < zoomTime)
        {
            t += Time.deltaTime;
            float p = t / zoomTime;
            p = Mathf.SmoothStep(0f, 1f, p);

            chapterMap.anchoredPosition = Vector2.Lerp(shrinkStartPos, endPos, p);
            chapterMap.localScale = Vector3.Lerp(shrinkStartScale, endScale, p);

            yield return null;
        }

        chapterMap.anchoredPosition = endPos;
        chapterMap.localScale = endScale;
    }

    private void SetCurrentMap(int index)
    {
        currentPanelIndex = index;
        chapterMap = chapterPanels[index].GetComponent<RectTransform>();

        Debug.Log("현재 줌 타겟: " + chapterPanels[index].name);
    }

    private void ShowOnlyPanel(int index)
    {
        for (int i = 0; i < chapterPanels.Length; i++)
        {
            if (chapterPanels[i] != null)
                chapterPanels[i].SetActive(i == index);
        }
    }
}