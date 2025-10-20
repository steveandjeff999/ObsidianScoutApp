using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObsidianScout.Models;

public class TeamMetrics
{
    [JsonPropertyName("match_count")]
    public int MatchCount { get; set; }

    [JsonPropertyName("total_points")]
    public double TotalPoints { get; set; }

    [JsonPropertyName("total_points_std")]
    public double TotalPointsStd { get; set; }

    [JsonPropertyName("auto_points")]
    public double AutoPoints { get; set; }

    [JsonPropertyName("auto_points_std")]
    public double AutoPointsStd { get; set; }

    [JsonPropertyName("teleop_points")]
    public double TeleopPoints { get; set; }

    [JsonPropertyName("teleop_points_std")]
    public double TeleopPointsStd { get; set; }

    [JsonPropertyName("endgame_points")]
    public double EndgamePoints { get; set; }

    [JsonPropertyName("endgame_points_std")]
    public double EndgamePointsStd { get; set; }

    [JsonPropertyName("consistency")]
    public double Consistency { get; set; }

    [JsonPropertyName("win_rate")]
    public double WinRate { get; set; }
}

public class TeamMetricsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("team")]
    public Team? Team { get; set; }

    [JsonPropertyName("event")]
    public Event? Event { get; set; }

    [JsonPropertyName("metrics")]
    public TeamMetrics? Metrics { get; set; }

    [JsonPropertyName("match_history")]
    public List<MatchHistoryEntry>? MatchHistory { get; set; }
}

public class MatchHistoryEntry
{
    [JsonPropertyName("match_id")]
    public int MatchId { get; set; }

    [JsonPropertyName("match_number")]
    public int MatchNumber { get; set; }

    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = string.Empty;

    [JsonPropertyName("alliance")]
    public string Alliance { get; set; } = string.Empty;

    [JsonPropertyName("total_points")]
    public double TotalPoints { get; set; }

    [JsonPropertyName("auto_points")]
    public double AutoPoints { get; set; }

    [JsonPropertyName("teleop_points")]
    public double TeleopPoints { get; set; }

    [JsonPropertyName("endgame_points")]
    public double EndgamePoints { get; set; }

    [JsonPropertyName("won")]
    public bool Won { get; set; }
}

public class GraphDataset
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<double> Data { get; set; } = new();

    [JsonPropertyName("borderColor")]
    [JsonConverter(typeof(StringOrArrayConverter))]
    public object? BorderColor { get; set; }

    [JsonPropertyName("backgroundColor")]
    [JsonConverter(typeof(StringOrArrayConverter))]
    public object? BackgroundColor { get; set; }
    
    [JsonPropertyName("tension")]
    public double? Tension { get; set; }
}

/// <summary>
/// Custom JSON converter that handles properties that can be either a string or an array of strings
/// </summary>
public class StringOrArrayConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Check the token type
        if (reader.TokenType == JsonTokenType.String)
        {
            // Single string value
            return reader.GetString();
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            // Array of strings
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }
                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (value != null)
                    {
                        list.Add(value);
                    }
                }
            }
            return list;
        }
        
        return null;
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else if (value is string str)
        {
            writer.WriteStringValue(str);
        }
        else if (value is List<string> list)
        {
            writer.WriteStartArray();
            foreach (var item in list)
            {
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
        }
        else if (value is IEnumerable<string> enumerable)
        {
            writer.WriteStartArray();
            foreach (var item in enumerable)
            {
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}

public class GraphData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = new();

    [JsonPropertyName("datasets")]
    public List<GraphDataset> Datasets { get; set; } = new();
}

public class CompareTeamsRequest
{
    [JsonPropertyName("team_numbers")]
    public List<int> TeamNumbers { get; set; } = new();

    [JsonPropertyName("event_id")]
    public int EventId { get; set; }

    [JsonPropertyName("metric")]
    public string Metric { get; set; } = "total_points";

    [JsonPropertyName("graph_types")]
    public List<string> GraphTypes { get; set; } = new() { "line", "bar" };

    [JsonPropertyName("data_view")]
    public string DataView { get; set; } = "averages";
}

public class TeamComparisonData
{
    [JsonPropertyName("team_number")]
    public int TeamNumber { get; set; }

    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("std_dev")]
    public double StdDev { get; set; }

    [JsonPropertyName("match_count")]
    public int MatchCount { get; set; }
}

public class CompareTeamsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("event")]
    public Event? Event { get; set; }

    [JsonPropertyName("metric")]
    public string Metric { get; set; } = string.Empty;

    [JsonPropertyName("metric_display_name")]
    public string MetricDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("data_view")]
    public string DataView { get; set; } = string.Empty;

    [JsonPropertyName("teams")]
    public List<TeamComparisonData> Teams { get; set; } = new();

    [JsonPropertyName("graphs")]
    public Dictionary<string, GraphData> Graphs { get; set; } = new();
}

public class MetricDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("higher_is_better")]
    public bool HigherIsBetter { get; set; }
}

public class MetricsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("metrics")]
    public List<MetricDefinition> Metrics { get; set; } = new();
}
