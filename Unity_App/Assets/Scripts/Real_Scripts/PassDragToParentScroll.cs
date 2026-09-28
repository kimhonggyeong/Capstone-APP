using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PassDragToParentScroll : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler
{
    private ScrollRect parentScrollRect;

    private void Awake()
    {
        parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (parentScrollRect != null)
        {
            parentScrollRect.OnBeginDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentScrollRect != null)
        {
            parentScrollRect.OnDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (parentScrollRect != null)
        {
            parentScrollRect.OnEndDrag(eventData);
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (parentScrollRect != null)
        {
            parentScrollRect.OnScroll(eventData);
        }
    }
}