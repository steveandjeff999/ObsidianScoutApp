using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ObsidianScout.Models;

public class SyncResultDetail
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("flashes")]
    public List<string>? Flashes { get; set; }
}

public class SyncTriggerResults
{
    [JsonPropertyName("teams_sync")]
    public SyncResultDetail? TeamsSync { get; set; }

    [JsonPropertyName("matches_sync")]
    public SyncResultDetail? MatchesSync { get; set; }

    [JsonPropertyName("alliance_sync")]
    public object? AllianceSync { get; set; }
}

public class SyncTriggerResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("results")]
    public SyncTriggerResults? Results { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
