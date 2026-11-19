using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XCharts.Runtime;

public class Reaction_ResultPanel : MonoBehaviour
{
    private Coroutine refreshRoutine;
    private bool refreshPending;

    [Header("레이아웃 갱신")]
    [Tooltip("결과 Scroll View의 Viewport/Content 중 Content를 연결하세요.")]
    public RectTransform layoutRoot;

    [Header("입력 내용")]
    public TMP_Text inputText;
    public TMP_Text inputTypeText;
    [Header("Summary")]
    public TMP_Text stockNameText;
    public TMP_Text sentimentText;
    public TMP_Text sentimentOneLinerText;
    public TMP_Text overallExplanationText;

    [Header("Impact")]
    public TMP_Text impactDirectionText;
    public TMP_Text impactStrengthText;
    public TMP_Text timeHorizonText;
    public TMP_Text relatedIndustriesText;
    public TMP_Text keyKeywordsText;
    [Header("영향 방향 아이콘 / 강도 점")]
    public Image impactDirectionImage;
    public Sprite positiveImpactSprite, negativeImpactSprite, neutralImpactSprite;
    [Tooltip("빈 동그라미 각각의 자식인 색칠 동그라미 5개")]
    public GameObject[] impactStrengthFillDots = new GameObject[5];
    public Color positiveColor = new Color(1f, .35f, .15f, 1f);
    public Color negativeColor = new Color(.2f, .45f, 1f, 1f);
    public Color neutralColor = new Color(.55f, .55f, .55f, 1f);

    [Header("Pressure")]
    public PercentBarUI percentBar;

    public TMP_Text buyPercentText;
    public TMP_Text sellPercentText;
    public TMP_Text holdPercentText;

    public TMP_Text pressureHeadlineText;
    [Header("시장 압력 차트 / 슬라이더")]
    public RingChart pressureRingChart;
    public TMP_Text pressureRingCenterText;
    public Slider buyPressureSlider, sellPressureSlider, holdPressureSlider;
    [Header("시장 분위기")]
    public Slider sentimentSlider;
    public TMP_Text sentimentScoreText;

    [Header("Confidence")]
    public TMP_Text confidenceGradeText;
    public TMP_Text confidenceScoreText;
    public TMP_Text confidenceExplanationText;
    public Slider confidenceSlider;

    [Header("Uncertainty")]
    public TMP_Text uncertaintyText;

    [Header("Agents")]
    public ReactionAgentRowUI individualRow;
    public ReactionAgentRowUI institutionalRow;
    public ReactionAgentRowUI foreignRow;
    public ReactionAgentRowUI shortTermRow;
    public ReactionAgentRowUI longTermRow;
    [Header("이전 통합 텍스트 (새 행 UI 사용 시 비워도 됨)")]
    public TMP_Text individualText;
    public TMP_Text institutionalText;
    public TMP_Text foreignText;
    public TMP_Text shortTermText;
    public TMP_Text longTermText;

    /*[Header("Debug")]
    //public TMP_Text metaText;*/

