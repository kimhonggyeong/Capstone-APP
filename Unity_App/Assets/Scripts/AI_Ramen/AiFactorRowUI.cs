using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AiFactorRowUI : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text weightText;
    public Slider weightSlider;

    public void SetData(AiFactor factor)
    {
        if (factor == null) return;
        if (titleText != null) titleText.text = factor.factor ?? "-";
        if (descriptionText != null) descriptionText.text = $"{factor.type}요인 · {factor.direction}";
        if (weightText != null) weightText.text = $"{factor.weight:0.#}%";
        if (weightSlider != null)
        {
            weightSlider.minValue = 0f;
            weightSlider.maxValue = 100f;
            weightSlider.interactable = false;
            weightSlider.SetValueWithoutNotify(Mathf.Clamp(factor.weight, 0f, 100f));
        }
    }
}