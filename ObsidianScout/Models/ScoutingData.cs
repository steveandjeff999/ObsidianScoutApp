using System.Text.Json.Serialization;

namespace ObsidianScout.Models;

public class ScoutingData
{
    [JsonPropertyName("auto_speaker_scored")]
    public int AutoSpeakerScored { get; set; }

    [JsonPropertyName("auto_amp_scored")]
    public int AutoAmpScored { get; set; }

    [JsonPropertyName("teleop_speaker_scored")]
    public int TeleopSpeakerScored { get; set; }

    [JsonPropertyName("teleop_amp_scored")]
    public int TeleopAmpScored { get; set; }

    [JsonPropertyName("endgame_climb")]
    public string EndgameClimb { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class ScoutingSubmission
{
    [JsonPropertyName("team_id")]
    public int TeamId { get; set; }

    [JsonPropertyName("match_id")]
    public int MatchId { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object?> Data { get; set; } = new();

    [JsonPropertyName("offline_id")]
    public string OfflineId { get; set; } = Guid.NewGuid().ToString();
}

public class ScoutingSubmitResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("scouting_id")]
    public int ScoutingId { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("offline_id")]
    public string OfflineId { get; set; } = string.Empty;
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }
}

public class ScoutingEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("team_id")]
    public int TeamId { get; set; }

    [JsonPropertyName("team_number")]
    public int TeamNumber { get; set; }

    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;

    [JsonPropertyName("match_id")]
    public int MatchId { get; set; }

    [JsonPropertyName("match_number")]
    public int MatchNumber { get; set; }

    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = string.Empty;

    [JsonPropertyName("event_id")]
    public int EventId { get; set; }

    [JsonPropertyName("event_code")]
    public string EventCode { get; set; } = string.Empty;

    [JsonPropertyName("alliance")]
    public string Alliance { get; set; } = string.Empty;

    [JsonPropertyName("scout_name")]
    public string ScoutName { get; set; } = string.Empty;

    [JsonPropertyName("scout_id")]
    public int ScoutId { get; set; }

    [JsonPropertyName("scouting_station")]
    public string? ScoutingStation { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("scouting_team_number")]
    public int ScoutingTeamNumber { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();
}

public class ScoutingListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("entries")]
    public List<ScoutingEntry> Entries { get; set; } = new();
}
