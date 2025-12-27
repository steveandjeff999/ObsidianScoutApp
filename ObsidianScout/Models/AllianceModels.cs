using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianScout.Models;

public class AlliancesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("my_alliances")]
    public List<Alliance>? MyAlliances { get; set; }

    [JsonPropertyName("pending_invitations")]
    public List<PendingInvitation>? PendingInvitations { get; set; }

    [JsonPropertyName("sent_invitations")]
    public List<SentInvitation>? SentInvitations { get; set; }

    [JsonPropertyName("active_alliance_id")]
    public int? ActiveAllianceId { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class Alliance
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("member_count")] public int MemberCount { get; set; }
    [JsonPropertyName("is_active")] public bool IsActive { get; set; }
    [JsonPropertyName("config_status")] public string? ConfigStatus { get; set; }
    [JsonPropertyName("is_config_complete")] public bool IsConfigComplete { get; set; }
}

public class PendingInvitation
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("alliance_id")] public int AllianceId { get; set; }
    [JsonPropertyName("alliance_name")] public string AllianceName { get; set; } = string.Empty;
    [JsonPropertyName("from_team")] public int FromTeam { get; set; }
}

public class SentInvitation
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("to_team")] public int ToTeam { get; set; }
    [JsonPropertyName("alliance_id")] public int AllianceId { get; set; }
    [JsonPropertyName("alliance_name")] public string AllianceName { get; set; } = string.Empty;
}

public class CreateAllianceRequest
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public class CreateAllianceResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("alliance_id")] public int? AllianceId { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

public class InviteRequest
{
    [JsonPropertyName("team_number")] public int TeamNumber { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class RespondInvitationRequest
{
    [JsonPropertyName("response")] public string? Response { get; set; } // "accept" or "decline"
}

public class ToggleAllianceRequest
{
    [JsonPropertyName("activate")] public bool Activate { get; set; }
    [JsonPropertyName("remove_shared_data")] public bool? RemoveSharedData { get; set; }
}

public class ToggleAllianceResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("is_active")] public bool IsActive { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}
