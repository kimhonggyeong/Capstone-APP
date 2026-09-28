using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StockButtonItem : MonoBehaviour
{
    [SerializeField] private Button button;

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text codeText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text changeRateText;

    [SerializeField] private Color riseColor = Color.red;
    [SerializeField] private Color fallColor = Color.blue;
    [SerializeField] private Color neutralColor = Color.gray;

    private AssetInfo asset;
    private Action<AssetInfo> clickCallback;

    public void Initialize(
        AssetInfo assetData,
        Action<AssetInfo> onClick)
    {
        asset = assetData;
        clickCallback = onClick;

        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError(
                $"[{name}] Button 컴포넌트를 연결하세요.", this
            );
            return;
        }

        if (asset == null)
        {
            button.interactable = false;
            return;
        }

        if (nameText != null)
            nameText.text = asset.name;

        if (codeText != null)
            codeText.text = asset.asset_id;

        if (priceText != null)
        {
            priceText.text = asset.data_available
                ? $"{asset.current_price:N0}원"
                : "데이터 없음";
        }

        if (changeRateText != null)
        {
            changeRateText.text = asset.data_available
                ? asset.change_pct.ToString("+0.00;-0.00;0.00") + "%"
                : "-";

            changeRateText.color = !asset.data_available
                ? neutralColor
                : asset.change_pct > 0
                    ? riseColor
                    : asset.change_pct < 0
                        ? fallColor
                        : neutralColor;
        }

        button.interactable = true;

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        if (asset == null)
            return;

        Debug.Log(
            $"[주식 버튼 클릭] {asset.name} / {asset.asset_id}",
            this
        );

        if (clickCallback == null)
        {
            Debug.LogError("종목 선택 콜백이 연결되지 않았습니다.", this);
            return;
        }

        clickCallback.Invoke(asset);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }
}