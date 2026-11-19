#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OrderPrefabBuilder
{
    [MenuItem("Tools/Mock Trading/Create Order Row Prefab")]
    public static void Create()
    {
        string path = EditorUtility.SaveFilePanelInProject("주문 행 프리팹 저장", "OrderRow", "prefab", "저장 위치를 선택하세요.");
        if (string.IsNullOrEmpty(path)) return;
        var root = new GameObject("OrderRow", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(OrderRowUI));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(320, 250);
        root.GetComponent<LayoutElement>().preferredHeight = 250;
        var layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 4;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var row = root.GetComponent<OrderRowUI>();
        row.selectedBackground = root.GetComponent<Image>();
        row.sideText = Label(root.transform, "Side", "매수");
        row.statusText = Label(root.transform, "Status", "미체결");
        row.priceText = Label(root.transform, "Price", "주문가격");
        row.quantityText = Label(root.transform, "Quantity", "주문수량");
        row.remainingText = Label(root.transform, "Remaining", "미체결수량");
        row.orderDateText = Label(root.transform, "OrderDate", "주문 날짜");
        row.orderTimeText = Label(root.transform, "OrderTime", "주문 시간");
        var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttons.transform.SetParent(root.transform, false);
        buttons.GetComponent<LayoutElement>().preferredHeight = 32;
        var buttonLayout = buttons.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.childControlWidth = true; buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        row.selectButton = MakeButton(buttons.transform, "Select", "선택");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Selection.activeObject = prefab;
    }
    static TMP_Text Label(Transform parent, string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text; label.fontSize = 16; label.color = Color.black; label.raycastTarget = false;
        go.GetComponent<LayoutElement>().preferredHeight = 24;
        return label;
    }
    static Button MakeButton(Transform parent, string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(.94f, .94f, .94f);
        var label = Label(go.transform, "Label", text);
        label.alignment = TextAlignmentOptions.Center;
        var rect = label.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }
}
#endif
