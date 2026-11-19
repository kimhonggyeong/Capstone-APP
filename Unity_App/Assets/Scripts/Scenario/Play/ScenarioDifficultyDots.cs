using UnityEngine;
using UnityEngine.UI;

public class ScenarioDifficultyDots : MonoBehaviour
{
    [Tooltip("빈 동그라미의 자식인 채워진 동그라미 Image를 왼쪽부터 연결")]
    [SerializeField]
    private Image[] filledDots = new Image[3];

    public void SetDifficulty(string difficulty)
    {
        int level = GetDifficultyLevel(difficulty);

        for (int i = 0; i < filledDots.Length; i++)
        {
            if (filledDots[i] != null)
                filledDots[i].gameObject.SetActive(i < level);
        }
    }

    private int GetDifficultyLevel(string difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
            return 0;

        switch (difficulty.Trim().ToLowerInvariant())
        {
            case "하":
            case "쉬움":
            case "easy":
            case "beginner":
                return 1;

            case "중":
            case "보통":
            case "normal":
            case "medium":
            case "intermediate":
                return 2;

            case "상":
            case "어려움":
            case "hard":
            case "advanced":
                return 3;

            default:
                Debug.LogWarning(
                    "[ScenarioDifficultyDots] 알 수 없는 난이도: " +
                    difficulty,
                    this
                );
                return 0;
        }
    }
}
