using System.Text.Json.Serialization;

namespace ObsidianScout.Models;

public class GameConfig
{
    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("game_name")]
    public string GameName { get; set; } = string.Empty;

    [JsonPropertyName("alliance_size")]
    public int AllianceSize { get; set; }

    [JsonPropertyName("match_types")]
    public List<string> MatchTypes { get; set; } = new();

    [JsonPropertyName("current_event_code")]
    public string CurrentEventCode { get; set; } = string.Empty;

    [JsonPropertyName("auto_period")]
    public GamePeriod? AutoPeriod { get; set; }

    [JsonPropertyName("teleop_period")]
    public GamePeriod? TeleopPeriod { get; set; }

    [JsonPropertyName("endgame_period")]
    public GamePeriod? EndgamePeriod { get; set; }

    [JsonPropertyName("post_match")]
    public PostMatch? PostMatch { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

public class GamePeriod
{
    [JsonPropertyName("duration_seconds")]
    public int DurationSeconds { get; set; }

    [JsonPropertyName("scoring_elements")]
    public List<ScoringElement> ScoringElements { get; set; } = new();
}

public class ScoringElement
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("perm_id")]
    public string PermId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public double Points { get; set; }

    [JsonPropertyName("default")]
    public object? Default { get; set; }

    [JsonPropertyName("display_in_predictions")]
    public bool DisplayInPredictions { get; set; }

    [JsonPropertyName("options")]
    public List<ScoringOption>? Options { get; set; }

    [JsonPropertyName("min")]
    public int? Min { get; set; }

    [JsonPropertyName("max")]
    public int? Max { get; set; }
}

public class ScoringOption
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public double Points { get; set; }
}

public class PostMatch
{
    [JsonPropertyName("rating_elements")]
    public List<RatingElement> RatingElements { get; set; } = new();

    [JsonPropertyName("text_elements")]
    public List<TextElement> TextElements { get; set; } = new();
}

public class RatingElement
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    public int Default { get; set; }

    [JsonPropertyName("min")]
    public int Min { get; set; }

    [JsonPropertyName("max")]
    public int Max { get; set; }
}

public class TextElement
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("multiline")]
    public bool Multiline { get; set; }
}

public class GameConfigResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("config")]
    public GameConfig? Config { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
