using System.Text.Json.Serialization;

namespace ObsidianScout.Models
{
 public class ScheduledNotification
 {
 [JsonPropertyName("id")] public int Id { get; set; }
 [JsonPropertyName("subscription_id")] public int SubscriptionId { get; set; }
 [JsonPropertyName("notification_type")] public string NotificationType { get; set; } = string.Empty;
 [JsonPropertyName("match_id")] public int? MatchId { get; set; }
 [JsonPropertyName("match_number")] public int? MatchNumber { get; set; }
 [JsonPropertyName("event_id")] public int? EventId { get; set; }
 [JsonPropertyName("event_code")] public string? EventCode { get; set; }
 [JsonPropertyName("scheduled_for")] public DateTime ScheduledFor { get; set; }
 [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
 [JsonPropertyName("attempts")] public int Attempts { get; set; }
 [JsonPropertyName("delivery_methods")] public Dictionary<string, bool>? DeliveryMethods { get; set; }
 [JsonPropertyName("target_team_number")] public int? TargetTeamNumber { get; set; }
 [JsonPropertyName("minutes_before")] public int? MinutesBefore { get; set; }
 [JsonPropertyName("weather")] public object? Weather { get; set; }
 }

 public class ScheduledNotificationsResponse
 {
 [JsonPropertyName("success")] public bool Success { get; set; }
 [JsonPropertyName("count")] public int Count { get; set; }
 [JsonPropertyName("total")] public int Total { get; set; }
 [JsonPropertyName("notifications")] public List<ScheduledNotification>? Notifications { get; set; }
 [JsonPropertyName("error")] public string? Error { get; set; }
 }
}
