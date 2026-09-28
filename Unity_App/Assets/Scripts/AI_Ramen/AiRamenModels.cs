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
    public string explanation;
    public Dictionary<string, List<string>> highlighted_factors;
}

[Serializable]
public class AiApiError
{
    public string detail;
}