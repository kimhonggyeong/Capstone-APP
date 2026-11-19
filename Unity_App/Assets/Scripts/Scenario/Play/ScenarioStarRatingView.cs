using UnityEngine;
using UnityEngine.UI;

public class ScenarioStarRatingView : MonoBehaviour
{
    [SerializeField] private Image[] colorStars = new Image[5];

    public void SetScore(float score)
    {
        float clamped = Mathf.Clamp(score, 0f, colorStars.Length);
        for (int i = 0; i < colorStars.Length; i++)
        {
            Image star = colorStars[i];
            if (star == null) continue;
            star.type = Image.Type.Filled;
            star.fillMethod = Image.FillMethod.Horizontal;
            star.fillOrigin = (int)Image.OriginHorizontal.Left;
            star.fillClockwise = true;
            star.fillAmount = Mathf.Clamp01(clamped - i);
        }
    }
}
