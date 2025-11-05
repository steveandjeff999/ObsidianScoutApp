using System;
using System.Collections.Generic;
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

 // Past notification model
 public class PastNotification
 {
 [JsonPropertyName("id")] public int Id { get; set; }
 [JsonPropertyName("subscription_id")] public int SubscriptionId { get; set; }
 [JsonPropertyName("notification_type")] public string NotificationType { get; set; } = string.Empty;
 [JsonPropertyName("match_id")] public int? MatchId { get; set; }
 [JsonPropertyName("match_number")] public int? MatchNumber { get; set; }
 [JsonPropertyName("event_id")] public int? EventId { get; set; }  // NEW: For deep linking
 [JsonPropertyName("event_code")] public string? EventCode { get; set; }
 [JsonPropertyName("sent_at")] public DateTime SentAt { get; set; }
 [JsonPropertyName("email_sent")] public bool EmailSent { get; set; }
 [JsonPropertyName("push_sent_count")] public int PushSentCount { get; set; }
 [JsonPropertyName("email_error")] public string? EmailError { get; set; }
 [JsonPropertyName("push_error")] public string? PushError { get; set; }
 [JsonPropertyName("title")] public string? Title { get; set; }
 [JsonPropertyName("message")] public string? Message { get; set; }
 [JsonPropertyName("target_team_number")] public int? TargetTeamNumber { get; set; }
 }

 public class PastNotificationsResponse
 {
 [JsonPropertyName("success")] public bool Success { get; set; }
 [JsonPropertyName("count")] public int Count { get; set; }
 [JsonPropertyName("total")] public int Total { get; set; }
 [JsonPropertyName("notifications")] public List<PastNotification>? Notifications { get; set; }
 [JsonPropertyName("error")] public string? Error { get; set; }
 }

 // Local tracking of sent notifications
 public class SentNotificationRecord
 {
 public int NotificationId { get; set; }
 public DateTime SentAt { get; set; }
 public DateTime ScheduledFor { get; set; }
 public string NotificationType { get; set; } = string.Empty;
 public int? MatchNumber { get; set; }
 public string? EventCode { get; set; }
 public bool WasMissed { get; set; } // Indicates if this was sent late (catch-up)
 }

 public class NotificationTrackingData
 {
 [JsonPropertyName("sent_notifications")]
 public List<SentNotificationRecord> SentNotifications { get; set; } = new();
 
 [JsonPropertyName("last_poll_time")]
 public DateTime LastPollTime { get; set; }
 
 [JsonPropertyName("last_cleanup_time")]
 public DateTime LastCleanupTime { get; set; }
  
 // Track last chat notification count to avoid duplicate notifications
 [JsonPropertyName("last_chat_unread_count")]
 public int LastChatUnreadCount { get; set; } = 0;
  
 // NEW: Track which chat messages we've already notified about
 [JsonPropertyName("notified_chat_message_ids")]
 public List<string> NotifiedChatMessageIds { get; set; } = new();
 }

 // Chat state model for unread count tracking
 public class ChatStateResponse
 {
 [JsonPropertyName("success")]
 public bool Success { get; set; }
 
 [JsonPropertyName("state")]
 public ChatState? State { get; set; }
 
 [JsonPropertyName("error")]
 public string? Error { get; set; }
 
 [JsonPropertyName("error_code")]
 public string? ErrorCode { get; set; }
 }

 public class ChatState
 {
 [JsonPropertyName("joinedGroups")]
  public List<string>? JoinedGroups { get; set; }
 
 [JsonPropertyName("currentGroup")]
 public string? CurrentGroup { get; set; }
 
 [JsonPropertyName("lastDmUser")]
 public string? LastDmUser { get; set; }
 
 [JsonPropertyName("unreadCount")]
 public int UnreadCount { get; set; }
 
 [JsonPropertyName("lastSource")]
 public ChatMessageSource? LastSource { get; set; }
 
 [JsonPropertyName("notified")]
 public bool Notified { get; set; }
 
 [JsonPropertyName("lastNotified")]
 public DateTime? LastNotified { get; set; }
  
  // NEW: Server includes actual unread messages in the chat state response
 [JsonPropertyName("unreadMessages")]
  public List<ChatMessage>? UnreadMessages { get; set; }
 }

 public class ChatMessageSource
 {
 [JsonPropertyName("type")]
 public string Type { get; set; } = string.Empty; // "dm", "group", etc.
 
 [JsonPropertyName("id")]
 public string? Id { get; set; } // username for DM, group name for groups
 }
}