    public void SetResult(SimulationResponseDto data)
    {
        if (data == null)
        {
            Debug.LogWarning("SimulationResponseDto is null");
            return;
        }

        if (inputText != null) inputText.text = data.input_text ?? "";
        if (inputTypeText != null) inputTypeText.text = InputTypeLabel(data.input_type);

        // =========================
        // 기본 정보
        // =========================
        if (stockNameText != null && data.selected_stock != null)
        {
            stockNameText.text = $"{data.selected_stock.name} ({data.selected_stock.code})";
        }

        // =========================
        // 시장 분위기
        // =========================
        if (data.market_sentiment != null)
        {
            if (sentimentText != null)
                sentimentText.text = data.market_sentiment.label_ko;

            if (sentimentOneLinerText != null)
                sentimentOneLinerText.text = data.market_sentiment.one_liner;
        }
        ApplySentimentSlider(data.market_sentiment != null ? data.market_sentiment.code : null);

        if (overallExplanationText != null)
            overallExplanationText.text = data.overall_explanation ?? "";

        // =========================
        // 영향 분석
        // =========================
        if (data.impact_analysis != null)
        {
            if (impactDirectionText != null)
                impactDirectionText.text = data.impact_analysis.impact_direction_ko;

            if (impactStrengthText != null)
                impactStrengthText.text = data.impact_analysis.impact_strength_ko;

            if (timeHorizonText != null)
                timeHorizonText.text = data.impact_analysis.time_horizon_ko;

            if (relatedIndustriesText != null)
            {
                relatedIndustriesText.text = data.impact_analysis.related_industries != null
                    ? string.Join(", ", data.impact_analysis.related_industries)
                    : "";
            }

            if (keyKeywordsText != null)
            {
                keyKeywordsText.text = data.impact_analysis.key_keywords != null
                    ? string.Join(", ", data.impact_analysis.key_keywords)
                    : "";
            }
            ApplyImpactVisuals(data.impact_analysis);
        }

        // =========================
        // 시장 압력
        // =========================
        if (data.market_pressure != null)
        {
            // 퍼센트바 연결
            if (percentBar != null)
            {
                percentBar.SetValues(
                    data.market_pressure.buy,
                    data.market_pressure.sell,
                    data.market_pressure.hold
                );
            }

            // 텍스트 3개 연결
            if (buyPercentText != null)
                buyPercentText.text = $"{data.market_pressure.buy}%";

            if (sellPercentText != null)
                sellPercentText.text = $"{data.market_pressure.sell}%";

            if (holdPercentText != null)
                holdPercentText.text = $"{data.market_pressure.hold}%";

            if (pressureHeadlineText != null)
                pressureHeadlineText.text = data.market_pressure.headline;
            SetPercentSlider(buyPressureSlider, data.market_pressure.buy);
            SetPercentSlider(sellPressureSlider, data.market_pressure.sell);
            SetPercentSlider(holdPressureSlider, data.market_pressure.hold);
            DrawPressureRing(data.market_pressure);
        }

        // =========================
        // 분석 신뢰도
        // =========================
        if (data.analysis_confidence != null)
        {
            if (confidenceGradeText != null)
                confidenceGradeText.text = data.analysis_confidence.grade_ko;

            if (confidenceScoreText != null)
                confidenceScoreText.text = $"{data.analysis_confidence.score * 100f:0}%";

            if (confidenceExplanationText != null)
                confidenceExplanationText.text = data.analysis_confidence.explanation;
            SetPercentSlider(confidenceSlider, data.analysis_confidence.score * 100f);
        }

        // =========================
        // 불확실성 요인
        // =========================
        if (uncertaintyText != null)
        {
            uncertaintyText.text = data.uncertainty_factors != null
                ? string.Join("\n", data.uncertainty_factors.Select(x => $"- {x}"))
                : "";
        }

        // =========================
        // 투자자별 반응
        // =========================
        SetAgentTexts(data);

        refreshPending = true;
        TryStartLayoutRefresh();

        // =========================
        // 디버그 / 메타 정보
        // =========================
/*        if (metaText != null && data.meta != null)
        {
            metaText.text =
                $"LLM: {data.meta.llm_status}\n" +
                $"Fallback: {data.meta.fallback_used}\n" +
                $"Model: {data.meta.llm_model}";
        }*/
    }

    private void OnEnable()
    {
        TryStartLayoutRefresh();
    }

    public void RefreshLayout()
    {
        refreshPending = true;
        TryStartLayoutRefresh();
    }

    private void TryStartLayoutRefresh()
    {
        if (!refreshPending || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (refreshRoutine != null)
            StopCoroutine(refreshRoutine);

        refreshRoutine = StartCoroutine(RefreshLayoutRoutine());
    }

    private IEnumerator RefreshLayoutRoutine()
    {
        // AiCompareView와 동일하게 활성화/텍스트 변경 다음 프레임에 갱신합니다.
        yield return null;
        ForceTextMeshes();
        Canvas.ForceUpdateCanvases();
        RebuildLayout();

        // ContentSizeFitter/VerticalLayoutGroup의 후속 계산을 한 번 더 반영합니다.
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        RebuildLayout();
        refreshRoutine = null;
        refreshPending = false;
    }

    private void ForceTextMeshes()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
            text.ForceMeshUpdate(true, true);
    }

    private void RebuildLayout()
    {
        RectTransform target = layoutRoot != null
            ? layoutRoot
            : transform as RectTransform;
        if (target != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);
    }

    private void OnDisable()
    {
        if (refreshRoutine == null)
            return;

        StopCoroutine(refreshRoutine);
        refreshRoutine = null;
        refreshPending = true;
    }

    void ApplyImpactVisuals(ImpactAnalysisDto impact)
    {
        bool positive = impact.impact_direction == "positive";
        bool negative = impact.impact_direction == "negative";
        Color color = positive ? positiveColor : negative ? negativeColor : neutralColor;
        if (impactDirectionImage != null)
        {
            impactDirectionImage.sprite = positive ? positiveImpactSprite : negative ? negativeImpactSprite : neutralImpactSprite;
            impactDirectionImage.color = color;
            impactDirectionImage.enabled = impactDirectionImage.sprite != null;
        }
        if (impactDirectionText != null) impactDirectionText.color = color;
        if (impactStrengthText != null) impactStrengthText.color = color;
        int count = impact.impact_strength == "high" ? 5 : impact.impact_strength == "medium" ? 3 : 1;
        for (int i = 0; i < impactStrengthFillDots.Length; i++)
        {
            GameObject dot = impactStrengthFillDots[i];
            if (dot == null) continue;
            dot.SetActive(i < count);
            Image image = dot.GetComponent<Image>();
            if (image != null) image.color = color;
        }
    }

