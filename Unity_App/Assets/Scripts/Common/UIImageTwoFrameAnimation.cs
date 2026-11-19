using UnityEngine;
using UnityEngine.UI;

public class UIImageTwoFrameAnimationWithTransform : MonoBehaviour
{
    [Header("Target")]
    public Image targetImage;
    public RectTransform targetRect;

    [Header("Sprites")]
    public Sprite firstSprite;
    public Sprite secondSprite;

    [Header("Animation")]
    public float changeInterval = 0.5f;

    [Header("First Image Setting")]
    public Vector2 firstPosition = Vector2.zero;
    public Vector2 firstSize = new Vector2(100f, 100f);
    public float firstRotationZ = 0f;

    [Header("Second Image Setting")]
    public Vector2 secondPosition = Vector2.zero;
    public Vector2 secondSize = new Vector2(100f, 100f);
    public float secondRotationZ = 0f;

    private float timer = 0f;
    private bool isFirst = true;

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetRect == null)
            targetRect = GetComponent<RectTransform>();
    }

    private void Start()
    {
        ApplyFirstImage();
    }

    private void Update()
    {
        if (targetImage == null || targetRect == null)
            return;

        if (firstSprite == null || secondSprite == null)
            return;

        timer += Time.deltaTime;

        if (timer >= changeInterval)
        {
            timer = 0f;
            isFirst = !isFirst;

            if (isFirst)
                ApplyFirstImage();
            else
                ApplySecondImage();
        }
    }

    private void ApplyFirstImage()
    {
        targetImage.sprite = firstSprite;

        targetRect.anchoredPosition = firstPosition;
        targetRect.sizeDelta = firstSize;
        targetRect.localRotation = Quaternion.Euler(0f, 0f, firstRotationZ);
    }

    private void ApplySecondImage()
    {
        targetImage.sprite = secondSprite;

        targetRect.anchoredPosition = secondPosition;
        targetRect.sizeDelta = secondSize;
        targetRect.localRotation = Quaternion.Euler(0f, 0f, secondRotationZ);
    }
}