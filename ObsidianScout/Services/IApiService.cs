using ObsidianScout.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ObsidianScout.Services;

public interface IApiService
{
    Task<LoginResponse> LoginAsync(string username, string password, int teamNumber);
    Task<TokenResponse> RefreshTokenAsync();
    Task<ApiResponse<User>> VerifyTokenAsync();
    Task<TeamsResponse> GetTeamsAsync(int? eventId = null, int limit =100, int offset =0);
    Task<EventsResponse> GetEventsAsync();
    Task<MatchesResponse> GetMatchesAsync(int eventId, string? matchType = null, int? teamNumber = null);
    Task<ScoutingSubmitResponse> SubmitScoutingDataAsync(ScoutingSubmission submission);
    Task<GameConfigResponse> GetGameConfigAsync();
    Task<ApiResponse<string>> HealthCheckAsync();
    Task<TeamMetricsResponse> GetTeamMetricsAsync(int teamId, int eventId);
    Task<CompareTeamsResponse> CompareTeamsAsync(CompareTeamsRequest request);
    Task<MetricsResponse> GetAvailableMetricsAsync();
    Task<ScoutingListResponse> GetAllScoutingDataAsync(int? teamNumber = null, int? eventId = null, int? matchId = null, int limit =200, int offset =0);
    Task<byte[]?> GetGraphsImageAsync(GraphImageRequest request);
    Task<ScheduledNotificationsResponse> GetScheduledNotificationsAsync(int limit =200, int offset =0);
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
}