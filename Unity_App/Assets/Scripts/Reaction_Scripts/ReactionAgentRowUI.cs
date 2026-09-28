using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReactionAgentRowUI : MonoBehaviour
{
    [Header("행 텍스트")]
    public TMP_Text agentNameText;
    public TMP_Text directionText;
    public TMP_Text relevanceText;
    public TMP_Text commentText;
    [Header("매수/매도/관망 배지")]
    public Image directionBackground;
    public Color buyTextColor = new Color(.85f, .1f, .1f, 1f);
    public Color buyBackgroundColor = new Color(1f, .82f, .82f, 1f);
    public Color sellTextColor = new Color(.1f, .3f, .8f, 1f);
    public Color sellBackgroundColor = new Color(.82f, .88f, 1f, 1f);
    public Color holdTextColor = new Color(.35f, .35f, .35f, 1f);
    public Color holdBackgroundColor = new Color(.86f, .86f, .86f, 1f);

    public void Bind(AgentReactionDto agent)
    {
        if (agent == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);
        if (agentNameText != null) agentNameText.text = agent.agent_name_ko ?? "-";
        if (directionText != null) directionText.text = agent.reaction_direction_ko ?? DirectionLabel(agent.reaction_direction);
        if (relevanceText != null) relevanceText.text = "입력 내용 관련도: " + RelevanceLabel(agent.input_relevance);
        if (commentText != null) commentText.text = agent.comment ?? "";
        bool buy = agent.reaction_direction == "buy";
        bool sell = agent.reaction_direction == "sell";
        Color textColor = buy ? buyTextColor : sell ? sellTextColor : holdTextColor;
        Color backgroundColor = buy ? buyBackgroundColor : sell ? sellBackgroundColor : holdBackgroundColor;
        if (directionText != null) directionText.color = textColor;
        if (directionBackground != null) directionBackground.color = backgroundColor;
    }

    static string RelevanceLabel(string value)
    {
        return value == "high" ? "높음" : value == "low" ? "낮음" : "보통";
    }
    static string DirectionLabel(string value)
    {
        return value == "buy" ? "매수" : value == "sell" ? "매도" : "관망";
    }
}
