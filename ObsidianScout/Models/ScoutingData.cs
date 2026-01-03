using System.Text.Json;
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
    [JsonConverter(typeof(SafeNullableIntJsonConverter))]
    public int? ScoutId { get; set; }

    [JsonPropertyName("scouting_station")]
    public string? ScoutingStation { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("offline_id")]
    public string OfflineId { get; set; } = string.Empty;

    [JsonPropertyName("scouting_team_number")]
    public int ScoutingTeamNumber { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    // Non-serialized preview of the data for UI display
    [JsonIgnore]
    public string Preview
    {
        get
        {
            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = false };
                return JsonSerializer.Serialize(Data, opts);
            }
            catch { return string.Empty; }
        }
    }

    // Short human readable summary (first 3 fields)
    [JsonIgnore]
    public string Summary
    {
        get
        {
            try
            {
                if (Data == null || Data.Count == 0) return string.Empty;
                var parts = Data.Take(3).Select(kv => $"{kv.Key}: {FormatValue(kv.Value)}");
                return string.Join(" — ", parts);
            }
            catch { return string.Empty; }
        }
    }

    private static string FormatValue(object? v)
    {
        if (v == null) return "(null)";
        if (v is JsonElement je)
        {
            try
            {
                if (je.ValueKind == JsonValueKind.String) return je.GetString() ?? string.Empty;
                return je.ToString() ?? string.Empty;
            }
            catch { return je.ToString() ?? string.Empty; }
        }
        return v.ToString() ?? string.Empty;
    }

    // Indicates whether this entry has local unsaved changes compared to server
    [JsonIgnore]
    public bool HasLocalChanges { get; set; } = false;

    // Upload in progress flag for UI
    [JsonIgnore]
    public bool UploadInProgress { get; set; } = false;

    // Indicates whether this entry has been uploaded to server
    [JsonIgnore]
    public bool IsUploaded => Id > 0;

    // Indicates whether this entry exists only locally (pending upload)
    [JsonIgnore]
    public bool IsPending => !string.IsNullOrEmpty(OfflineId) && Id == 0;

    // Whether Upload button should be shown (only when not uploaded and not pending)
    [JsonIgnore]
    public bool CanUpload => !IsUploaded && !IsPending; // No change made

    [JsonIgnore]
    public bool CanEdit => !IsUploaded && !IsPending;

    // Human-friendly status text for UI
    [JsonIgnore]
    public string UploadStatus
    {
        get
        {
            if (UploadInProgress) return "Uploading...";
            if (HasLocalChanges) return "Modified (not uploaded)";
            if (IsUploaded) return "Uploaded";
            if (IsPending) return "Pending";
            return "Not uploaded";
        }
    }
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
