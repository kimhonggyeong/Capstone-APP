using TMPro;
using UnityEngine;

public class ScenarioHoldingListController : MonoBehaviour
{
    [Header("보유 주식 목록")]
    [Tooltip("Scroll View의 Viewport/Content를 연결")]
    [SerializeField] private Transform content;

    [Tooltip("ScenarioHoldingItem이 붙은 보유 주식 프리팹")]
    [SerializeField] private ScenarioHoldingItem holdingItemPrefab;

    [Header("빈 목록 문구 - 선택")]
    [SerializeField] private TMP_Text emptyText;

    public void Refresh(ScenarioRuntimeData scenario)
    {
        ClearItems();

        PositionInfo[] positions =
            scenario?.turnData?.portfolio?.positions;

        bool hasPositions =
            positions != null && positions.Length > 0;

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(!hasPositions);
            emptyText.text = "보유 중인 주식이 없습니다.";
        }

        if (!hasPositions ||
            content == null ||
            holdingItemPrefab == null)
        {
            return;
        }

        foreach (PositionInfo position in positions)
        {
            if (position == null || position.quantity <= 0)
                continue;

            ScenarioHoldingItem item =
                Instantiate(holdingItemPrefab, content);

            item.SetData(position);
        }
    }

    // 주문 성공 후 ScenarioOrderController의
    // On Portfolio Updated 이벤트에 연결할 함수입니다.
    public void RefreshSelectedScenario()
    {
        if (ScenarioDataManager.Instance == null)
            return;

        Refresh(ScenarioDataManager.Instance.SelectedScenario);
    }

    private void ClearItems()
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }
    }
}