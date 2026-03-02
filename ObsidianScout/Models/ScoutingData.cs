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
    [JsonConverter(typeof(SafeIntJsonConverter))]
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
    [JsonConverter(typeof(SafeIntJsonConverter))]
    public int TeamNumber { get; set; }

    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;

    [JsonPropertyName("match_id")]
    [JsonConverter(typeof(SafeIntJsonConverter))]
    public int MatchId { get; set; }

    [JsonPropertyName("match_number")]
    [JsonConverter(typeof(SafeIntJsonConverter))]
    public int MatchNumber { get; set; }

    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = string.Empty;

    [JsonPropertyName("event_id")]
    [JsonConverter(typeof(SafeIntJsonConverter))]
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
    [JsonConverter(typeof(SafeIntJsonConverter))]
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

    // Whether this entry contains qualitative scouting data
    [JsonIgnore]
    public bool IsQualitative
    {
        get
        {
            try
            {
                if (Data == null || !Data.ContainsKey("qualitative")) return false;
                var val = Data["qualitative"];
                if (val is bool b) return b;
                if (val is JsonElement je) return je.ValueKind == JsonValueKind.True;
                return val?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch { return false; }
        }
    }

    // Human-friendly qualitative summary for History display
    [JsonIgnore]
    public string QualitativeSummary
    {
        get
        {
            try
            {
                if (Data == null || Data.Count == 0) return string.Empty;

                var parts = new List<string>();

                // Overall rating
                var overallRating = GetQualIntValue("qual_overall_rating") ?? GetQualIntValue("overall_rating");
                if (overallRating.HasValue)
                    parts.Add($"? {overallRating.Value}/5");

                // Robot rating
                var robotRating = GetQualIntValue("qual_robot_rating") ?? GetQualIntValue("robot_rating");
                if (robotRating.HasValue)
                    parts.Add($"Robot: {robotRating.Value}/5");

                // Ranking
                var ranking = GetQualIntValue("qual_ranking") ?? GetQualIntValue("ranking");
                if (ranking.HasValue)
                    parts.Add($"Rank: {ranking.Value}");

                // Roles
                var roles = new List<string>();
                if (GetQualBoolValue("qual_cycling") || GetQualBoolValue("cycling")) roles.Add("Cycler");
                if (GetQualBoolValue("qual_scoring") || GetQualBoolValue("scoring")) roles.Add("Scorer");
                if (GetQualBoolValue("qual_feeding") || GetQualBoolValue("feeding")) roles.Add("Feeder");
                if (GetQualBoolValue("qual_defending") || GetQualBoolValue("defending")) roles.Add("Defender");
                if (GetQualBoolValue("qual_stealing") || GetQualBoolValue("stealing")) roles.Add("Stealer");
                if (roles.Count > 0)
                    parts.Add(string.Join(", ", roles));

                // Driver rating
                var driverRating = GetQualIntValue("qual_driver_rating") ?? GetQualIntValue("driver_rating");
                if (driverRating.HasValue)
                    parts.Add($"Driver: {driverRating.Value}/5");

                // Notes
                var notes = GetQualStringValue("qual_notes") ?? GetQualStringValue("notes");
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    var trimmed = notes.Length > 40 ? notes[..40] + "…" : notes;
                    parts.Add($"\"{trimmed}\"");
                }

                return parts.Count > 0 ? string.Join(" · ", parts) : "Qualitative data";
            }
            catch { return "Qualitative data"; }
        }
    }

    private int? GetQualIntValue(string key)
    {
        if (Data == null || !Data.ContainsKey(key)) return null;
        var val = Data[key];
        if (val is int i) return i;
        if (val is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out int n)) return n;
            if (je.ValueKind == JsonValueKind.Null) return null;
        }
        if (int.TryParse(val?.ToString(), out int parsed)) return parsed;
        return null;
    }

    private bool GetQualBoolValue(string key)
    {
        if (Data == null || !Data.ContainsKey(key)) return false;
        var val = Data[key];
        if (val is bool b) return b;
        if (val is JsonElement je) return je.ValueKind == JsonValueKind.True;
        return val?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    }

    private string? GetQualStringValue(string key)
    {
        if (Data == null || !Data.ContainsKey(key)) return null;
        var val = Data[key];
        if (val is string s) return s;
        if (val is JsonElement je && je.ValueKind == JsonValueKind.String) return je.GetString();
        return val?.ToString();
    }

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
