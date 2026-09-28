using UnityEngine;
using UnityEngine.UI;

public class AiComparePanelController : MonoBehaviour
{
    [Header("연결")]
    public AiRamenController aiRamenController;

    [Tooltip("매수/관망/매도 선택 버튼이 들어 있는 패널")]
    public GameObject selectorPanel;
    public GameObject buyResultPanel;
    public GameObject holdResultPanel;
    public GameObject sellResultPanel;

    [Header("선택 버튼 이미지")]
    public Image buyButtonImage;
    public Image holdButtonImage;
    public Image sellButtonImage;

    [Header("색상")]
    public Color selectedColor = new Color32(0xF6, 0x6B, 0x24, 0xFF);
    public Color unselectedColor = new Color32(0xD9, 0xD9, 0xD9, 0xFF);

    public void SelectBuy()
    {
        ShowSelection(buy: true, hold: false, sell: false);
        if (aiRamenController != null) aiRamenController.CompareBuy();
    }

    public void SelectHold()
    {
        ShowSelection(buy: false, hold: true, sell: false);
        if (aiRamenController != null) aiRamenController.CompareHold();
    }

    public void SelectSell()
    {
        ShowSelection(buy: false, hold: false, sell: true);
        if (aiRamenController != null) aiRamenController.CompareSell();
    }

    public void ShowDefaultBuy()
    {
        SelectBuy();
    }

    private void ShowSelection(bool buy, bool hold, bool sell)
    {
        // 선택 버튼 영역은 결과가 바뀌어도 계속 표시한다.
        SetActive(selectorPanel, true);
        SetActive(buyResultPanel, buy);
        SetActive(holdResultPanel, hold);
        SetActive(sellResultPanel, sell);

        SetColor(buyButtonImage, buy ? selectedColor : unselectedColor);
        SetColor(holdButtonImage, hold ? selectedColor : unselectedColor);
        SetColor(sellButtonImage, sell ? selectedColor : unselectedColor);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }

    private static void SetColor(Image target, Color color)
    {
        if (target != null) target.color = color;
    }
}