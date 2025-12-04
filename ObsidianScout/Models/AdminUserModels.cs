using System.Collections.Generic;
using System.Text.Json.Serialization;
using System;

namespace ObsidianScout.Models;

public class RolesResponse
{
 public bool Success { get; set; }
 public List<Role>? Roles { get; set; }
 public string? Error { get; set; }
}

public class Role
{
 public int Id { get; set; }
 public string Name { get; set; } = string.Empty;
 public string? Description { get; set; }
}

public class UsersListResponse
{
 public bool Success { get; set; }
 public List<User>? Users { get; set; }
 public string? Error { get; set; }
}

public class CreateUserRequest
{
 [JsonPropertyName("username")]
 public string? Username { get; set; }
 [JsonPropertyName("email")]
 public string? Email { get; set; }
 [JsonPropertyName("password")]
 public string? Password { get; set; }
 [JsonPropertyName("scouting_team_number")]
 public int? ScoutingTeamNumber { get; set; }
 [JsonPropertyName("roles")]
 public List<string>? Roles { get; set; }
}

public class CreateUserResponse
{
 public bool Success { get; set; }
 public User? User { get; set; }
 public string? Error { get; set; }
}

public class UserDetailResponse
{
 public bool Success { get; set; }
 public User? User { get; set; }
 public string? Error { get; set; }
}

public class UpdateUserRequest
{
 [JsonPropertyName("username")]
 public string? Username { get; set; }
 [JsonPropertyName("email")]
 public string? Email { get; set; }
 [JsonPropertyName("scouting_team_number")]
 public int? ScoutingTeamNumber { get; set; }
 [JsonPropertyName("password")]
 public string? Password { get; set; }
 [JsonPropertyName("is_active")]
 public bool? IsActive { get; set; }
 [JsonPropertyName("roles")]
 public List<string>? Roles { get; set; }
}
