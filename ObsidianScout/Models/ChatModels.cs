using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace ObsidianScout.Models;

public class ChatMessage : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;

 private string _id = string.Empty;
 public string Id
 {
 get => _id;
 set { if (_id != value) { _id = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id))); } }
 }

 private string _sender = string.Empty;
 public string Sender
 {
 get => _sender;
 set { if (_sender != value) { _sender = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Sender))); } }
 }

 private string? _recipient;
 public string? Recipient
 {
 get => _recipient;
 set { if (_recipient != value) { _recipient = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Recipient))); } }
 }

 private string? _group;
 public string? Group
 {
 get => _group;
 set { if (_group != value) { _group = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Group))); } }
 }

 private int? _team;
 public int? Team
 {
 get => _team;
 set { if (_team != value) { _team = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Team))); } }
 }

 private string _text = string.Empty;
 public string Text
 {
 get => _text;
 set { if (_text != value) { _text = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text))); } }
 }

 private DateTime _timestamp;
 public DateTime Timestamp
 {
 get => _timestamp;
 set { if (_timestamp != value) { _timestamp = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Timestamp))); } }
 }

 private bool? _isUnread;
 public bool? IsUnread
 {
 get => _isUnread;
 set { if (_isUnread != value) { _isUnread = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUnread))); } }
 }

 // New: edited metadata and reactions summary to support edit/delete/react features
 private bool _edited;
 public bool Edited
 {
 get => _edited;
 set { if (_edited != value) { _edited = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Edited))); } }
 }

 private DateTime? _editedTimestamp;
 public DateTime? EditedTimestamp
 {
 get => _editedTimestamp;
 set { if (_editedTimestamp != value) { _editedTimestamp = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditedTimestamp))); } }
 }

 private ObservableCollection<ReactionSummary>? _reactions;
 public ObservableCollection<ReactionSummary>? Reactions
 {
 get => _reactions;
 set
 {
 if (_reactions != value)
 {
 _reactions = value;
 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reactions)));
 // Also notify ReactionPreview so UI binding updates
 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionPreview)));
 }
 }
 }

 public string ReactionPreview => Reactions != null && Reactions.Count >0
 ? string.Join(" ", Reactions.Select(r => r.DisplayEmoji))
 : string.Empty;

 // New: flag set by viewmodel indicating this message was sent by the current user
 private bool _isFromCurrentUser;
 public bool IsFromCurrentUser
 {
 get => _isFromCurrentUser;
 set { if (_isFromCurrentUser != value) { _isFromCurrentUser = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFromCurrentUser))); } }
 }
}

public class ChatMessagesResponse
{
 public bool Success { get; set; }
 public string? Type { get; set; }
 public string? Group { get; set; }
 public List<ChatMessage>? Messages { get; set; }
 public int Count { get; set; }
 public int UnreadCount { get; set; }
}

// New: models for sending chat messages
public class ChatSendRequest
{
 // For direct message use recipient_id; for alliance/team/group use conversation_type ("alliance" etc.)
 [JsonPropertyName("recipient_id")]
 public int? RecipientId { get; set; }

 [JsonPropertyName("conversation_type")]
 public string? ConversationType { get; set; }

 [JsonPropertyName("group")]
 public string? Group { get; set; }

 [JsonPropertyName("body")]
 public string Body { get; set; } = string.Empty;

 // offline_id is optional and should be omitted when null
 [JsonPropertyName("offline_id")]
 public string? OfflineId { get; set; }
}

public class ChatSendResponse
{
 public bool Success { get; set; }
 public ChatMessage? Message { get; set; }
 public string? Error { get; set; }
 public string? ErrorCode { get; set; }
}

// New: model for chat members
public class ChatMember
{
 public int Id { get; set; }
 public string Username { get; set; } = string.Empty;
 public string DisplayName { get; set; } = string.Empty;
 public int TeamNumber { get; set; }
}

public class ChatMembersResponse
{
 public bool Success { get; set; }
 public List<ChatMember>? Members { get; set; }
 public string? Error { get; set; }
}

// New: edit/delete/react request/response models
public class ChatEditRequest
{
 [JsonPropertyName("message_id")]
 public string MessageId { get; set; } = string.Empty;

 [JsonPropertyName("text")]
 public string Text { get; set; } = string.Empty;
}

public class ChatEditResponse
{
 public bool Success { get; set; }
 public string? Error { get; set; }
}

public class ChatDeleteRequest
{
 [JsonPropertyName("message_id")]
 public string MessageId { get; set; } = string.Empty;
}

public class ChatDeleteResponse
{
 public bool Success { get; set; }
 public string? Error { get; set; }
}

public class ChatReactRequest
{
 [JsonPropertyName("message_id")]
 public string MessageId { get; set; } = string.Empty;

 [JsonPropertyName("emoji")]
 public string Emoji { get; set; } = string.Empty;
}

public class ReactionSummary
{
 public string Emoji { get; set; } = string.Empty;
 public int Count { get; set; }

 [JsonIgnore]
 public string DisplayEmoji
 {
 get
 {
 if (string.IsNullOrWhiteSpace(Emoji)) return string.Empty;
 var e = Emoji.Trim();
 // If already an emoji character, return it
 if (e.Any(c => char.IsSurrogate(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherSymbol))
 return e;

 // Strip surrounding colons like :thumbsup:
 if (e.StartsWith(':') && e.EndsWith(':'))
 e = e.Substring(1, e.Length -2);

 // common mappings
 return e.ToLowerInvariant() switch
 {
 "thumbsup" or "thumbs_up" or "+1" or "like" => "??",
 "heart" or "love" or "?" => "??",
 "laugh" or "lol" or "joy" => "??",
 "party" or "tada" or "celebrate" => "??",
 "surprised" or "wow" => "??",
 "sad" or "cry" => "??",
 "clap" or "applause" => "??",
 _ => Emoji // fallback to whatever server sent
 };
 }
 }
}

public class ChatReactResponse
{
 public bool Success { get; set; }
 public List<ReactionSummary>? Reactions { get; set; }
 public string? Error { get; set; }
}
