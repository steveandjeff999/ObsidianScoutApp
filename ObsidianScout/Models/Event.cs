using System.Text.Json.Serialization;

namespace ObsidianScout.Models;

public class Event
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; set; }
    
    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; set; }
    
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;
    
    [JsonPropertyName("team_count")]
    public int TeamCount { get; set; }

    [JsonPropertyName("is_alliance")]
    public bool IsAlliance { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayName => IsAlliance ? Name + " (alliance)" : Name;
}

public class EventsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("events")]
    public List<Event> Events { get; set; } = new();
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
