using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace ObsidianScout.Models;

public class ChatMessage : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;

 private string _id = string.Empty;
 // Keep Id as string for internal use
 [JsonIgnore]
 public string Id
 {
 get => _id;
 set { if (_id != value) { _id = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id))); } }
 }

 // Accept id as number or string from server and map into Id
 [JsonPropertyName("id")]
 public JsonElement IdRaw
 {
 set
 {
 try
 {
 if (value.ValueKind == JsonValueKind.Number)
 {
 Id = value.GetRawText();
 }
 else if (value.ValueKind == JsonValueKind.String)
 {
 Id = value.GetString() ?? string.Empty;
 }
 else
 {
 Id = value.GetRawText();
 }
 }
 catch
 {
 Id = value.GetRawText();
 }
 }
 }

 private string _sender = string.Empty;
 // Server may send sender as username or omit and provide sender_id
 [JsonPropertyName("sender")]
 public string Sender
 {
 get => _sender;
 set { if (_sender != value) { _sender = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Sender))); } }
 }

 // Accept numeric sender id when provided and map to Sender as string
 [JsonPropertyName("sender_id")]
 public int? SenderId
 {
 set
 {
 if (value.HasValue)
 {
 Sender = value.Value.ToString();
 }
 }
 }

 private string? _recipient;
 // Recipient may be username or id depending on server; keep string to display
 [JsonPropertyName("recipient")]
 public string? Recipient
 {
 get => _recipient;
 set { if (_recipient != value) { _recipient = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Recipient))); } }
 }

 // Map numeric recipient id if server returns recipient_id
 [JsonPropertyName("recipient_id")]
 public int? RecipientId
 {
 set
 {
 if (value.HasValue)
 {
 Recipient = value.Value.ToString();
 }
 }
 }

 private string? _group;
 [JsonPropertyName("group")]
 public string? Group
 {
 get => _group;
 set { if (_group != value) { _group = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Group))); } }
 }

 private int? _team;
 [JsonPropertyName("team")]
 public int? Team
 {
 get => _team;
 set { if (_team != value) { _team = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Team))); } }
 }

 private string _text = string.Empty;
 // Primary text property used by UI
 [JsonIgnore]
 public string Text
 {
 get => _text;
 set { if (_text != value) { _text = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text))); } }
 }

 // Map server 'body' field to Text
 [JsonPropertyName("body")]
 public string Body
 {
 get => Text;
 set => Text = value ?? string.Empty;
 }

 // Map server 'text' field if used
 [JsonPropertyName("text")]
 public string TextAlias
 {
 set => Text = value ?? string.Empty;
 }

 private DateTime _timestamp;
 // Keep default mapping via CreatedAt if server sends created_at
 [JsonIgnore]
 public DateTime Timestamp
 {
 get => _timestamp;
 set { if (_timestamp != value) { _timestamp = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Timestamp))); } }
 }

 [JsonPropertyName("created_at")]
 public DateTime CreatedAt
 {
 get => Timestamp;
 set => Timestamp = value;
 }

 [JsonPropertyName("timestamp")]
 public DateTime TimestampAlias
 {
 set => Timestamp = value;
 }

 private bool? _isUnread;
 [JsonPropertyName("is_unread")]
 public bool? IsUnread
 {
 get => _isUnread;
 set { if (_isUnread != value) { _isUnread = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUnread))); } }
 }

 // New: edited metadata and reactions summary to support edit/delete/react features
 private bool _edited;
 [JsonPropertyName("edited")]
 public bool Edited
 {
 get => _edited;
 set { if (_edited != value) { _edited = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Edited))); } }
 }

 private DateTime? _editedTimestamp;
 [JsonPropertyName("edited_timestamp")]
 public DateTime? EditedTimestamp
 {
 get => _editedTimestamp;
 set { if (_editedTimestamp != value) { _editedTimestamp = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditedTimestamp))); } }
 }

 private ObservableCollection<ReactionSummary>? _reactions;
 [JsonPropertyName("reactions_summary")]
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

 // Also accept 'reactions' if server uses that name
 [JsonPropertyName("reactions")]
 public List<ReactionSummary>? ReactionsAlias
 {
 set
 {
 if (value != null)
 {
 Reactions = new ObservableCollection<ReactionSummary>(value);
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

// New: models for chat groups (updated to match mobile API docs)
public class ChatGroup
{
 // Keep Id for backward compatibility if server provides numeric ids; otherwise0
 public int Id { get; set; }

 // API returns 'name' for group name
 [JsonPropertyName("name")]
 public string Name { get; set; } = string.Empty;

 // Backwards-compatible Title property used by UI bindings
 public string Title
 {
 get => !string.IsNullOrWhiteSpace(Name) ? Name : _title;
 set => _title = value;
 }
 private string _title = string.Empty;

 // Member usernames when returned by API
 [JsonPropertyName("members")]
 public List<string>? MembersUsernames { get; set; }

 // Indicate whether the authenticated user is a member of this group (API: is_member)
 [JsonPropertyName("is_member")]
 public bool IsMember { get; set; }

 // Optional other metadata
 public List<int>? MemberIds { get; set; }
 public DateTime? CreatedAt { get; set; }
 public int? TeamNumber { get; set; }
}

public class ChatGroupsResponse
{
 public bool Success { get; set; }
 public List<ChatGroup>? Groups { get; set; }
 public string? Error { get; set; }
 }
 
 // Request model for adding/removing members from a group
 public class GroupMembersRequest
 {
 [JsonPropertyName("members")]
 public List<string> Members { get; set; } = new List<string>();
 }
 
 public class ChatGroupMembersResponse
 {
 public bool Success { get; set; }
 public List<string>? Members { get; set; }
 public string? Error { get; set; }
 }

// Request model for creating groups: uses 'group' and 'members' (usernames) per API docs
public class ChatCreateGroupRequest
{
 [JsonPropertyName("group")]
 public string Group { get; set; } = string.Empty;

 [JsonPropertyName("members")]
 public List<string>? Members { get; set; }
}

public class ChatCreateGroupResponse
{
 public bool Success { get; set; }
 public ChatGroup? Group { get; set; }
 public string? Error { get; set; }
}