    void DrawPressureRing(MarketPressureDto pressure)
    {
        if (pressureRingCenterText != null)
        {
            int maximum = Mathf.Max(pressure.buy, Mathf.Max(pressure.sell, pressure.hold));
            pressureRingCenterText.text = $"{DirectionLabel(pressure.dominant)}\n{maximum}%";
        }
        if (pressureRingChart == null) return;
        Ring serie = pressureRingChart.GetSerie(0) as Ring;
        if (serie == null) serie = pressureRingChart.AddSerie<Ring>("시장 압력");
        serie.ClearData();
        int ringMaximum = Mathf.Max(pressure.buy, Mathf.Max(pressure.sell, pressure.hold));
        pressureRingChart.AddData(serie.index, ringMaximum, 100, DirectionLabel(pressure.dominant));
        pressureRingChart.RefreshChart();
    }

    void ApplySentimentSlider(string code)
    {
        float value = code == "very_positive" ? 100f : code == "positive" ? 75f :
            code == "negative" ? 25f : code == "very_negative" ? 0f : 50f;
        SetPercentSlider(sentimentSlider, value);
        if (sentimentScoreText != null) sentimentScoreText.text = $"{value:0}%";
    }

    static void SetPercentSlider(Slider slider, float value)
    {
        if (slider == null) return;
        slider.minValue = 0f; slider.maxValue = 100f;
        slider.SetValueWithoutNotify(Mathf.Clamp(value, 0f, 100f));
        slider.interactable = false;
    }

    static string DirectionLabel(string value)
    {
        return value == "buy" ? "매수 우세" : value == "sell" ? "매도 우세" : "관망 우세";
    }

    static string InputTypeLabel(string value)
    {
        switch (value)
        {
            case "real_news": return "뉴스 / 이벤트";
            case "company_information": return "기업 공시";
            case "industry_information": return "산업 정보";
            case "hypothetical_scenario": return "가정 시나리오";
            case "economic_market_event": return "경제 / 시장 이벤트";
            default: return "기타";
        }
    }

    private void SetAgentTexts(SimulationResponseDto data)
    {
        if (data.agent_reactions == null)
            return;

        if (individualRow != null) individualRow.gameObject.SetActive(false);
        if (institutionalRow != null) institutionalRow.gameObject.SetActive(false);
        if (foreignRow != null) foreignRow.gameObject.SetActive(false);
        if (shortTermRow != null) shortTermRow.gameObject.SetActive(false);
        if (longTermRow != null) longTermRow.gameObject.SetActive(false);

        // 이전 결과가 남는 것 방지
        if (individualText != null) individualText.text = "";
        if (institutionalText != null) institutionalText.text = "";
        if (foreignText != null) foreignText.text = "";
        if (shortTermText != null) shortTermText.text = "";
        if (longTermText != null) longTermText.text = "";

        foreach (var agent in data.agent_reactions)
        {
            string text = BuildAgentText(agent);

            switch (agent.agent_type)
            {
                case "individual_investor":
                    if (individualRow != null) individualRow.Bind(agent);
                    if (individualText != null)
                        individualText.text = text;
                    break;

                case "institutional_investor":
                    if (institutionalRow != null) institutionalRow.Bind(agent);
                    if (institutionalText != null)
                        institutionalText.text = text;
                    break;

                case "foreign_investor":
                    if (foreignRow != null) foreignRow.Bind(agent);
                    if (foreignText != null)
                        foreignText.text = text;
                    break;

                case "short_term_investor":
                    if (shortTermRow != null) shortTermRow.Bind(agent);
                    if (shortTermText != null)
                        shortTermText.text = text;
                    break;

                case "long_term_investor":
                    if (longTermRow != null) longTermRow.Bind(agent);
                    if (longTermText != null)
                        longTermText.text = text;
                    break;
            }
        }
    }

    private string BuildAgentText(AgentReactionDto agent)
    {
        if (agent == null)
            return "";

        string reasons = agent.key_reasons != null
            ? string.Join(", ", agent.key_reasons)
            : "";

        string risks = agent.risk_factors != null
            ? string.Join(", ", agent.risk_factors)
            : "";

        return
            $"{agent.agent_name_ko}\n" +
            $"{agent.reaction_direction_ko} / {agent.reaction_strength_ko}\n" +
            $"근거: {reasons}\n" +
            $"리스크: {risks}\n" +
            $"{agent.comment}";
    }
}
