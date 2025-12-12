using ObsidianScout.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObsidianScout.Services;

public partial class ApiService : IApiService
{
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

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settings_service;
    private readonly ICacheService _cache_service;
    private readonly IConnectivityService _connectivity_service;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(HttpClient httpClient, ISettingsService settingsService, ICacheService cacheService, IConnectivityService connectivityService)
    {
        _httpClient = httpClient;
        _settings_service = settingsService;
        _cache_service = cacheService;
        _connectivity_service = connectivityService;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
          DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new FlexibleDateTimeConverter() }
        };
    }

    /// <summary>
    /// Updates the HttpClient timeout from settings.
    /// Call this when network timeout setting changes.
    /// </summary>
    public async Task UpdateHttpClientTimeoutAsync()
    {
   try
        {
            var timeoutSeconds = await _settings_service.GetNetworkTimeoutAsync();
 _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        System.Diagnostics.Debug.WriteLine($"[ApiService] HttpClient timeout updated to {timeoutSeconds} seconds");
        }
        catch (Exception ex)
        {
  System.Diagnostics.Debug.WriteLine($"[ApiService] Failed to update HttpClient timeout: {ex.Message}");
        }
    }

    // New helper: returns true when network calls should be attempted (offline mode disabled AND connectivity present)
    private async Task<bool> ShouldUseNetworkAsync()
    {
        try
        {
            // First check connectivity service state
            if (_connectivity_service == null || !_connectivity_service.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[API] No connectivity - using cache");
                return false;
            }

            var offlineMode = await _settings_service.GetOfflineModeAsync();
            if (offlineMode)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline mode is enabled in settings - using cache instead of network");
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Failed to check network/offline mode: {ex.Message}");
            // Default to trying network if we can't determine state
            return _connectivity_service?.IsConnected ?? false;
        }

        return true;
    }

    private async Task<string> GetBaseUrlAsync()
    {
        var serverUrl = await _settings_service.GetServerUrlAsync();
        return $"{serverUrl.TrimEnd('/')}/api/mobile";
    }

    private async Task<HttpRequestMessage> CreateRequestMessageAsync(HttpMethod method, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url), "URL cannot be null or empty");
        }

        var request = new HttpRequestMessage(method, url);
        try
        {
            var token = await _settings_service.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] CreateRequestMessageAsync token error: {ex.Message}");
            // Continue without auth header
        }
        return request;
    }

    private async Task AddAuthHeaderAsync()
    {
        // Deprecated: Do not modify DefaultRequestHeaders as it is not thread-safe.
        // Use CreateRequestMessageAsync instead.
        // await Task.CompletedTask;

        try
        {
            if (_httpClient == null)
            {
                System.Diagnostics.Debug.WriteLine("[ApiService] AddAuthHeaderAsync: HttpClient is null");
                return;
            }

            var token = await _settings_service.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }
        catch (InvalidOperationException opEx)
        {
            // Can happen if headers are being accessed from another thread
            System.Diagnostics.Debug.WriteLine($"[ApiService] AddAuthHeaderAsync InvalidOperationException: {opEx.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] AddAuthHeaderAsync error: {ex.Message}");
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
                    await _settings_service.SetTokenAsync(result.Token);
                    await _settings_service.SetTokenExpirationAsync(result.ExpiresAt);
                    
                    // Store the username for auto-filling scout name
                    if (result.User != null && !string.IsNullOrEmpty(result.User.Username))
                    {
                        await _settings_service.SetUsernameAsync(result.User.Username);
                    }
                    
                    // Store user roles
                    if (result.User != null && result.User.Roles != null && result.User.Roles.Count > 0)
                    {
                        await _settings_service.SetUserRolesAsync(result.User.Roles);
                        
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
                    await _settings_service.SetTokenAsync(result.Token);
                    await _settings_service.SetTokenExpirationAsync(result.ExpiresAt);
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

    public async Task<UserProfileResponse> GetUserProfileAsync()
    {
        try
        {
         await AddAuthHeaderAsync();
       var baseUrl = await GetBaseUrlAsync();
        var response = await _httpClient.GetAsync($"{baseUrl}/profiles/me");

         if (response.IsSuccessStatusCode)
 {
             var result = await response.Content.ReadFromJsonAsync<UserProfileResponse>(_jsonOptions);
       return result ?? new UserProfileResponse { Success = false, Error = "Invalid response" };
            }

var errorContent = await response.Content.ReadAsStringAsync();
            return new UserProfileResponse 
        { 
      Success = false, 
           Error = $"Request failed with status {response.StatusCode}: {errorContent}" 
            };
        }
        catch (Exception ex)
        {
        System.Diagnostics.Debug.WriteLine($"[API] Get profile failed: {ex.Message}");
  return new UserProfileResponse
       {
                Success = false,
      Error = $"Connection error: {ex.Message}"
            };
        }
    }

 public async Task<byte[]?> GetProfilePictureAsync()
 {
 try
 {
 System.Diagnostics.Debug.WriteLine("[API] GetProfilePictureAsync called");

 // If offline or offline-mode, return cached picture if available
 if (!await ShouldUseNetworkAsync())
 {
 var cached = await _cache_service.GetCachedProfilePictureAsync();
 if (cached != null && cached.Length >0)
 {
 System.Diagnostics.Debug.WriteLine($"[API] Offline - returning cached profile picture ({cached.Length} bytes)");
 return cached;
 }

 System.Diagnostics.Debug.WriteLine("[API] Offline and no cached profile picture available");
 return null;
 }

 await AddAuthHeaderAsync();
 var baseUrl = await GetBaseUrlAsync();
 var url = $"{baseUrl}/profiles/me/picture";

 System.Diagnostics.Debug.WriteLine($"[API] Profile picture URL: {url}");

 // Check if auth header is set
 var authHeader = _httpClient.DefaultRequestHeaders.Authorization?.ToString();
 if (!string.IsNullOrEmpty(authHeader) && authHeader.Length >20)
 {
 System.Diagnostics.Debug.WriteLine($"[API] Auth header: {authHeader.Substring(0,20)}...");
 }
 else
 {
 System.Diagnostics.Debug.WriteLine($"[API] ? WARNING: No auth header or invalid auth header");
 }

 var response = await _httpClient.GetAsync(url);

 System.Diagnostics.Debug.WriteLine($"[API] Response status: {(int)response.StatusCode} {response.StatusCode}");
 System.Diagnostics.Debug.WriteLine($"[API] Response content type: {response.Content.Headers.ContentType?.MediaType ?? "null"}");
 System.Diagnostics.Debug.WriteLine($"[API] Response content length: {response.Content.Headers.ContentLength?.ToString() ?? "null"}");

 if (response.IsSuccessStatusCode)
 {
 var bytes = await response.Content.ReadAsByteArrayAsync();
 System.Diagnostics.Debug.WriteLine($"[API] ? Received {bytes.Length} bytes for profile picture");

 // Log first few bytes to verify image data
 if (bytes.Length >=4)
 {
 System.Diagnostics.Debug.WriteLine($"[API] First4 bytes: {bytes[0]:X2} {bytes[1]:X2} {bytes[2]:X2} {bytes[3]:X2}");
 }

 // Cache the profile picture for offline use
 try
 {
 await _cache_service.CacheProfilePictureAsync(bytes);
 }
 catch (Exception cex)
 {
 System.Diagnostics.Debug.WriteLine($"[API] Failed to cache profile picture: {cex.Message}");
 }

 return bytes;
 }
 else
 {
 var errorContent = await response.Content.ReadAsStringAsync();
 System.Diagnostics.Debug.WriteLine($"[API] ? Profile picture request failed");
 System.Diagnostics.Debug.WriteLine($"[API] Error content: {errorContent}");

 // Try to return cached picture when server responds with error
 try
 {
 var cached = await _cache_service.GetCachedProfilePictureAsync();
 if (cached != null && cached.Length >0)
 {
 System.Diagnostics.Debug.WriteLine($"[API] Returning cached profile picture after server error ({cached.Length} bytes)");
 return cached;
 }
 }
 catch (Exception cex)
 {
 System.Diagnostics.Debug.WriteLine($"[API] Failed to read cached profile picture: {cex.Message}");
 }
 }

 return null;
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[API] ? Error fetching profile picture");
 System.Diagnostics.Debug.WriteLine($"[API] Exception type: {ex.GetType().Name}");
 System.Diagnostics.Debug.WriteLine($"[API] Exception message: {ex.Message}");
 if (ex.InnerException != null)
 {
 System.Diagnostics.Debug.WriteLine($"[API] Inner exception: {ex.InnerException.Message}");
 }

 // On exception, try to return cached picture as fallback
 try
 {
 var cached = await _cache_service.GetCachedProfilePictureAsync();
 if (cached != null && cached.Length >0)
 {
 System.Diagnostics.Debug.WriteLine($"[API] Returning cached profile picture due to exception ({cached.Length} bytes)");
 return cached;
 }
 }
 catch (Exception cex)
 {
 System.Diagnostics.Debug.WriteLine($"[API] Failed to read cached profile picture after exception: {cex.Message}");
 }

 return null;
 }
 }

    public async Task<TeamsResponse> GetTeamsAsync(int? eventId = null, int limit = 100, int offset = 0)
    {
        // If we know we're offline or user requested offline mode, return cached data quickly without attempting network
        if (!await ShouldUseNetworkAsync())
        {
            var cachedTeams = await _cache_service.GetCachedTeamsAsync();
            if (cachedTeams != null && cachedTeams.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Offline or offline-mode - returning cached teams immediately");
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
                    await _cache_service.CacheTeamsAsync(result.Teams);
                }
                
                return result ?? new TeamsResponse { Success = false };
            }

            // Try to load from cache on failure
            var cachedTeams2 = await _cache_service.GetCachedTeamsAsync();
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
            var cachedTeams3 = await _cache_service.GetCachedTeamsAsync();
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
        if (!await ShouldUseNetworkAsync())
        {
            var cachedEvents = await _cache_service.GetCachedEventsAsync();
            if (cachedEvents != null && cachedEvents.Count >0)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline-mode - returning cached events immediately");
                return new EventsResponse { Success = true, Events = cachedEvents, Error = "Using cached data (offline mode)" };
            }
            return new EventsResponse { Success = false, Error = "Offline - no cached events available" };
        }
        
        try
        {
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/events";
            
            var request = await CreateRequestMessageAsync(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<EventsResponse>(_jsonOptions);
                
                // Cache the events data
                if (result != null && result.Success && result.Events != null && result.Events.Count >0)
                {
                    await _cache_service.CacheEventsAsync(result.Events);
                }
                
                return result ?? new EventsResponse { Success = false, Error = "Invalid response format" };
            }

            // Try to load from cache on failure
            var cachedEvents2 = await _cache_service.GetCachedEventsAsync();
            if (cachedEvents2 != null && cachedEvents2.Count >0)
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
            var cachedEvents3 = await _cache_service.GetCachedEventsAsync();
            if (cachedEvents3 != null && cachedEvents3.Count >0)
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
            var cachedEvents4 = await _cache_service.GetCachedEventsAsync();
            if (cachedEvents4 != null && cachedEvents4.Count >0)
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
        if (!await ShouldUseNetworkAsync())
        {
            var cachedMatches = await _cache_service.GetCachedMatchesAsync(eventId);
            if (cachedMatches != null && cachedMatches.Count >0)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline-mode - returning cached matches immediately");
                return new MatchesResponse { Success = true, Matches = cachedMatches, Error = "Using cached data (offline mode)" };
            }
            return new MatchesResponse { Success = false, Error = "Offline - no cached matches available" };
        }
        
        try
        {
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/matches?event_id={eventId}";
            
            if (!string.IsNullOrEmpty(matchType))
                url += $"&match_type={matchType}";
            
            if (teamNumber.HasValue)
                url += $"&team_number={teamNumber.Value}";

            var request = await CreateRequestMessageAsync(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<MatchesResponse>(_jsonOptions);
                
                // Cache the matches data
                if (result != null && result.Success && result.Matches != null && result.Matches.Count > 0)
                {
                    await _cache_service.CacheMatchesAsync(result.Matches, eventId);
                }
                
                return result ?? new MatchesResponse { Success = false, Error = "Invalid response format" };
            }

            // Try to load from cache on failure
            var cachedMatches2 = await _cache_service.GetCachedMatchesAsync(eventId);
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
            var cachedMatches3 = await _cache_service.GetCachedMatchesAsync(eventId);
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
            var cachedMatches4 = await _cache_service.GetCachedMatchesAsync(eventId);
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
        if (!_connectivity_service.IsConnected)
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
        if (!await ShouldUseNetworkAsync())
        {
            var cachedConfig = await _cache_service.GetCachedGameConfigAsync();
            if (cachedConfig != null)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline-mode - returning cached game config immediately");
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
                    await _cache_service.CacheGameConfigAsync(result.Config);
                }
                
                return result ?? new GameConfigResponse { Success = false, Error = "Invalid response" };
            }

            // Try to load from cache on failure
            var cachedConfig2 = await _cache_service.GetCachedGameConfigAsync();
            if (cachedConfig2 != null)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached game config (server returned {response.StatusCode})");
                return new GameConfigResponse 
                { 
                    Success = true, 
                    Config = cachedConfig2,
                    Error = $"?? Using cached data (server error: {response.StatusCode})"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new GameConfigResponse 
            { 
                Success = false, 
                Error = $"Request failed with status {response.StatusCode}" 
            };
        }
        catch (TaskCanceledException tcEx)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Game config request timed out: {tcEx.Message}");
            
  // Try to load from cache on timeout
    var cachedConfig3 = await _cache_service.GetCachedGameConfigAsync();
    if (cachedConfig3 != null)
  {
       System.Diagnostics.Debug.WriteLine($"[API] Using cached game config after timeout");
     return new GameConfigResponse 
            { 
       Success = true, 
   Config = cachedConfig3,
        Error = "?? Using cached data (server timeout)"
 };
          }
            
            return new GameConfigResponse
    {
           Success = false,
   Error = $"Connection timeout: Server took too long to respond"
          };
        }
        catch (Exception ex)
   {
            System.Diagnostics.Debug.WriteLine($"[API] Game config request failed: {ex.Message}");

      // Try to load from cache on failure
            var cachedConfig4 = await _cache_service.GetCachedGameConfigAsync();
            if (cachedConfig4 != null)
            {
        System.Diagnostics.Debug.WriteLine($"[API] Using cached game config after error (offline mode)");
       return new GameConfigResponse 
   { 
       Success = true, 
     Config = cachedConfig4,
    Error = $"?? Using cached data ({ex.Message})"
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
        if (!await ShouldUseNetworkAsync())
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
        if (!await ShouldUseNetworkAsync())
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
        if (!await ShouldUseNetworkAsync())
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
        if (!await ShouldUseNetworkAsync())
        {
            var cachedMetrics = await _cache_service.GetCachedAvailableMetricsAsync();
            if (cachedMetrics != null && cachedMetrics.Count >0)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline-mode - returning cached metrics immediately");
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
                    await _cache_service.CacheAvailableMetricsAsync(result.Metrics);
                }
                
                return result ?? new MetricsResponse { Success = false, Error = "Invalid response" };
            }

            // Try to load from cache on failure
            var cachedMetrics2 = await _cache_service.GetCachedAvailableMetricsAsync();
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
            var cachedMetrics3 = await _cache_service.GetCachedAvailableMetricsAsync();
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

    // Helper to filter cached scouting entries by optional parameters
    private List<ScoutingEntry> FilterScoutingEntries(List<ScoutingEntry>? entries, int? teamNumber, int? eventId, int? matchId)
    {
        if (entries == null) return new List<ScoutingEntry>();
        var query = entries.AsEnumerable();
        if (teamNumber.HasValue) query = query.Where(e => e.TeamNumber == teamNumber.Value);
        if (eventId.HasValue) query = query.Where(e => e.EventId == eventId.Value);
        if (matchId.HasValue) query = query.Where(e => e.MatchId == matchId.Value);
        return query.ToList();
    }

    public async Task<ScoutingListResponse> GetAllScoutingDataAsync(int? teamNumber = null, int? eventId = null, int? matchId = null, int limit =200, int offset = 0)
    {
        if (!await ShouldUseNetworkAsync())
        {
            var cachedData = await _cache_service.GetCachedScoutingDataAsync();
            var filtered = FilterScoutingEntries(cachedData, teamNumber, eventId, matchId);
            if (filtered != null && filtered.Count >0)
            {
                System.Diagnostics.Debug.WriteLine("[API] Offline-mode - returning cached scouting data immediately (filtered)");
                return new ScoutingListResponse { Success = true, Entries = filtered, Error = "Using cached data (offline mode)" };
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
                    await _cache_service.CacheScoutingDataAsync(result.Entries);
                }
                
                System.Diagnostics.Debug.WriteLine($"Success: Fetched {result?.Entries.Count ??0} scouting entries");
                return result ?? new ScoutingListResponse { Success = false, Error = "Invalid response" };
            }

            // Try to load from cache on failure
            var cachedData2 = await _cache_service.GetCachedScoutingDataAsync();
            var filtered2 = FilterScoutingEntries(cachedData2, teamNumber, eventId, matchId);
            if (filtered2 != null && filtered2.Count >0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached scouting data (offline mode) - filtered");
                return new ScoutingListResponse 
                { 
                    Success = true, 
                    Entries = filtered2,
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
            var cachedData3 = await _cache_service.GetCachedScoutingDataAsync();
            var filtered3 = FilterScoutingEntries(cachedData3, teamNumber, eventId, matchId);
            if (filtered3 != null && filtered3.Count >0)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached scouting data after error (offline mode) - filtered");
                return new ScoutingListResponse 
                { 
                    Success = true, 
                    Entries = filtered3,
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

    public async Task<byte[]?> GetGraphsImageAsync(GraphImageRequest request)
    {
        if (!await ShouldUseNetworkAsync())
        {
            System.Diagnostics.Debug.WriteLine("[API] Offline-mode - cannot fetch server graph image");
            return null;
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/graphs";

            System.Diagnostics.Debug.WriteLine($"POST {endpoint} to generate graph image");

            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                System.Diagnostics.Debug.WriteLine($"Received {bytes.Length} bytes for graph image");
                return bytes;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Graph image request failed: {response.StatusCode} - {errorContent}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching graph image: {ex.Message}");
            return null;
        }
    }

    public async Task<ScheduledNotificationsResponse> GetScheduledNotificationsAsync(int limit =200, int offset =0)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ScheduledNotificationsResponse { Success = false, Error = "Offline - cannot fetch scheduled notifications" };
        }

        try
        {
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/notifications/scheduled?limit={limit}&offset={offset}";
            
            var request = await CreateRequestMessageAsync(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ScheduledNotificationsResponse>(_jsonOptions);
                return result ?? new ScheduledNotificationsResponse { Success = false, Error = "Invalid response" };
            }

            var err = await response.Content.ReadAsStringAsync();
            return new ScheduledNotificationsResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
        }
        catch (Exception ex)
        {
            return new ScheduledNotificationsResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<PastNotificationsResponse> GetPastNotificationsAsync(int limit =200, int offset =0)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new PastNotificationsResponse { Success = false, Error = "Offline - cannot fetch past notifications" };
        }

        try
        {
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/notifications/past?limit={limit}&offset={offset}";
            
            var request = await CreateRequestMessageAsync(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PastNotificationsResponse>(_jsonOptions);
                return result ?? new PastNotificationsResponse { Success = false, Error = "Invalid response" };
            }

            var err = await response.Content.ReadAsStringAsync();
            return new PastNotificationsResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
        }
        catch (Exception ex)
        {
            return new PastNotificationsResponse { Success = false, Error = ex.Message };
        }
    }

 // Implementations for chat members endpoints (ensure these are inside the ApiService class)
    public async Task<ChatMembersResponse> GetChatMembersAsync(string scope = "team")
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatMembersResponse { Success = false, Error = "Offline - cannot fetch members" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/chat/members?scope={Uri.EscapeDataString(scope)}";
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatMembersResponse>(_jsonOptions);
                return result ?? new ChatMembersResponse { Success = false, Error = "Invalid response" };
            }

            var err = await response.Content.ReadAsStringAsync();
            return new ChatMembersResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
        }
        catch (Exception ex)
        {
            return new ChatMembersResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatMembersResponse> GetChatMembersForTeamAsync(int teamNumber)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatMembersResponse { Success = false, Error = "Offline - cannot fetch members" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/chat/members?scope=team&team_number={teamNumber}";
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatMembersResponse>(_jsonOptions);
                return result ?? new ChatMembersResponse { Success = false, Error = "Invalid response" };
            }

            var err = await response.Content.ReadAsStringAsync();
            return new ChatMembersResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
        }
        catch (Exception ex)
        {
            return new ChatMembersResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatMessagesResponse> GetChatMessagesAsync(string type = "dm", string? user = null, string? group = null, int? allianceId = null, int limit =50, int offset =0)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatMessagesResponse { Success = false, Messages = new List<ChatMessage>(), Count =0 };
        }

        try
        {
            var baseUrl = await GetBaseUrlAsync();

            // Build URL conditionally: omit type when caller passes null or empty so server can infer by group param
            var url = $"{baseUrl}/chat/messages?limit={limit}&offset={offset}";
            if (!string.IsNullOrEmpty(type))
                url = $"{baseUrl}/chat/messages?type={Uri.EscapeDataString(type)}&limit={limit}&offset={offset}";

            if (!string.IsNullOrEmpty(user)) url += $"&user={Uri.EscapeDataString(user)}";
            if (!string.IsNullOrEmpty(group)) url += $"&group={Uri.EscapeDataString(group)}";
            if (allianceId.HasValue) url += $"&alliance_id={allianceId.Value}";

            System.Diagnostics.Debug.WriteLine($"[API] GetChatMessagesAsync GET {url}");

            var request = await CreateRequestMessageAsync(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[API] GetChatMessagesAsync Status: {(int)response.StatusCode} {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[API] GetChatMessagesAsync Response: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<ChatMessagesResponse>(responseContent, _jsonOptions);
                return result ?? new ChatMessagesResponse { Success = false };
            }

            return new ChatMessagesResponse { Success = false, Messages = new List<ChatMessage>(), Count =0 };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetChatMessagesAsync failed: {ex.Message}");
            return new ChatMessagesResponse { Success = false, Messages = new List<ChatMessage>(), Count =0 };
        }
    }

    public async Task<ChatSendResponse> SendChatAsync(ChatSendRequest request)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatSendResponse { Success = false, Error = "Offline - cannot send message", ErrorCode = "OFFLINE" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/chat/send";

            // Log request body for debugging
            try
            {
                var reqJson = System.Text.Json.JsonSerializer.Serialize(request, _jsonOptions);
                System.Diagnostics.Debug.WriteLine($"[API] SendChatAsync POST {endpoint}");
                System.Diagnostics.Debug.WriteLine($"[API] SendChatAsync Request: {reqJson}");
            }
            catch { }

            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[API] SendChatAsync Status: {(int)response.StatusCode} {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[API] SendChatAsync Response: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<ChatSendResponse>(responseContent, _jsonOptions);
                return result ?? new ChatSendResponse { Success = false, Error = "Invalid response" };
            }

            // Try to parse error as ChatSendResponse
            try
            {
                var err = System.Text.Json.JsonSerializer.Deserialize<ChatSendResponse>(responseContent, _jsonOptions);
                if (err != null) return err;
            }
            catch { /* ignore */ }

            return new ChatSendResponse { Success = false, Error = $"HTTP {response.StatusCode}: {responseContent}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendChatAsync exception: {ex.Message}");
            return new ChatSendResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatEditResponse> EditChatMessageAsync(ChatEditRequest request)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatEditResponse { Success = false, Error = "Offline - cannot edit message" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/chat/edit-message";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatEditResponse>(_jsonOptions);
                return result ?? new ChatEditResponse { Success = false, Error = "Invalid response" };
            }

            var err = await response.Content.ReadAsStringAsync();
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<ChatEditResponse>(err, _jsonOptions);
                if (parsed != null) return parsed;
            }
            catch { }

            return new ChatEditResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EditChatMessageAsync exception: {ex.Message}");
            return new ChatEditResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatDeleteResponse> DeleteChatMessageAsync(ChatDeleteRequest request)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatDeleteResponse { Success = false, Error = "Offline - cannot delete message" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/chat/delete-message";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatDeleteResponse>(_jsonOptions);
                return result ?? new ChatDeleteResponse { Success = false, Error = "Invalid response" };
            }

            var err = await response.Content.ReadAsStringAsync();
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<ChatDeleteResponse>(err, _jsonOptions);
                if (parsed != null) return parsed;
            }
            catch { }

            return new ChatDeleteResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteChatMessageAsync exception: {ex.Message}");
            return new ChatDeleteResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatReactResponse> ReactToChatMessageAsync(ChatReactRequest request)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatReactResponse { Success = false, Error = "Offline - cannot react to message" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/chat/react-message";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatReactResponse>(_jsonOptions);
                return result ?? new ChatReactResponse { Success = false, Error = "Invalid response" };
            }

            var err = await response.Content.ReadAsStringAsync();
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<ChatReactResponse>(err, _jsonOptions);
                if (parsed != null) return parsed;
            }
            catch { }

            return new ChatReactResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ReactToChatMessageAsync exception: {ex.Message}");
            return new ChatReactResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatGroupsResponse> GetChatGroupsAsync(int? teamNumber = null)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatGroupsResponse { Success = false, Error = "Offline - cannot fetch groups" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/chat/groups";
            if (teamNumber.HasValue) url += $"?team_number={teamNumber.Value}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatGroupsResponse>(_jsonOptions);
                return result ?? new ChatGroupsResponse { Success = false, Error = "Invalid response" };
            }

            var err = await response.Content.ReadAsStringAsync();
            return new ChatGroupsResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetChatGroupsAsync failed: {ex.Message}");
            return new ChatGroupsResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatCreateGroupResponse> CreateChatGroupAsync(ChatCreateGroupRequest request)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatCreateGroupResponse { Success = false, Error = "Offline - cannot create group" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/chat/groups";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatCreateGroupResponse>(_jsonOptions);
                return result ?? new ChatCreateGroupResponse { Success = false, Error = "Invalid response" };
            }

            var err = await response.Content.ReadAsStringAsync();
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<ChatCreateGroupResponse>(err, _jsonOptions);
                if (parsed != null) return parsed;
            }
            catch { }

            return new ChatCreateGroupResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateChatGroupAsync failed: {ex.Message}");
            return new ChatCreateGroupResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatGroupMembersResponse> GetChatGroupMembersAsync(string group)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatGroupMembersResponse { Success = false, Error = "Offline - cannot fetch group members" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/chat/groups/{Uri.EscapeDataString(group)}/members";
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[API] GetChatGroupMembers GET {url}");
            System.Diagnostics.Debug.WriteLine($"[API] Response: {content}");

            if (response.IsSuccessStatusCode)
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<ChatGroupMembersResponse>(content, _jsonOptions);
                return result ?? new ChatGroupMembersResponse { Success = false, Error = "Invalid response" };
            }

            return new ChatGroupMembersResponse { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetChatGroupMembersAsync failed: {ex.Message}");
            return new ChatGroupMembersResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatGroupMembersResponse> AddChatGroupMembersAsync(string group, GroupMembersRequest request)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatGroupMembersResponse { Success = false, Error = "Offline - cannot add group members" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/chat/groups/{Uri.EscapeDataString(group)}/members";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
            var content = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[API] POST {endpoint} Request: {System.Text.Json.JsonSerializer.Serialize(request)}");
            System.Diagnostics.Debug.WriteLine($"[API] Response: {content}");

            if (response.IsSuccessStatusCode)
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<ChatGroupMembersResponse>(content, _jsonOptions);
                return result ?? new ChatGroupMembersResponse { Success = false, Error = "Invalid response" };
            }

            return new ChatGroupMembersResponse { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddChatGroupMembersAsync failed: {ex.Message}");
            return new ChatGroupMembersResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ChatGroupMembersResponse> RemoveChatGroupMembersAsync(string group, GroupMembersRequest request)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatGroupMembersResponse { Success = false, Error = "Offline - cannot remove group members" };
        }

        try
        {
            await AddAuthHeaderAsync();
            var baseUrl = await GetBaseUrlAsync();
            var endpoint = $"{baseUrl}/chat/groups/{Uri.EscapeDataString(group)}/members";

        // First attempt: if request has members, send them in DELETE body
          if (request != null && request.Members != null && request.Members.Count >0)
      {
          var httpRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint)
 {
      Content = JsonContent.Create(request, options: _jsonOptions)
     };
       var response = await _httpClient.SendAsync(httpRequest);
    var content = await response.Content.ReadAsStringAsync();
         System.Diagnostics.Debug.WriteLine($"[API] DELETE {endpoint} Request: {System.Text.Json.JsonSerializer.Serialize(request)}");
      System.Diagnostics.Debug.WriteLine($"[API] Response: {content}");

         if (response.IsSuccessStatusCode)
            {
      var result = System.Text.Json.JsonSerializer.Deserialize<ChatGroupMembersResponse>(content, _jsonOptions);
              return result ?? new ChatGroupMembersResponse { Success = false, Error = "Invalid response" };
           }

         System.Diagnostics.Debug.WriteLine($"RemoveChatGroupMembersAsync: first attempt failed with HTTP {response.StatusCode}, trying fallback delete-with-empty-body...");
            }

// Fallback attempt: send DELETE with no body so server will remove the requesting user
            var fallbackRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            var fallbackResponse = await _httpClient.SendAsync(fallbackRequest);
      var fallbackContent = await fallbackResponse.Content.ReadAsStringAsync();
      System.Diagnostics.Debug.WriteLine($"[API] DELETE {endpoint} (no body) Response: {fallbackContent}");

            if (fallbackResponse.IsSuccessStatusCode)
  {
        var result = System.Text.Json.JsonSerializer.Deserialize<ChatGroupMembersResponse>(fallbackContent, _jsonOptions);
           return result ?? new ChatGroupMembersResponse { Success = false, Error = "Invalid response" };
            }

            return new ChatGroupMembersResponse { Success = false, Error = $"HTTP {fallbackResponse.StatusCode}: {fallbackContent}" };
        }
    catch (Exception ex)
        {
    System.Diagnostics.Debug.WriteLine($"RemoveChatGroupMembersAsync failed: {ex.Message}");
       return new ChatGroupMembersResponse { Success = false, Error = ex.Message };
      }
    }

    public async Task<ChatStateResponse> GetChatStateAsync()
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ChatStateResponse { Success = false, Error = "Offline - cannot fetch chat state" };
        }

        try
        {
            var baseUrl = await GetBaseUrlAsync();
            var url = $"{baseUrl}/chat/state";
   
            System.Diagnostics.Debug.WriteLine($"[API] GetChatStateAsync GET {url}");
      
            var request = await CreateRequestMessageAsync(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
 
            System.Diagnostics.Debug.WriteLine($"[API] GetChatStateAsync Status: {(int)response.StatusCode} {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[API] GetChatStateAsync Response: {content}");

            if (response.IsSuccessStatusCode)
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<ChatStateResponse>(content, _jsonOptions);
                return result ?? new ChatStateResponse { Success = false, Error = "Invalid response" };
            }

            return new ChatStateResponse { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] GetChatStateAsync failed: {ex.Message}");
            return new ChatStateResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<ApiResponse<bool>> MarkChatMessagesAsReadAsync(string conversationId, string lastReadMessageId)
  {
  if (!await ShouldUseNetworkAsync())
    {
  return new ApiResponse<bool> { Success = false, Error = "Offline - cannot mark messages as read" };
  }

        try
   {
   await AddAuthHeaderAsync();
  var baseUrl = await GetBaseUrlAsync();
     // FIXED: Correct endpoint per API docs - no conversationId in URL path
       var endpoint = $"{baseUrl}/chat/conversations/read";
   
     // FIXED: Parse conversationId to extract type and id
    // Format: "dm_username", "group_groupname", or "alliance_id"
            string type = "dm";
       string id = conversationId;
          
         if (conversationId.StartsWith("dm_"))
            {
     type = "dm";
   id = conversationId.Substring(3); // Remove "dm_" prefix
       }
        else if (conversationId.StartsWith("group_"))
       {
    type = "group";
         id = conversationId.Substring(6); // Remove "group_" prefix
         }
 else if (conversationId.StartsWith("alliance_"))
       {
   type = "alliance";
    id = conversationId.Substring(9); // Remove "alliance_" prefix
    }
            
 // FIXED: Use correct request body format per API docs
        var requestBody = new 
            { 
         type = type,
   id = id,
       last_read_message_id = lastReadMessageId 
       };
 
    System.Diagnostics.Debug.WriteLine($"[API] MarkChatMessagesAsReadAsync POST {endpoint}");
     System.Diagnostics.Debug.WriteLine($"[API] Request: {System.Text.Json.JsonSerializer.Serialize(requestBody)}");
       
     var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, _jsonOptions);
    var content = await response.Content.ReadAsStringAsync();
       
   System.Diagnostics.Debug.WriteLine($"[API] MarkChatMessagesAsReadAsync Status: {(int)response.StatusCode} {response.StatusCode}");
   System.Diagnostics.Debug.WriteLine($"[API] MarkChatMessagesAsReadAsync Response: {content}");

      if (response.IsSuccessStatusCode)
 {
       var result = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<bool>>(content, _jsonOptions);
        return result ?? new ApiResponse<bool> { Success = true };
 }

    return new ApiResponse<bool> { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
        }
        catch (Exception ex)
        {
     System.Diagnostics.Debug.WriteLine($"[API] MarkChatMessagesAsReadAsync failed: {ex.Message}");
         return new ApiResponse<bool> { Success = false, Error = ex.Message };
   }
    }

    public async Task<ApiResponse<bool>> SaveGameConfigAsync(GameConfig config)
    {
        if (!await ShouldUseNetworkAsync())
        {
            return new ApiResponse<bool> { Success = false, Error = "Offline - cannot save config" };
        }

        try
 {
    await AddAuthHeaderAsync();
    var baseUrl = await GetBaseUrlAsync();
     var endpoint = $"{baseUrl}/config/game";
    
     System.Diagnostics.Debug.WriteLine("=== API: SAVE GAME CONFIG ===");
System.Diagnostics.Debug.WriteLine($"Endpoint: {endpoint}");
 
       var response = await _httpClient.PostAsJsonAsync(endpoint, config, _jsonOptions);
  var content = await response.Content.ReadAsStringAsync();

       System.Diagnostics.Debug.WriteLine($"Status Code: {(int)response.StatusCode} {response.StatusCode}");
   System.Diagnostics.Debug.WriteLine($"Response: {content}");

    if (response.IsSuccessStatusCode)
{
     return new ApiResponse<bool> { Success = true };
      }

      // Try to parse error body
      try
            {
         var parsed = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<bool>>(content, _jsonOptions);
       if (parsed != null)
  return parsed;
      }
      catch { }

       return new ApiResponse<bool> { Success = false, Error = $"HTTP {(int)response.StatusCode}: {content}" };
}
        catch (Exception ex)
        {
 System.Diagnostics.Debug.WriteLine($"[API] Save game config failed: {ex.Message}");
       return new ApiResponse<bool> { Success = false, Error = ex.Message };
    }
    }

    public async Task<PitConfigResponse> GetPitConfigAsync()
    {
        if (!await ShouldUseNetworkAsync())
        {
            var cachedConfig = await _cache_service.GetCachedPitConfigAsync();
   if (cachedConfig != null)
 {
                System.Diagnostics.Debug.WriteLine("[API] Offline-mode - returning cached pit config immediately");
     return new PitConfigResponse { Success = true, Config = cachedConfig, Error = "Using cached data (offline mode)" };
    }
        return new PitConfigResponse { Success = false, Error = "Offline - no cached pit config available" };
        }
        
        try
        {
   await AddAuthHeaderAsync();
     var baseUrl = await GetBaseUrlAsync();
       var response = await _httpClient.GetAsync($"{baseUrl}/config/pit");

          if (response.IsSuccessStatusCode)
            {
// Get raw JSON for debugging
         var rawJson = await response.Content.ReadAsStringAsync();
      System.Diagnostics.Debug.WriteLine("=== RAW PIT CONFIG JSON ===");
         System.Diagnostics.Debug.WriteLine(rawJson.Length > 2000 ? rawJson.Substring(0, 2000) + "... (truncated)" : rawJson);
    System.Diagnostics.Debug.WriteLine("=== END RAW JSON ===");
          
                var result = await response.Content.ReadFromJsonAsync<PitConfigResponse>(_jsonOptions);
    
        // Cache the pit config
    if (result != null && result.Success && result.Config != null)
         {
           await _cache_service.CachePitConfigAsync(result.Config);
      }
       
      return result ?? new PitConfigResponse { Success = false, Error = "Invalid response" };
            }

       // Try to load from cache on failure
var cachedConfig2 = await _cache_service.GetCachedPitConfigAsync();
            if (cachedConfig2 != null)
     {
       System.Diagnostics.Debug.WriteLine($"[API] Using cached pit config (server returned {response.StatusCode})");
            return new PitConfigResponse 
          { 
            Success = true, 
          Config = cachedConfig2,
     Error = $"?? Using cached data (server error: {response.StatusCode})"
          };
            }

    return new PitConfigResponse 
        { 
                Success = false, 
        Error = $"Request failed with status {response.StatusCode}" 
            };
        }
        catch (Exception ex)
    {
    System.Diagnostics.Debug.WriteLine($"[API] Pit config request failed: {ex.Message}");

         // Try to load from cache on exception
            var cachedConfig3 = await _cache_service.GetCachedPitConfigAsync();
          if (cachedConfig3 != null)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached pit config after error (offline mode)");
                return new PitConfigResponse 
                { 
    Success = true, 
          Config = cachedConfig3,
          Error = $"?? Using cached data ({ex.Message})"
          };
            }
 
    return new PitConfigResponse
          {
   Success = false,
      Error = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<PitScoutingSubmitResponse> SubmitPitScoutingDataAsync(PitScoutingSubmission submission)
    {
    // If offline, return quickly indicating offline so UI can queue/handle
  if (!_connectivity_service.IsConnected)
    {
            return new PitScoutingSubmitResponse
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
   var endpoint = $"{baseUrl}/pit-scouting/submit";
  
            System.Diagnostics.Debug.WriteLine("=== API: SUBMIT PIT SCOUTING DATA ===");
            System.Diagnostics.Debug.WriteLine($"Timestamp: {startTime:yyyy-MM-dd HH:mm:ss.fff}");
   System.Diagnostics.Debug.WriteLine($"Endpoint: {endpoint}");
            System.Diagnostics.Debug.WriteLine($"Team ID: {submission.TeamId}");
          System.Diagnostics.Debug.WriteLine($"Data fields: {submission.Data.Count}");
            System.Diagnostics.Debug.WriteLine($"Images: {submission.Images?.Count ?? 0}");
     
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
        var result = System.Text.Json.JsonSerializer.Deserialize<PitScoutingSubmitResponse>(responseContent, _jsonOptions);
                
    if (result != null)
    {
        System.Diagnostics.Debug.WriteLine($"Parsed Success: {result.Success}");
     System.Diagnostics.Debug.WriteLine($"Parsed Pit Scouting ID: {result.PitScoutingId}");
        return result;
    }
        else
    {
      return new PitScoutingSubmitResponse { Success = false, Error = "Invalid response - null result" };
   }
          }
         catch (JsonException jsonEx)
 {
         System.Diagnostics.Debug.WriteLine($"ERROR: JSON deserialization failed: {jsonEx.Message}");
         return new PitScoutingSubmitResponse 
    { 
      Success = false, 
           Error = $"Invalid JSON response: {jsonEx.Message}",
                ErrorCode = "JSON_PARSE_ERROR"
   };
    }
      }
       else
         {
var errorContent = await response.Content.ReadAsStringAsync();
         System.Diagnostics.Debug.WriteLine($"Error Response Body: {errorContent}");
   
 // Try to parse error response
      try
      {
        if (!string.IsNullOrWhiteSpace(errorContent))
      {
       var errorResponse = System.Text.Json.JsonSerializer.Deserialize<PitScoutingSubmitResponse>(errorContent, _jsonOptions);
    if (errorResponse != null)
      {
        return errorResponse;
       }
           }
   }
  catch { }
          
        return new PitScoutingSubmitResponse 
     { 
       Success = false, 
       Error = $"HTTP {(int)response.StatusCode}: {errorContent}",
    ErrorCode = $"HTTP_{(int)response.StatusCode}"
     };
        }
        }
        catch (Exception ex)
        {
      System.Diagnostics.Debug.WriteLine($"=== PIT SCOUTING EXCEPTION ===");
       System.Diagnostics.Debug.WriteLine($"Type: {ex.GetType().Name}");
       System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
     
   return new PitScoutingSubmitResponse
   {
      Success = false,
  Error = $"Error: {ex.Message}",
          ErrorCode = "GENERAL_ERROR"
 };
        }
finally
   {
    var totalElapsed = (DateTime.Now - startTime).TotalMilliseconds;
 System.Diagnostics.Debug.WriteLine($"=== END PIT SCOUTING API CALL ({totalElapsed:F0}ms) ===\n");
      }
    }

    public async Task<PitScoutingListResponse> GetPitScoutingDataAsync(int? teamNumber = null)
    {
        if (!await ShouldUseNetworkAsync())
        {
       return new PitScoutingListResponse { Success = false, Error = "Offline - no cached pit scouting data available" };
      }
        
        try
   {
        await AddAuthHeaderAsync();
      var baseUrl = await GetBaseUrlAsync();
      var url = $"{baseUrl}/pit-scouting/all";
  
            if (teamNumber.HasValue)
        url += $"?team_number={teamNumber.Value}";

            System.Diagnostics.Debug.WriteLine($"=== API: FETCH PIT SCOUTING DATA ===");
  System.Diagnostics.Debug.WriteLine($"URL: {url}");
 
            var response = await _httpClient.GetAsync(url);
   
            System.Diagnostics.Debug.WriteLine($"Response Status: {(int)response.StatusCode} {response.StatusCode}");

            if (response.IsSuccessStatusCode)
  {
             var result = await response.Content.ReadFromJsonAsync<PitScoutingListResponse>(_jsonOptions);
       
    System.Diagnostics.Debug.WriteLine($"Success: Fetched {result?.Entries.Count ?? 0} pit scouting entries");
    return result ?? new PitScoutingListResponse { Success = false, Error = "Invalid response" };
     }

            var errorContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Error Content: {errorContent}");

        return new PitScoutingListResponse 
    { 
       Success = false, 
     Error = $"Request failed with status {response.StatusCode}" 
       };
        }
        catch (Exception ex)
        {
    System.Diagnostics.Debug.WriteLine($"[API] Pit scouting data request failed: {ex.Message}");
            return new PitScoutingListResponse
     {
       Success = false,
                Error = $"Connection error: {ex.Message}"
        };
        }
    }

    public async Task<PitScoutingEntry?> GetPitScoutingEntryAsync(int entryId)
    {
        if (!await ShouldUseNetworkAsync())
        {
      return null;
        }
     
   try
   {
     await AddAuthHeaderAsync();
  var baseUrl = await GetBaseUrlAsync();
    var url = $"{baseUrl}/pit-scouting/{entryId}";

            System.Diagnostics.Debug.WriteLine($"=== API: FETCH PIT SCOUTING ENTRY ===");
            System.Diagnostics.Debug.WriteLine($"URL: {url}");
         
            var response = await _httpClient.GetAsync(url);
     
  System.Diagnostics.Debug.WriteLine($"Response Status: {(int)response.StatusCode} {response.StatusCode}");

     if (response.IsSuccessStatusCode)
{
     var result = await response.Content.ReadFromJsonAsync<PitScoutingEntry>(_jsonOptions);
                return result;
            }

        return null;
  }
        catch (Exception ex)
        {
     System.Diagnostics.Debug.WriteLine($"[API] Pit scouting entry request failed: {ex.Message}");
            return null;
        }
    }

    public async Task<PitScoutingSubmitResponse> UpdatePitScoutingDataAsync(int entryId, PitScoutingSubmission submission)
    {
        if (!_connectivity_service.IsConnected)
        {
            return new PitScoutingSubmitResponse
            {
                Success = false,
                Error = "Offline - cannot update entry",
                ErrorCode = "OFFLINE"
            };
        }
        
        try
        {
        await AddAuthHeaderAsync();
    var baseUrl = await GetBaseUrlAsync();
   var endpoint = $"{baseUrl}/pit-scouting/{entryId}";
  
     System.Diagnostics.Debug.WriteLine("=== API: UPDATE PIT SCOUTING DATA ===");
   System.Diagnostics.Debug.WriteLine($"Endpoint: {endpoint}");
       System.Diagnostics.Debug.WriteLine($"Entry ID: {entryId}");
         System.Diagnostics.Debug.WriteLine($"Team ID: {submission.TeamId}");
        
 var response = await _httpClient.PutAsJsonAsync(endpoint, submission, _jsonOptions);
     
        System.Diagnostics.Debug.WriteLine($"Status Code: {(int)response.StatusCode} {response.StatusCode}");

            if (response.IsSuccessStatusCode)
      {
    var responseContent = await response.Content.ReadAsStringAsync();
    var result = System.Text.Json.JsonSerializer.Deserialize<PitScoutingSubmitResponse>(responseContent, _jsonOptions);
          return result ?? new PitScoutingSubmitResponse { Success = true, Message = "Updated successfully" };
            }
            else
     {
      var errorContent = await response.Content.ReadAsStringAsync();
   return new PitScoutingSubmitResponse 
     { 
      Success = false, 
         Error = $"HTTP {(int)response.StatusCode}: {errorContent}"
  };
            }
        }
        catch (Exception ex)
        {
System.Diagnostics.Debug.WriteLine($"Update pit scouting failed: {ex.Message}");
     return new PitScoutingSubmitResponse
   {
   Success = false,
     Error = $"Error: {ex.Message}",
          ErrorCode = "GENERAL_ERROR"
        };
        }
    }

    public async Task<ApiResponse<bool>> DeletePitScoutingEntryAsync(int entryId)
    {
        if (!_connectivity_service.IsConnected)
        {
            return new ApiResponse<bool> { Success = false, Error = "Offline - cannot delete entry" };
   }
        
        try
        {
     await AddAuthHeaderAsync();
    var baseUrl = await GetBaseUrlAsync();
       var endpoint = $"{baseUrl}/pit-scouting/{entryId}";
  
       System.Diagnostics.Debug.WriteLine($"=== API: DELETE PIT SCOUTING ENTRY ===");
     System.Diagnostics.Debug.WriteLine($"Endpoint: {endpoint}");
 
      var response = await _httpClient.DeleteAsync(endpoint);
     
    System.Diagnostics.Debug.WriteLine($"Status Code: {(int)response.StatusCode} {response.StatusCode}");

      if (response.IsSuccessStatusCode)
   {
return new ApiResponse<bool> { Success = true };
  }
   else
     {
       var errorContent = await response.Content.ReadAsStringAsync();
        return new ApiResponse<bool> 
  { 
        Success = false, 
    Error = $"HTTP {(int)response.StatusCode}: {errorContent}"
      };
      }
        }
   catch (Exception ex)
        {
   System.Diagnostics.Debug.WriteLine($"Delete pit scouting failed: {ex.Message}");
      return new ApiResponse<bool>
            {
        Success = false,
 Error = $"Error: {ex.Message}"
  };
    }
    }

    public async Task<ApiResponse<bool>> SavePitConfigAsync(PitConfig config)
    {
        if (!_connectivity_service.IsConnected)
        {
         return new ApiResponse<bool> { Success = false, Error = "Offline - cannot save pit config" };
        }

        try
 {
    await AddAuthHeaderAsync();
    var baseUrl = await GetBaseUrlAsync();
     var endpoint = $"{baseUrl}/config/pit";
    
     System.Diagnostics.Debug.WriteLine("=== API: SAVE PIT CONFIG ===");
System.Diagnostics.Debug.WriteLine($"Endpoint: {endpoint}");
 
       var response = await _httpClient.PostAsJsonAsync(endpoint, config, _jsonOptions);
  var content = await response.Content.ReadAsStringAsync();

       System.Diagnostics.Debug.WriteLine($"Status Code: {(int)response.StatusCode} {response.StatusCode}");
   System.Diagnostics.Debug.WriteLine($"Response: {content}");

    if (response.IsSuccessStatusCode)
{
     return new ApiResponse<bool> { Success = true };
      }

      // Try to parse error body
      try
            {
         var parsed = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<bool>>(content, _jsonOptions);
       if (parsed != null)
  return parsed;
      }
      catch { }

       return new ApiResponse<bool> { Success = false, Error = $"HTTP {(int)response.StatusCode}: {content}" };
}
        catch (Exception ex)
        {
 System.Diagnostics.Debug.WriteLine($"[API] Save pit config failed: {ex.Message}");
       return new ApiResponse<bool> { Success = false, Error = ex.Message };
    }
    }

    // Custom register method
 public async Task<LoginResponse> RegisterAsync(string username, string password, string? confirmPassword, int teamNumber, string? email)
 {
 try
 {
 var baseUrl = await GetBaseUrlAsync();
 var payload = new Dictionary<string, object?>
 {
 ["username"] = username,
 ["password"] = password,
 ["team_number"] = teamNumber
 };

 if (!string.IsNullOrEmpty(confirmPassword)) payload["confirm_password"] = confirmPassword;
 if (!string.IsNullOrEmpty(email)) payload["email"] = email;

 var endpoint = $"{baseUrl}/auth/register";
 var response = await _httpClient.PostAsJsonAsync(endpoint, payload, _jsonOptions);

 var content = await response.Content.ReadAsStringAsync();
 if (response.IsSuccessStatusCode)
 {
 try
 {
 var result = System.Text.Json.JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);
 if (result != null && result.Success)
 {
 // Persist token and user data similar to login
 if (!string.IsNullOrEmpty(result.Token))
 {
 await _settings_service.SetTokenAsync(result.Token);
 await _settings_service.SetTokenExpirationAsync(result.ExpiresAt);
 }

 if (result.User != null && !string.IsNullOrEmpty(result.User.Username))
 {
 await _settings_service.SetUsernameAsync(result.User.Username);
 }

 if (result.User?.Roles != null && result.User.Roles.Count >0)
 {
 await _settings_service.SetUserRolesAsync(result.User.Roles);
 }

 return result;
 }

 return result ?? new LoginResponse { Success = false, Error = "Invalid response" };
 }
 catch (System.Text.Json.JsonException jex)
 {
 return new LoginResponse { Success = false, Error = $"JSON parse error: {jex.Message}" };
 }
 }
 else
 {
 // Try to parse error body as LoginResponse to surface structured error codes
 try
 {
 var err = System.Text.Json.JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);
 if (err != null) return err;
 }
 catch { }

 return new LoginResponse { Success = false, Error = $"HTTP {(int)response.StatusCode}: {content}" };
 }
 }
 catch (Exception ex)
 {
 return new LoginResponse { Success = false, Error = ex.Message };
 }
 }

 public async Task<RolesResponse> GetAdminRolesAsync()
 {
 if (!await ShouldUseNetworkAsync())
 return new RolesResponse { Success = false, Error = "Offline - cannot fetch roles" };

 try
 {
 await AddAuthHeaderAsync();
 var baseUrl = await GetBaseUrlAsync();
 var url = $"{baseUrl}/admin/roles";
 var response = await _httpClient.GetAsync(url);
 if (response.IsSuccessStatusCode)
 {
 var result = await response.Content.ReadFromJsonAsync<RolesResponse>(_jsonOptions);
 return result ?? new RolesResponse { Success = false };
 }

 var err = await response.Content.ReadAsStringAsync();
 return new RolesResponse { Success = false, Error = $"HTTP {response.StatusCode}: {err}" };
 }
 catch (Exception ex)
 {
 return new RolesResponse { Success = false, Error = ex.Message };
 }
 }

 public async Task<UsersListResponse> GetAdminUsersAsync(string? search = null, int limit =200, int offset = 0)
 {
 if (!await ShouldUseNetworkAsync())
 return new UsersListResponse { Success = false, Error = "Offline - cannot fetch users" };

 try
 {
 await AddAuthHeaderAsync();
 var baseUrl = await GetBaseUrlAsync();
 var url = $"{baseUrl}/admin/users?limit={limit}&offset={offset}";
 if (!string.IsNullOrEmpty(search)) url += "&search=" + Uri.EscapeDataString(search);

 var response = await _httpClient.GetAsync(url);
 var content = await response.Content.ReadAsStringAsync();
 if (response.IsSuccessStatusCode)
 {
 var result = System.Text.Json.JsonSerializer.Deserialize<UsersListResponse>(content, _jsonOptions);
 return result ?? new UsersListResponse { Success = false };
 }

 return new UsersListResponse { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
 }
 catch (Exception ex)
 {
 return new UsersListResponse { Success = false, Error = ex.Message };
 }
 }

 public async Task<CreateUserResponse> CreateAdminUserAsync(CreateUserRequest request)
 {
 if (!await ShouldUseNetworkAsync())
 return new CreateUserResponse { Success = false, Error = "Offline - cannot create user" };

 try
 {
 await AddAuthHeaderAsync();
 var baseUrl = await GetBaseUrlAsync();
 var endpoint = $"{baseUrl}/admin/users";
 var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
 var content = await response.Content.ReadAsStringAsync();
 if (response.IsSuccessStatusCode)
 {
 var result = System.Text.Json.JsonSerializer.Deserialize<CreateUserResponse>(content, _jsonOptions);
 return result ?? new CreateUserResponse { Success = false };
 }

 return new CreateUserResponse { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
 }
 catch (Exception ex)
 {
 return new CreateUserResponse { Success = false, Error = ex.Message };
 }
 }

 public async Task<UserDetailResponse> GetAdminUserAsync(int userId)
 {
 if (!await ShouldUseNetworkAsync())
 return new UserDetailResponse { Success = false, Error = "Offline - cannot fetch user" };

 try
 {
 await AddAuthHeaderAsync();
 var baseUrl = await GetBaseUrlAsync();
 var url = $"{baseUrl}/admin/users/{userId}";
 var response = await _httpClient.GetAsync(url);
 var content = await response.Content.ReadAsStringAsync();
 if (response.IsSuccessStatusCode)
 {
 var result = System.Text.Json.JsonSerializer.Deserialize<UserDetailResponse>(content, _jsonOptions);
 return result ?? new UserDetailResponse { Success = false };
 }

 return new UserDetailResponse { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
 }
 catch (Exception ex)
 {
 return new UserDetailResponse { Success = false, Error = ex.Message };
 }
 }

 public async Task<UserDetailResponse> UpdateAdminUserAsync(int userId, UpdateUserRequest request)
 {
 if (!await ShouldUseNetworkAsync())
 return new UserDetailResponse { Success = false, Error = "Offline - cannot update user" };

 try
 {
 await AddAuthHeaderAsync();
 var baseUrl = await GetBaseUrlAsync();
 var endpoint = $"{baseUrl}/admin/users/{userId}";
 var response = await _httpClient.PutAsJsonAsync(endpoint, request, _jsonOptions);
 var content = await response.Content.ReadAsStringAsync();
 if (response.IsSuccessStatusCode)
 {
 var result = System.Text.Json.JsonSerializer.Deserialize<UserDetailResponse>(content, _jsonOptions);
 return result ?? new UserDetailResponse { Success = false };
 }

 return new UserDetailResponse { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
 }
 catch (Exception ex)
 {
 return new UserDetailResponse { Success = false, Error = ex.Message };
 }
 }

 public async Task<ApiResponse<bool>> DeleteAdminUserAsync(int userId)
 {
 if (!await ShouldUseNetworkAsync())
 return new ApiResponse<bool> { Success = false, Error = "Offline - cannot delete user" };

 try
 {
 await AddAuthHeaderAsync();
 var baseUrl = await GetBaseUrlAsync();
 var endpoint = $"{baseUrl}/admin/users/{userId}";
 var response = await _httpClient.DeleteAsync(endpoint);
 var content = await response.Content.ReadAsStringAsync();
 if (response.IsSuccessStatusCode)
 {
 return new ApiResponse<bool> { Success = true };
 }

 return new ApiResponse<bool> { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
 }
 catch (Exception ex)
 {
 return new ApiResponse<bool> { Success = false, Error = ex.Message };
 }
 }
}
