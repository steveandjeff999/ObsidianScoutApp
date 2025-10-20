using System.Text.Json.Serialization;

namespace ObsidianScout.Models;

/// <summary>
/// Represents a prediction for a single team in a match
/// </summary>
public class TeamMatchPrediction
{
    public int TeamNumber { get; set; }
    public string TeamName { get; set; } = string.Empty;
    
    // Predicted points by period
    public double PredictedAutoPoints { get; set; }
    public double PredictedTeleopPoints { get; set; }
    public double PredictedEndgamePoints { get; set; }
    public double PredictedTotalPoints { get; set; }
    
    // Standard deviations for confidence ranges
    public double AutoPointsStd { get; set; }
    public double TeleopPointsStd { get; set; }
    public double EndgamePointsStd { get; set; }
    public double TotalPointsStd { get; set; }
    
    // Historical data used
    public int MatchCount { get; set; }
    public double Consistency { get; set; }
    
    // Alliance color for display
    public string Alliance { get; set; } = string.Empty; // "red" or "blue"
}

/// <summary>
/// Represents a complete match prediction with both alliances
/// </summary>
public class MatchPrediction
{
    public Match Match { get; set; } = new();
    
    // Team predictions
    public List<TeamMatchPrediction> RedAlliance { get; set; } = new();
    public List<TeamMatchPrediction> BlueAlliance { get; set; } = new();
    
    // Alliance totals
    public double RedPredictedAutoPoints { get; set; }
    public double RedPredictedTeleopPoints { get; set; }
    public double RedPredictedEndgamePoints { get; set; }
    public double RedPredictedTotalPoints { get; set; }
    
    public double BluePredictedAutoPoints { get; set; }
    public double BluePredictedTeleopPoints { get; set; }
    public double BluePredictedEndgamePoints { get; set; }
    public double BluePredictedTotalPoints { get; set; }
    
    // Win probability (0-1)
    public double RedWinProbability { get; set; }
    public double BlueWinProbability { get; set; }
    
    // Confidence indicators
    public bool HasSufficientData { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
}

/// <summary>
/// Historical statistics for a team used for prediction
/// </summary>
public class TeamHistoricalStats
{
    public int TeamNumber { get; set; }
    public string TeamName { get; set; } = string.Empty;
    
    public double AvgAutoPoints { get; set; }
    public double StdAutoPoints { get; set; }
    
    public double AvgTeleopPoints { get; set; }
    public double StdTeleopPoints { get; set; }
    
    public double AvgEndgamePoints { get; set; }
    public double StdEndgamePoints { get; set; }
    
    public double AvgTotalPoints { get; set; }
    public double StdTotalPoints { get; set; }
    
    public int MatchCount { get; set; }
    public double Consistency { get; set; }
}

/// <summary>
/// Request to predict match outcome based on team data
/// </summary>
public class MatchPredictionRequest
{
    [JsonPropertyName("match_id")]
    public int MatchId { get; set; }
    
    [JsonPropertyName("event_id")]
    public int EventId { get; set; }
}

/// <summary>
/// Response from match prediction API (if server-side prediction is implemented)
/// </summary>
public class MatchPredictionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    [JsonPropertyName("match")]
    public Match? Match { get; set; }
    
    [JsonPropertyName("red_alliance")]
    public AlliancePrediction? RedAlliance { get; set; }
    
    [JsonPropertyName("blue_alliance")]
    public AlliancePrediction? BlueAlliance { get; set; }
    
    [JsonPropertyName("red_win_probability")]
    public double RedWinProbability { get; set; }
    
    [JsonPropertyName("blue_win_probability")]
    public double BlueWinProbability { get; set; }
}

public class AlliancePrediction
{
    [JsonPropertyName("teams")]
    public List<int> Teams { get; set; } = new();
    
    [JsonPropertyName("predicted_score")]
    public double PredictedScore { get; set; }
    
    [JsonPropertyName("predicted_auto_points")]
    public double PredictedAutoPoints { get; set; }
    
    [JsonPropertyName("predicted_teleop_points")]
    public double PredictedTeleopPoints { get; set; }
    
    [JsonPropertyName("predicted_endgame_points")]
    public double PredictedEndgamePoints { get; set; }
    
    [JsonPropertyName("team_details")]
    public List<TeamPredictionDetail> TeamDetails { get; set; } = new();
}

public class TeamPredictionDetail
{
    [JsonPropertyName("team_number")]
    public int TeamNumber { get; set; }
    
    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;
    
    [JsonPropertyName("predicted_auto_points")]
    public double PredictedAutoPoints { get; set; }
    
    [JsonPropertyName("predicted_teleop_points")]
    public double PredictedTeleopPoints { get; set; }
    
    [JsonPropertyName("predicted_endgame_points")]
    public double PredictedEndgamePoints { get; set; }
    
    [JsonPropertyName("predicted_total_points")]
    public double PredictedTotalPoints { get; set; }
    
    [JsonPropertyName("match_count")]
    public int MatchCount { get; set; }
    
    [JsonPropertyName("consistency")]
    public double Consistency { get; set; }
}
