using ObsidianScout.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ObsidianScout.Services;

public interface IApiService
{
    Task<LoginResponse> LoginAsync(string username, string password, int teamNumber);
    Task<LoginResponse> RegisterAsync(string username, string password, string? confirmPassword, int teamNumber, string? email);
    Task<TokenResponse> RefreshTokenAsync();
    Task<ApiResponse<User>> VerifyTokenAsync();
    
    // User Profile
    Task<UserProfileResponse> GetUserProfileAsync();
    Task<byte[]?> GetProfilePictureAsync();
    
    Task<TeamsResponse> GetTeamsAsync(int? eventId = null, int limit =100, int offset =0);
    Task<EventsResponse> GetEventsAsync();
    Task<MatchesResponse> GetMatchesAsync(int eventId, string? matchType = null, int? teamNumber = null);
    Task<ScoutingSubmitResponse> SubmitScoutingDataAsync(ScoutingSubmission submission);
    Task<GameConfigResponse> GetGameConfigAsync();
    Task<GameConfigResponse> GetTeamGameConfigAsync();
    Task<ApiResponse<string>> HealthCheckAsync();
    Task<TeamMetricsResponse> GetTeamMetricsAsync(int teamId, int eventId);
    Task<CompareTeamsResponse> CompareTeamsAsync(CompareTeamsRequest request);
    Task<MetricsResponse> GetAvailableMetricsAsync();
    Task<ScoutingListResponse> GetAllScoutingDataAsync(int? teamNumber = null, int? eventId = null, int? matchId = null, int limit =200, int offset =0);
    Task<byte[]?> GetGraphsImageAsync(GraphImageRequest request);
    Task<ScheduledNotificationsResponse> GetScheduledNotificationsAsync(int limit =200, int offset =0);
    Task<PastNotificationsResponse> GetPastNotificationsAsync(int limit =200, int offset =0);
    Task<ChatMessagesResponse> GetChatMessagesAsync(string type = "dm", string? user = null, string? group = null, int? allianceId = null, int limit =50, int offset =0);
    Task<ChatSendResponse> SendChatAsync(ChatSendRequest request);
    Task<ChatMembersResponse> GetChatMembersAsync(string scope = "team");
    Task<ChatMembersResponse> GetChatMembersForTeamAsync(int teamNumber);
    Task<ChatEditResponse> EditChatMessageAsync(ChatEditRequest request);
    Task<ChatDeleteResponse> DeleteChatMessageAsync(ChatDeleteRequest request);
    Task<ChatReactResponse> ReactToChatMessageAsync(ChatReactRequest request);
    Task<ChatGroupsResponse> GetChatGroupsAsync(int? teamNumber = null);
    Task<ChatCreateGroupResponse> CreateChatGroupAsync(ChatCreateGroupRequest request);
    
    // Group member management
    Task<ChatGroupMembersResponse> GetChatGroupMembersAsync(string group);
    Task<ChatGroupMembersResponse> AddChatGroupMembersAsync(string group, GroupMembersRequest request);
    Task<ChatGroupMembersResponse> RemoveChatGroupMembersAsync(string group, GroupMembersRequest request);
    
    // Chat state for unread tracking
    Task<ChatStateResponse> GetChatStateAsync();
    
    // Mark messages as read
    Task<ApiResponse<bool>> MarkChatMessagesAsReadAsync(string conversationId, string lastReadMessageId);
    Task<ApiResponse<bool>> SaveGameConfigAsync(GameConfig config);
    
    // Pit Scouting
    Task<PitConfigResponse> GetPitConfigAsync();
    Task<PitConfigResponse> GetTeamPitConfigAsync();
    Task<PitScoutingSubmitResponse> SubmitPitScoutingDataAsync(PitScoutingSubmission submission);
    Task<PitScoutingListResponse> GetPitScoutingDataAsync(int? teamNumber = null);
    Task<PitScoutingEntry?> GetPitScoutingEntryAsync(int entryId);
    Task<PitScoutingSubmitResponse> UpdatePitScoutingDataAsync(int entryId, PitScoutingSubmission submission);
    Task<ApiResponse<bool>> DeletePitScoutingEntryAsync(int entryId);
    Task<ApiResponse<bool>> SavePitConfigAsync(PitConfig config);

    // Admin user management endpoints
    Task<RolesResponse> GetAdminRolesAsync();
    Task<UsersListResponse> GetAdminUsersAsync(string? search = null, int limit =200, int offset =0);
    Task<CreateUserResponse> CreateAdminUserAsync(CreateUserRequest request);
    Task<UserDetailResponse> GetAdminUserAsync(int userId);
    Task<UserDetailResponse> UpdateAdminUserAsync(int userId, UpdateUserRequest request);
    Task<ApiResponse<bool>> DeleteAdminUserAsync(int userId);
    
    // Network configuration
    Task UpdateHttpClientTimeoutAsync();
}