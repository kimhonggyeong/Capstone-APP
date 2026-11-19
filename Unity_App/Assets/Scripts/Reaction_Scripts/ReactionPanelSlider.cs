using System.Collections;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class ReactionPanelSlider : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform panelRect;
    public GameObject panelObject;
    public float openY = 672f;
    public float closedY = 0f;
    public float snapSpeed = 10f;
    public float closeSpeedMultiplier = 1.5f;
    [Range(.1f, .9f)] public float closeThreshold = .5f;
    bool opened, dragging;
    Coroutine slideRoutine;
    Action closeRootAction;

    public void SetCloseRootAction(Action action) { closeRootAction = action; }

    public void OpenPanel()
    {
        StopSlide();
        if (panelObject != null) panelObject.SetActive(true);
        // The slider component is usually on a Handle under panelRect.
        // Activate that panel before starting a coroutine on the Handle.
        if (panelRect != null) panelRect.gameObject.SetActive(true);
        SetY(closedY); opened = true;
        slideRoutine = StartCoroutine(Slide(openY, false));
    }
    public void ClosePanel()
    {
        StopSlide(); dragging = false; opened = false;
        slideRoutine = StartCoroutine(Slide(closedY, true, true));
    }
    public void CloseForSwitch()
    {
        StopSlide(); dragging = false; opened = false;
        // Move only this panel rect. panelObject can be the shared simulator root.
        slideRoutine = StartCoroutine(Slide(closedY, false, false));
    }
    public void HideForSwitchImmediate()
    {
        StopSlide(); dragging = false; opened = false; SetY(closedY);
    }
    public void HideImmediate()
    {
        StopSlide(); dragging = false; opened = false; SetY(closedY);
        if (panelObject != null) panelObject.SetActive(false);
    }
    public bool IsOpened() { return opened; }
    public void OnBeginDrag(PointerEventData eventData) { if (!opened) return; dragging = true; StopSlide(); }
    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || panelRect == null) return;
        Vector2 pos = panelRect.anchoredPosition;
        pos.y = Mathf.Clamp(pos.y + eventData.delta.y, closedY, openY);
        panelRect.anchoredPosition = pos;
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || panelRect == null) return;
        dragging = false;
        float ratio = Mathf.InverseLerp(openY, closedY, panelRect.anchoredPosition.y);
        if (ratio >= closeThreshold) ClosePanel();
        else { opened = true; slideRoutine = StartCoroutine(Slide(openY, false)); }
    }
    IEnumerator Slide(float targetY, bool disableAfter, bool disableRootAfter = false)
    {
        float speed = snapSpeed * 100f * (disableAfter ? closeSpeedMultiplier : 1f);
        while (panelRect != null && Mathf.Abs(panelRect.anchoredPosition.y - targetY) > .1f)
        {
            SetY(Mathf.MoveTowards(panelRect.anchoredPosition.y, targetY, speed * Time.unscaledDeltaTime));
            yield return null;
        }
        SetY(targetY); slideRoutine = null;
        if (disableRootAfter)
        {
            closeRootAction?.Invoke();
            yield break;
        }
        if (disableAfter && panelObject != null) panelObject.SetActive(false);
    }
    void SetY(float y) { if (panelRect != null) panelRect.anchoredPosition = new Vector2(panelRect.anchoredPosition.x, y); }
    void StopSlide() { if (slideRoutine != null) StopCoroutine(slideRoutine); slideRoutine = null; }
}
