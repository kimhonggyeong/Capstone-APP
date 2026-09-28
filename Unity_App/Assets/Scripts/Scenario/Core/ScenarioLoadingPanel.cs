using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScenarioLoadingPanel : MonoBehaviour
{
    [SerializeField]
    [Min(0f)]
    private float minimumVisibleSeconds = 0.5f;

    private readonly HashSet<int> owners = new HashSet<int>();
    private Coroutine hideRoutine;
    private float shownAt;

    public void Show(Object owner)
    {
        if (owner == null)
            return;

        owners.Add(owner.GetInstanceID());

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            shownAt = Time.realtimeSinceStartup;
        }

        transform.SetAsLastSibling();
    }

    public void Hide(Object owner)
    {
        if (owner != null)
            owners.Remove(owner.GetInstanceID());

        if (owners.Count > 0 || hideRoutine != null)
            return;

        // 다른 초기화 코드가 패널을 먼저 껐더라도 오류를 내지 않는다.
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        hideRoutine = StartCoroutine(HideAfterMinimumTime());
    }

    public void HideImmediately()
    {
        owners.Clear();

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        gameObject.SetActive(false);
    }

    private IEnumerator HideAfterMinimumTime()
    {
        float remaining = minimumVisibleSeconds -
            (Time.realtimeSinceStartup - shownAt);

        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        hideRoutine = null;

        if (owners.Count == 0)
            gameObject.SetActive(false);
    }
}
