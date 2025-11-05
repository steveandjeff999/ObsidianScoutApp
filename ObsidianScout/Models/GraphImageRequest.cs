using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ObsidianScout.Models;

public class GraphImageRequest
{
 [JsonPropertyName("team_numbers")]
 public List<int> TeamNumbers { get; set; } = new();

 [JsonPropertyName("event_id")]
 public int? EventId { get; set; }

 [JsonPropertyName("metric")]
 public string? Metric { get; set; }

 [JsonPropertyName("graph_type")]
 public string? GraphType { get; set; }

 [JsonPropertyName("graph_types")]
 public List<string>? GraphTypes { get; set; }

 [JsonPropertyName("mode")]
 public string? Mode { get; set; }

 [JsonPropertyName("data_view")]
 public string? DataView { get; set; }

 [JsonPropertyName("weather")]
 public Dictionary<string, string>? Weather { get; set; }
}
