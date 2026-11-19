using System.Collections;
using UnityEngine;

/// <summary>
/// 이 오브젝트가 활성화되면 두 패널을 일정 간격으로 번갈아 표시합니다.
/// Loading_Panel 오브젝트에 붙여 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public class LoadingPanelAlternator : MonoBehaviour
{
    [Header("번갈아 표시할 패널")]
    [SerializeField] private GameObject panel1;
    [SerializeField] private GameObject panel2;

    [Header("전환 간격")]
    [Min(0.01f)]
    [SerializeField] private float intervalSeconds = 0.5f;

    private Coroutine alternateCoroutine;

    private void Reset()
    {
        Transform first = transform.Find("Panel1");
        Transform second = transform.Find("Panel2");

        if (first != null)
            panel1 = first.gameObject;

        if (second != null)
            panel2 = second.gameObject;
    }

    private void OnEnable()
    {
        if (alternateCoroutine != null)
            StopCoroutine(alternateCoroutine);

        alternateCoroutine = StartCoroutine(AlternatePanels());
    }

    private void OnDisable()
    {
        if (alternateCoroutine != null)
        {
            StopCoroutine(alternateCoroutine);
            alternateCoroutine = null;
        }
    }

    private IEnumerator AlternatePanels()
    {
        bool showFirst = true;

        while (true)
        {
            if (panel1 != null)
                panel1.SetActive(showFirst);

            if (panel2 != null)
                panel2.SetActive(!showFirst);

            yield return new WaitForSecondsRealtime(intervalSeconds);
            showFirst = !showFirst;
        }
    }
}
