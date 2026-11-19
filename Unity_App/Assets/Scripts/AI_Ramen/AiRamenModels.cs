using System;
using System.Collections.Generic;

[Serializable]
public class AiAnalyzeRequest
{
    public string symbol;
    public string stock_name;
}

[Serializable]
public class AiFactor
{
    public string type;
    public string direction;
    public string factor;
    public float weight;
}

[Serializable]
public class AiJudgmentResponse
{
    public string symbol;
    public string judge;
    public float confidence;
    public Dictionary<string, float> probabilities;
    public string summary;
    public List<AiFactor> factors;
    public string computed_at;
}

[Serializable]
public class AiHistoryEntry
{
    public string time;
    public string judge;
    public string reason;
    public bool changed;
    public float confidence;
    public Dictionary<string, float> probabilities;
}

[Serializable]
public class AiCompareRequest
{
    public string symbol;
    public string user_judge;
}

[Serializable]
public class AiCompareResponse
{
    public string user_judge;
    public string ai_judge;
    public Dictionary<string, float> ai_probabilities;
    public float? ai_confidence;
    public string explanation;
    public Dictionary<string, List<string>> highlighted_factors;

    // 비교 응답의 값을 우선한다. 이전 서버 응답일 때만 같은 종목의 분석 값을 쓴다.
    public float? GetConfidence(AiJudgmentResponse fallback)
    {
        if (ai_confidence.HasValue) return ai_confidence.Value;
        float probability;
        if (ai_probabilities != null && ai_judge != null &&
            ai_probabilities.TryGetValue(ai_judge, out probability) && probability > 0f)
            return probability;
        return fallback != null ? (float?)fallback.confidence : null;
    }
}

[Serializable]
public class AiApiError
{
    public string detail;
}