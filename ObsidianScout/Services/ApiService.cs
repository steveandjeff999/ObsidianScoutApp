using ObsidianScout.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObsidianScout.Services;

// Custom DateTime converter to handle various date formats
public class FlexibleDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly string[] DateFormats = new[]
    {
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ss.fffffffZ",
        "MM/dd/yyyy",
        "M/d/yyyy"
    };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString();
        if (string.IsNullOrEmpty(dateString))
            return DateTime.MinValue;

        // Try parsing with each format
        foreach (var format in DateFormats)
        {
            if (DateTime.TryParseExact(dateString, format, 
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        // Fall back to default parsing
        if (DateTime.TryParse(dateString, out var parsedDate))
            return parsedDate;

        // If all else fails, return MinValue
        return DateTime.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss"));
    }
}

public interface IApiService
{
    Task<LoginResponse> LoginAsync(string username, string password, int teamNumber);
    Task<TokenResponse> RefreshTokenAsync();
    Task<ApiResponse<User>> VerifyTokenAsync();
    Task<TeamsResponse> GetTeamsAsync(int? eventId = null, int limit = 100, int offset = 0);
    Task<EventsResponse> GetEventsAsync();
    Task<MatchesResponse> GetMatchesAsync(int eventId, string? matchType = null, int? teamNumber = null);
    Task<ScoutingSubmitResponse> SubmitScoutingDataAsync(ScoutingSubmission submission);
    Task<GameConfigResponse> GetGameConfigAsync();
    Task<ApiResponse<string>> HealthCheckAsync();
    Task<TeamMetricsResponse> GetTeamMetricsAsync(int teamId, int eventId);
    Task<CompareTeamsResponse> CompareTeamsAsync(CompareTeamsRequest request);
    Task<MetricsResponse> GetAvailableMetricsAsync();
    Task<ScoutingListResponse> GetAllScoutingDataAsync(int? teamNumber = null, int? eventId = null, int? matchId = null, int limit = 200, int offset = 0);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly ICacheService _cacheService;
    private readonly IConnectivityService _connectivityService;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(HttpClient httpClient, ISettingsService settingsService, ICacheService cacheService, IConnectivityService connectivityService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _cacheService = cacheService;
        _connectivityService = connectivityService;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // Removed PropertyNamingPolicy since we're using explicit JsonPropertyName attributes
            Converters = { new FlexibleDateTimeConverter() }
        };
    }

    private async Task<string> GetBaseUrlAsync()
    {
        var serverUrl = await _settingsService.GetServerUrlAsync();
        return $"{serverUrl.TrimEnd('/')}/api/mobile";
    }

    private async Task AddAuthHeaderAsync()
    {
        var token = await _settingsService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<LoginResponse> LoginAsync(string username, string password, int teamNumber)
    {
        try
        {
            var baseUrl = await GetBaseUrlAsync();
            var response = await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/auth/login",
                new { username, password, team_number = teamNumber },
                _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
                if (result != null && result.Success)
                {
                    await _settingsService.SetTokenAsync(result.Token);
                    await _settingsService.SetTokenExpirationAsync(result.ExpiresAt);
                    
                    // Store the username for auto-filling scout name
                    if (result.User != null && !string.IsNullOrEmpty(result.User.Username))
                    {
                        await _settingsService.SetUsernameAsync(result.User.Username);
                    }
                    
                    // Store user roles
                    if (result.User != null && result.User.Roles != null && result.User.Roles.Count > 0)
                    {
                        await _settingsService.SetUserRolesAsync(result.User.Roles);
                        
                        // DEBUG: Log roles
                        System.Diagnostics.Debug.WriteLine($"LOGIN: Stored {result.User.Roles.Count} roles:");
                        foreach (var role in result.User.Roles)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - '{role}'");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("LOGIN: No roles returned from API or User is null");
                        if (result.User == null)
                        {
                            System.Diagnostics.Debug.WriteLine("  User object is NULL");
                        }
                        else if (result.User.Roles == null)
                        {
                            System.Diagnostics.Debug.WriteLine("  Roles list is NULL");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"  Roles count: {result.User.Roles.Count}");
                        }
                    }
                    
                    return result;
                }
                return result ?? new LoginResponse { Success = false, Error = "Invalid response" };
            }
            else
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
                return errorResponse ?? new LoginResponse 
                { 
                    Success = false, 
                    Error = $"Request failed with status {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            return new LoginResponse
            {
                Success = false,
                Error = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<TokenResponse> RefreshTokenAsync()
    {
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var response = await _httpClient.PostAsync($"{baseUrl}/auth/refresh", null);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TokenResponse>(_jsonOptions);
                if (result != null && result.Success)
                {
                    await _settingsService.SetTokenAsync(result.Token);
                    await _settingsService.SetTokenExpirationAsync(result.ExpiresAt);
                }
                return result ?? new TokenResponse { Success = false };
            }

            return new TokenResponse { Success = false };
        }
        catch
        {
            return new TokenResponse { Success = false };
        }
    }

    public async Task<ApiResponse<User>> VerifyTokenAsync()
    {
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var response = await _httpClient.GetAsync($"{baseUrl}/auth/verify");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<User>>(_jsonOptions);
                return result ?? new ApiResponse<User> { Success = false };
            }

            return new ApiResponse<User> { Success = false };
        }
        catch
        {
            return new ApiResponse<User> { Success = false };
        }
    }

    public async Task<TeamsResponse> GetTeamsAsync(int? eventId = null, int limit = 100, int offset = 0)
    {
        // If we know we're offline, return cached data quickly without attempting network
        if (!_connectivityService.IsConnected)
        {
            var cachedTeams = await _cacheService.GetCachedTeamsAsync();
            if (cachedTeams != null && cachedTeams.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Offline - returning cached teams immediately");
                return new TeamsResponse
                {
                    Success = true,
                    Teams = cachedTeams,
                    Error = "Using cached data (offline mode)"
                };
            }

            return new TeamsResponse { Success = false, Error = "Offline - no cached teams available" };
        }
        
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/teams?limit={limit}&offset={offset}";
            
            if (eventId.HasValue)
                url += $"&event_id={eventId.Value}";

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TeamsResponse>(_jsonOptions);
                
                // Cache the teams data
                if (result != null && result.Success && result.Teams != null && result.Teams.Count > 0)
                {
                    await _cacheService.CacheTeamsAsync(result.Teams);
                }
                
                return result ?? new TeamsResponse { Success = false };
            }

            // Try to load from cache on failure
            var cachedTeams2 = await _cacheService.GetCachedTeamsAsync();
            if (cachedTeams2 != null && cachedTeams2.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached teams (offline mode)");
                return new TeamsResponse 
                { 
                    Success = true, 
                    Teams = cachedTeams2,
                    Error = "Using cached data (offline mode)"
                };
            }

            return new TeamsResponse { Success = false };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Teams request failed: {ex.Message}");
            
            // Try to load from cache on exception
            var cachedTeams3 = await _cacheService.GetCachedTeamsAsync();
            if (cachedTeams3 != null && cachedTeams3.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached teams after error (offline mode)");
                return new TeamsResponse 
                { 
                    Success = true, 
                    Teams = cachedTeams3,
                    Error = "Using cached data (offline mode)"
                };
            }
            
            return new TeamsResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<EventsResponse> GetEventsAsync()
    {
        if (!_connectivityService.IsConnected)
        {
            var cachedEvents = await _cacheService.GetCachedEventsAsync();
            if (cachedEvents != null && cachedEvents.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline - returning cached events immediately");
                return new EventsResponse { Success = true, Events = cachedEvents, Error = "Using cached data (offline mode)" };
            }
            return new EventsResponse { Success = false, Error = "Offline - no cached events available" };
        }
        
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var response = await _httpClient.GetAsync($"{baseUrl}/events");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<EventsResponse>(_jsonOptions);
                
                // Cache the events data
                if (result != null && result.Success && result.Events != null && result.Events.Count > 0)
                {
                    await _cacheService.CacheEventsAsync(result.Events);
                }
                
                return result ?? new EventsResponse { Success = false, Error = "Invalid response format" };
            }

            // Try to load from cache on failure
            var cachedEvents2 = await _cacheService.GetCachedEventsAsync();
            if (cachedEvents2 != null && cachedEvents2.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached events (offline mode)");
                return new EventsResponse 
                { 
                    Success = true, 
                    Events = cachedEvents2,
                    Error = "Using cached data (offline mode)"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new EventsResponse 
            { 
                Success = false, 
                Error = $"HTTP {response.StatusCode}: {errorContent}" 
            };
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Events request failed: {httpEx.Message}");
            
            // Try to load from cache on exception
            var cachedEvents3 = await _cacheService.GetCachedEventsAsync();
            if (cachedEvents3 != null && cachedEvents3.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached events after error (offline mode)");
                return new EventsResponse 
                { 
                    Success = true, 
                    Events = cachedEvents3,
                    Error = "Using cached data (offline mode)"
                };
            }
            
            return new EventsResponse
            {
                Success = false,
                Error = $"Network error: {httpEx.Message}"
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Events exception: {ex.Message}");
            
            // Try to load from cache on exception
            var cachedEvents4 = await _cacheService.GetCachedEventsAsync();
            if (cachedEvents4 != null && cachedEvents4.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached events after exception (offline mode)");
                return new EventsResponse 
                { 
                    Success = true, 
                    Events = cachedEvents4,
                    Error = "Using cached data (offline mode)"
                };
            }
            
            return new EventsResponse
            {
                Success = false,
                Error = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<MatchesResponse> GetMatchesAsync(int eventId, string? matchType = null, int? teamNumber = null)
    {
        if (!_connectivityService.IsConnected)
        {
            var cachedMatches = await _cacheService.GetCachedMatchesAsync(eventId);
            if (cachedMatches != null && cachedMatches.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline - returning cached matches immediately");
                return new MatchesResponse { Success = true, Matches = cachedMatches, Error = "Using cached data (offline mode)" };
            }
            return new MatchesResponse { Success = false, Error = "Offline - no cached matches available" };
        }
        
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/matches?event_id={eventId}";
            
            if (!string.IsNullOrEmpty(matchType))
                url += $"&match_type={matchType}";
            
            if (teamNumber.HasValue)
                url += $"&team_number={teamNumber.Value}";

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<MatchesResponse>(_jsonOptions);
                
                // Cache the matches data
                if (result != null && result.Success && result.Matches != null && result.Matches.Count > 0)
                {
                    await _cacheService.CacheMatchesAsync(result.Matches, eventId);
                }
                
                return result ?? new MatchesResponse { Success = false, Error = "Invalid response format" };
            }

            // Try to load from cache on failure
            var cachedMatches2 = await _cacheService.GetCachedMatchesAsync(eventId);
            if (cachedMatches2 != null && cachedMatches2.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached matches (offline mode)");
                return new MatchesResponse 
                { 
                    Success = true, 
                    Matches = cachedMatches2,
                    Error = "Using cached data (offline mode)"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new MatchesResponse 
            { 
                Success = false, 
                Error = $"HTTP {response.StatusCode}: {errorContent}" 
            };
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Matches request failed: {httpEx.Message}");
            
            // Try to load from cache on exception
            var cachedMatches3 = await _cacheService.GetCachedMatchesAsync(eventId);
            if (cachedMatches3 != null && cachedMatches3.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached matches after error (offline mode)");
                return new MatchesResponse 
                { 
                    Success = true, 
                    Matches = cachedMatches3,
                    Error = "Using cached data (offline mode)"
                };
            }
            
            return new MatchesResponse
            {
                Success = false,
                Error = $"Network error: {httpEx.Message}"
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Matches exception: {ex.Message}");
            
            // Try to load from cache on exception
            var cachedMatches4 = await _cacheService.GetCachedMatchesAsync(eventId);
            if (cachedMatches4 != null && cachedMatches4.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached matches after exception (offline mode)");
                return new MatchesResponse 
                { 
                    Success = true, 
                    Matches = cachedMatches4,
                    Error = "Using cached data (offline mode)"
                };
            }
            
            return new MatchesResponse
            {
                Success = false,
                Error = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<ScoutingSubmitResponse> SubmitScoutingDataAsync(ScoutingSubmission submission)
    {
        // If offline, return quickly indicating offline so UI can queue/handle
        if (!_connectivityService.IsConnected)
        {
            return new ScoutingSubmitResponse
            {
                Success = false,
                Error = "Offline - submission queued locally",
                ErrorCode = "OFFLINE"
            };
        }
        
        var startTime = DateTime.Now;
        
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/scouting/submit";
            
            System.Diagnostics.Debug.WriteLine("=== API: SUBMIT SCOUTING DATA ===");
            System.Diagnostics.Debug.WriteLine($"Timestamp: {startTime:yyyy-MM-dd HH:mm:ss.fff}");
            System.Diagnostics.Debug.WriteLine($"Endpoint: {endpoint}");
            System.Diagnostics.Debug.WriteLine($"Team ID: {submission.TeamId}");
            System.Diagnostics.Debug.WriteLine($"Match ID: {submission.MatchId}");
            System.Diagnostics.Debug.WriteLine($"Data fields: {submission.Data.Count}");
            System.Diagnostics.Debug.WriteLine($"Offline ID: {submission.OfflineId}");
            
            // Log auth header (last 20 chars only for security)
            var authHeader = _httpClient.DefaultRequestHeaders.Authorization?.ToString();
            if (authHeader != null && authHeader.Length > 20)
            {
                System.Diagnostics.Debug.WriteLine($"Auth: {authHeader.Substring(0, 20)}...");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Auth: {authHeader ?? "NONE"}");
            }
            
            // Serialize and log request body preview
            try
            {
                var requestJson = System.Text.Json.JsonSerializer.Serialize(submission, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
                });
                System.Diagnostics.Debug.WriteLine("Request Body:");
                // Limit to first 500 chars to avoid huge logs
                if (requestJson.Length > 500)
                {
                    System.Diagnostics.Debug.WriteLine(requestJson.Substring(0, 500) + "\n... (truncated)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(requestJson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to preview request: {ex.Message}");
            }
            
            System.Diagnostics.Debug.WriteLine("Sending POST request...");
            var response = await _httpClient.PostAsJsonAsync(endpoint, submission, _jsonOptions);
            
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            System.Diagnostics.Debug.WriteLine($"Response received in {elapsed:F0}ms");
            System.Diagnostics.Debug.WriteLine($"Status Code: {(int)response.StatusCode} {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Success: {response.IsSuccessStatusCode}");

            // Log response headers
            System.Diagnostics.Debug.WriteLine("Response Headers:");
            foreach (var header in response.Headers)
            {
                System.Diagnostics.Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Response Body: {responseContent}");
                
                try
                {
                    var result = System.Text.Json.JsonSerializer.Deserialize<ScoutingSubmitResponse>(responseContent, _jsonOptions);
                    
                    if (result != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Parsed Success: {result.Success}");
                        System.Diagnostics.Debug.WriteLine($"Parsed Message: {result.Message}");
                        System.Diagnostics.Debug.WriteLine($"Parsed Error: {result.Error}");
                        System.Diagnostics.Debug.WriteLine($"Parsed Scouting ID: {result.ScoutingId}");
                        return result;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: Deserialization returned null");
                        return new ScoutingSubmitResponse { Success = false, Error = "Invalid response - null result" };
                    }
                }
                catch (JsonException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: JSON deserialization failed: {jsonEx.Message}");
                    return new ScoutingSubmitResponse 
                    { 
                        Success = false, 
                        Error = $"Invalid JSON response: {jsonEx.Message}",
                        ErrorCode = "JSON_PARSE_ERROR"
                    };
                }
            }
            else
            {
                // Read error content
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Error Response Body: {errorContent}");
                
                // Try to parse as ScoutingSubmitResponse
                try
                {
                    if (!string.IsNullOrWhiteSpace(errorContent))
                    {
                        var errorResponse = System.Text.Json.JsonSerializer.Deserialize<ScoutingSubmitResponse>(errorContent, _jsonOptions);
                        if (errorResponse != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Parsed error response: {errorResponse.Error}");
                            return errorResponse;
                        }
                    }
                }
                catch (JsonException)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to parse error response as JSON");
                }
                
                return new ScoutingSubmitResponse 
                { 
                    Success = false, 
                    Error = $"HTTP {(int)response.StatusCode}: {errorContent}",
                    ErrorCode = $"HTTP_{(int)response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"=== HTTP REQUEST EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Message: {httpEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Status: {httpEx.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Stack: {httpEx.StackTrace}");
            
            return new ScoutingSubmitResponse
            {
                Success = false,
                Error = $"Network error: {httpEx.Message}",
                ErrorCode = "NETWORK_ERROR"
            };
        }
        catch (JsonException jsonEx)
        {
            System.Diagnostics.Debug.WriteLine($"=== JSON EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Message: {jsonEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Path: {jsonEx.Path}");
            System.Diagnostics.Debug.WriteLine($"Line: {jsonEx.LineNumber}, Position: {jsonEx.BytePositionInLine}");
            System.Diagnostics.Debug.WriteLine($"Stack: {jsonEx.StackTrace}");
            
            return new ScoutingSubmitResponse
            {
                Success = false,
                Error = $"JSON error: {jsonEx.Message}",
                ErrorCode = "JSON_ERROR"
            };
        }
        catch (TaskCanceledException tcEx)
        {
            System.Diagnostics.Debug.WriteLine($"=== TIMEOUT EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Message: {tcEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Cancelled: {tcEx.CancellationToken.IsCancellationRequested}");
            
            return new ScoutingSubmitResponse
            {
                Success = false,
                Error = "Request timeout - server took too long to respond",
                ErrorCode = "TIMEOUT"
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== GENERAL EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            
            return new ScoutingSubmitResponse
            {
                Success = false,
                Error = $"Error: {ex.Message}",
                ErrorCode = "GENERAL_ERROR"
            };
        }
        finally
        {
            var totalElapsed = (DateTime.Now - startTime).TotalMilliseconds;
            System.Diagnostics.Debug.WriteLine($"=== END API CALL ({totalElapsed:F0}ms) ===\n");
        }
    }

    public async Task<GameConfigResponse> GetGameConfigAsync()
    {
        if (!_connectivityService.IsConnected)
        {
            var cachedConfig = await _cacheService.GetCachedGameConfigAsync();
            if (cachedConfig != null)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline - returning cached game config immediately");
                return new GameConfigResponse { Success = true, Config = cachedConfig, Error = "Using cached data (offline mode)" };
            }
            return new GameConfigResponse { Success = false, Error = "Offline - no cached game config available" };
        }
        
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var response = await _httpClient.GetAsync($"{baseUrl}/config/game");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GameConfigResponse>(_jsonOptions);
                
                // Cache the game config
                if (result != null && result.Success && result.Config != null)
                {
                    await _cacheService.CacheGameConfigAsync(result.Config);
                }
                
                return result ?? new GameConfigResponse { Success = false, Error = "Invalid response" };
            }

            // Try to load from cache on failure
            var cachedConfig2 = await _cacheService.GetCachedGameConfigAsync();
            if (cachedConfig2 != null)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached game config (offline mode)");
                return new GameConfigResponse 
                { 
                    Success = true, 
                    Config = cachedConfig2,
                    Error = "Using cached data (offline mode)"
                };
            }

            return new GameConfigResponse 
            { 
                Success = false, 
                Error = $"Request failed with status {response.StatusCode}" 
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Game config request failed: {ex.Message}");
            
            // Try to load from cache on failure
            var cachedConfig3 = await _cacheService.GetCachedGameConfigAsync();
            if (cachedConfig3 != null)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached game config after error (offline mode)");
                return new GameConfigResponse 
                { 
                    Success = true, 
                    Config = cachedConfig3,
                    Error = "Using cached data (offline mode)"
                };
            }
            
            return new GameConfigResponse
            {
                Success = false,
                Error = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<string>> HealthCheckAsync()
    {
        if (!_connectivityService.IsConnected)
        {
            return new ApiResponse<string> { Success = false, Error = "Offline - cannot perform health check" };
        }
        
        try
        {
            var baseUrl = await GetBaseUrlAsync();
            var response = await _httpClient.GetAsync($"{baseUrl}/health");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<string>>(_jsonOptions);
                return result ?? new ApiResponse<string> { Success = false, Error = "Invalid response" };
            }

            return new ApiResponse<string> 
            { 
                Success = false, 
                Error = $"Health check failed with status {response.StatusCode}" 
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<string>
            {
                Success = false,
                Error = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<TeamMetricsResponse> GetTeamMetricsAsync(int teamId, int eventId)
    {
        if (!_connectivityService.IsConnected)
        {
            return new TeamMetricsResponse { Success = false, Error = "Offline - no cached metrics available" };
        }
        
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var response = await _httpClient.GetAsync($"{baseUrl}/teams/{teamId}/metrics?event_id={eventId}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TeamMetricsResponse>(_jsonOptions);
                return result ?? new TeamMetricsResponse { Success = false, Error = "Invalid response" };
            }

            return new TeamMetricsResponse 
            { 
                Success = false, 
                Error = $"Request failed with status {response.StatusCode}" 
            };
        }
        catch (Exception ex)
        {
            return new TeamMetricsResponse
            {
                Success = false,
                Error = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<CompareTeamsResponse> CompareTeamsAsync(CompareTeamsRequest request)
    {
        if (!_connectivityService.IsConnected)
        {
            return new CompareTeamsResponse { Success = false, Error = "Offline - cannot compare teams" };
        }
        
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/graphs/compare";
            
            System.Diagnostics.Debug.WriteLine("=== API: COMPARE TEAMS ===");
            System.Diagnostics.Debug.WriteLine($"Endpoint: {endpoint}");
            System.Diagnostics.Debug.WriteLine($"Teams: {string.Join(", ", request.TeamNumbers)}");
            System.Diagnostics.Debug.WriteLine($"Event ID: {request.EventId}");
            System.Diagnostics.Debug.WriteLine($"Metric: {request.Metric}");
            
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
            
            System.Diagnostics.Debug.WriteLine($"Response Status: {(int)response.StatusCode} {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CompareTeamsResponse>(_jsonOptions);
                System.Diagnostics.Debug.WriteLine($"Success: Teams compared successfully");
                return result ?? new CompareTeamsResponse { Success = false, Error = "Invalid response" };
            }
            
            // Handle specific HTTP status codes
            var errorMessage = response.StatusCode switch
            {
                System.Net.HttpStatusCode.NotFound => 
                    "Graph comparison endpoint not implemented on server yet.\n\n" +
                    "?? This feature requires the /api/mobile/graphs/compare endpoint.\n" +
                    "Contact your system administrator to implement this endpoint.",
                
                System.Net.HttpStatusCode.Unauthorized => 
                    "Authentication required. Please log in again.",
                
                System.Net.HttpStatusCode.Forbidden => 
                    "You don't have permission to access graph comparison.",
                
                System.Net.HttpStatusCode.BadRequest => 
                    "Invalid request. Check that teams and event are selected.",
                
                _ => $"Request failed with status {response.StatusCode}"
            };
            
            var errorContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Error: {errorMessage}");
            System.Diagnostics.Debug.WriteLine($"Error Content: {errorContent}");

            return new CompareTeamsResponse 
            { 
                Success = false, 
                Error = errorMessage
            };
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP Exception: {httpEx.Message}");
            return new CompareTeamsResponse
            {
                Success = false,
                Error = $"Network error: {httpEx.Message}\n\n?? Check your internet connection and server address."
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
            return new CompareTeamsResponse
            {
                Success = false,
                Error = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<MetricsResponse> GetAvailableMetricsAsync()
    {
        if (!_connectivityService.IsConnected)
        {
            var cachedMetrics = await _cacheService.GetCachedAvailableMetricsAsync();
            if (cachedMetrics != null && cachedMetrics.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline - returning cached metrics immediately");
                return new MetricsResponse { Success = true, Metrics = cachedMetrics, Error = "Using cached data (offline mode)" };
            }
            return new MetricsResponse { Success = false, Error = "Offline - no cached metrics available" };
        }
        
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var response = await _httpClient.GetAsync($"{baseUrl}/config/metrics");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<MetricsResponse>(_jsonOptions);
                
                // Cache the metrics
                if (result != null && result.Success && result.Metrics != null && result.Metrics.Count > 0)
                {
                    await _cacheService.CacheAvailableMetricsAsync(result.Metrics);
                }
                
                return result ?? new MetricsResponse { Success = false, Error = "Invalid response" };
            }

            // Try to load from cache on failure
            var cachedMetrics2 = await _cacheService.GetCachedAvailableMetricsAsync();
            if (cachedMetrics2 != null && cachedMetrics2.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached metrics (offline mode)");
                return new MetricsResponse 
                { 
                    Success = true, 
                    Metrics = cachedMetrics2,
                    Error = "Using cached data (offline mode)"
                };
            }

            return new MetricsResponse 
            { 
                Success = false, 
                Error = $"Request failed with status {response.StatusCode}" 
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Metrics request failed: {ex.Message}");
            
            // Try to load from cache on exception
            var cachedMetrics3 = await _cacheService.GetCachedAvailableMetricsAsync();
            if (cachedMetrics3 != null && cachedMetrics3.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached metrics after error (offline mode)");
                return new MetricsResponse 
                { 
                    Success = true, 
                    Metrics = cachedMetrics3,
                    Error = "Using cached data (offline mode)"
                };
            }
            
            return new MetricsResponse
            {
                Success = false,
                Error = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ScoutingListResponse> GetAllScoutingDataAsync(int? teamNumber = null, int? eventId = null, int? matchId = null, int limit = 200, int offset = 0)
    {
        if (!_connectivityService.IsConnected)
        {
            var cachedData = await _cacheService.GetCachedScoutingDataAsync();
            if (cachedData != null && cachedData.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline - returning cached scouting data immediately");
                return new ScoutingListResponse { Success = true, Entries = cachedData, Error = "Using cached data (offline mode)" };
            }
            return new ScoutingListResponse { Success = false, Error = "Offline - no cached scouting data available" };
        }
        
        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/scouting/all?limit={limit}&offset={offset}";
            
            if (teamNumber.HasValue)
                url += $"&team_number={teamNumber.Value}";
            
            if (eventId.HasValue)
                url += $"&event_id={eventId.Value}";
            
            if (matchId.HasValue)
                url += $"&match_id={matchId.Value}";

            System.Diagnostics.Debug.WriteLine($"=== API: FETCH SCOUTING DATA ===");
            System.Diagnostics.Debug.WriteLine($"URL: {url}");
            
            var response = await _httpClient.GetAsync(url);
            
            System.Diagnostics.Debug.WriteLine($"Response Status: {(int)response.StatusCode} {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ScoutingListResponse>(_jsonOptions);
                
                // Cache the scouting data
                if (result != null && result.Success && result.Entries != null && result.Entries.Count > 0)
                {
                    await _cacheService.CacheScoutingDataAsync(result.Entries);
                }
                
                System.Diagnostics.Debug.WriteLine($"Success: Fetched {result?.Entries.Count ?? 0} scouting entries");
                return result ?? new ScoutingListResponse { Success = false, Error = "Invalid response" };
            }

            // Try to load from cache on failure
            var cachedData2 = await _cacheService.GetCachedScoutingDataAsync();
            if (cachedData2 != null && cachedData2.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached scouting data (offline mode)");
                return new ScoutingListResponse 
                { 
                    Success = true, 
                    Entries = cachedData2,
                    Error = "Using cached data (offline mode)"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Error Content: {errorContent}");

            return new ScoutingListResponse 
            { 
                Success = false, 
                Error = $"Request failed with status {response.StatusCode}" 
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Scouting data request failed: {ex.Message}");
            
            // Try to load from cache on exception
            var cachedData3 = await _cacheService.GetCachedScoutingDataAsync();
            if (cachedData3 != null && cachedData3.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached scouting data after error (offline mode)");
                return new ScoutingListResponse 
                { 
                    Success = true, 
                    Entries = cachedData3,
                    Error = "Using cached data (offline mode)"
                };
            }
            
            return new ScoutingListResponse
            {
                Success = false,
                Error = $"Connection error: {ex.Message}"
            };
        }
    }
}
