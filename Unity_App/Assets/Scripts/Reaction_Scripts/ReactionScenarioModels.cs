using System;
using System.Collections.Generic;

[Serializable]
public class SimulationRequestDto
{
    public string user_id;
    public SelectedStockDto selected_stock;
    public string input_text;
    public string input_type_hint; // null 가능
}

[Serializable]
public class SelectedStockDto
{
    public string code;
    public string name;
}

[Serializable]
public class SimulationResponseDto
{
    public string status;
    public string simulation_id;
    public SelectedStockDto selected_stock;
    public string input_text;
    public string input_type;
    public ImpactAnalysisDto impact_analysis;
    public CurrentStockContextDto current_stock_context;
    public MarketPressureDto market_pressure;
    public MarketSentimentDto market_sentiment;
    public AnalysisConfidenceDto analysis_confidence;
    public List<string> uncertainty_factors;
    public List<AgentReactionDto> agent_reactions;
    public string overall_explanation;
    public SimulationMetaDto meta;
    public string created_at;
}

[Serializable]
public class ImpactAnalysisDto
{
    public string impact_direction;
    public string impact_direction_ko;
    public string impact_strength;
    public string impact_strength_ko;
    public List<string> related_industries;
    public string time_horizon;
    public string time_horizon_ko;
    public List<string> key_keywords;
}

[Serializable]
public class CurrentStockContextDto
{
    public string code;
    public string name;
    public string industry;
    public int? current_price;
    public float? daily_change_rate;
    public string volume_trend;
    public float? market_cap_trillion;
    public string data_source;
    public bool is_realtime;
    public string observed_at;
}

[Serializable]
public class MarketPressureDto
{
    public int buy;
    public int sell;
    public int hold;
    public string dominant;
    public string headline;
}

[Serializable]
public class MarketSentimentDto
{
    public string code;
    public string label_ko;
    public string one_liner;
}

[Serializable]
public class AnalysisConfidenceDto
{
    public float score;
    public string grade;
    public string grade_ko;
    public string explanation;
}

[Serializable]
public class AgentReactionDto
{
    public string agent_type;
    public string agent_name_ko;
    public string reaction_direction;
    public string reaction_direction_ko;
    public string reaction_strength;
    public string reaction_strength_ko;
    public float base_weight;
    public string input_relevance;
    public List<string> key_reasons;
    public string comment;
    public List<string> risk_factors;
}

[Serializable]
public class SimulationMetaDto
{
    public string llm_model;
    public string llm_status;
    public bool fallback_used;
    public List<string> fallback_modules;
    public string stock_data_source;
    public string db_save_status;
}

[Serializable]
public class RejectedResponseDto
{
    public string status;
    public string reason_code;
    public string message;
}