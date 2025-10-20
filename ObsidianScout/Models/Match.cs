using System.Text.Json.Serialization;

namespace ObsidianScout.Models;

public class Match
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("match_number")]
    public int MatchNumber { get; set; }
    
    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = string.Empty;
    
    [JsonPropertyName("red_alliance")]
    public string RedAlliance { get; set; } = string.Empty;
    
    [JsonPropertyName("blue_alliance")]
    public string BlueAlliance { get; set; } = string.Empty;
    
    [JsonPropertyName("red_score")]
    public int? RedScore { get; set; }
    
    [JsonPropertyName("blue_score")]
    public int? BlueScore { get; set; }
    
    [JsonPropertyName("winner")]
    public string? Winner { get; set; }
}

public class MatchesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("matches")]
    public List<Match> Matches { get; set; } = new();
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
