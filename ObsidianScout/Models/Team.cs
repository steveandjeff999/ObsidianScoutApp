using System.Text.Json.Serialization;

namespace ObsidianScout.Models;

public class Team
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("team_number")]
    public int TeamNumber { get; set; }
    
    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("scouting_data_count")]
    public int ScoutingDataCount { get; set; }
}

public class TeamsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("teams")]
    public List<Team> Teams { get; set; } = new();
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("total")]
    public int Total { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
