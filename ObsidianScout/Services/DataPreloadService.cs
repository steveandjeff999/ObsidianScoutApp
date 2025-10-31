using ObsidianScout.Models;
using System.Linq;

namespace ObsidianScout.Services;

/// <summary>
/// Service responsible for preloading and caching all data on app startup
/// Ensures data is available offline and persists across app restarts
/// </summary>
public interface IDataPreloadService
{
 Task PreloadAllDataAsync(bool force = false);
 bool IsPreloading { get; }
 string PreloadStatus { get; }
}

public class DataPreloadService : IDataPreloadService
{
 private readonly IApiService _apiService;
 private readonly ICacheService _cacheService;
 private readonly ISettingsService _settingsService;

 private bool _isPreloading;
 private string _preloadStatus = string.Empty;

 public bool IsPreloading => _isPreloading;
 public string PreloadStatus => _preloadStatus;

 public DataPreloadService(IApiService apiService, ICacheService cacheService, ISettingsService settingsService)
 {
 _apiService = apiService;
 _cacheService = cacheService;
 _settingsService = settingsService;
 }

 public async Task PreloadAllDataAsync(bool force = false)
 {
 if (_isPreloading)
 {
 System.Diagnostics.Debug.WriteLine("[Preload] Already preloading, skipping");
 return;
 }

 _isPreloading = true;

 try
 {
 System.Diagnostics.Debug.WriteLine("=== DATA PRELOAD START ===");

 // If not forcing, check if user is logged in and skip if no token (to avoid unauthenticated endpoints)
 var token = await _settingsService.GetTokenAsync();
 if (string.IsNullOrEmpty(token) && !force)
 {
 System.Diagnostics.Debug.WriteLine("[Preload] No token found and preload not forced - using existing cache");
 _isPreloading = false;
 return;
 }

 // Ensure cache markers exist
 await _cacheService.PreloadAllDataAsync();

 // Perform critical preload steps synchronously so callers (Cache All) can wait for completion
 await PreloadCriticalDataAsync();

 System.Diagnostics.Debug.WriteLine("=== DATA PRELOAD COMPLETED ===");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Preload] Error: {ex.Message}");
 }
 finally
 {
 _isPreloading = false;
 }
 }

 private async Task PreloadCriticalDataAsync()
 {
 try
 {
 System.Diagnostics.Debug.WriteLine("[Preload] Critical preload started");

 _preloadStatus = "Loading game config...";
 await PreloadGameConfigAsync();

 _preloadStatus = "Loading events...";
 await PreloadEventsAsync();

 _preloadStatus = "Loading teams...";
 await PreloadTeamsAsync();

 _preloadStatus = "Loading matches...";
 await PreloadMatchesAsync();

 _preloadStatus = "Loading metrics...";
 await PreloadMetricsAsync();

 _preloadStatus = "Loading scouting data...";
 await PreloadScoutingDataAsync();

 _preloadStatus = "Preload complete";
 System.Diagnostics.Debug.WriteLine("[Preload] Critical preload completed successfully");
 }
 catch (Exception ex)
 {
 _preloadStatus = $"Preload failed: {ex.Message}";
 System.Diagnostics.Debug.WriteLine($"[Preload] Critical preload failed: {ex.Message}");
 }
 }

 private async Task PreloadGameConfigAsync()
 {
 try
 {
 if (!await _cacheService.IsCacheExpiredAsync("cache_game_config", TimeSpan.FromHours(24)))
 {
 System.Diagnostics.Debug.WriteLine("[Preload] Game config cache is fresh, skipping");
 return;
 }

 var response = await _apiService.GetGameConfigAsync();
 if (response.Success && response.Config != null)
 {
 await _cacheService.CacheGameConfigAsync(response.Config);
 System.Diagnostics.Debug.WriteLine($"[Preload] Game config cached: {response.Config.GameName}");
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Preload] Failed to preload game config: {ex.Message}");
 }
 }

 private async Task PreloadEventsAsync()
 {
 try
 {
 if (!await _cacheService.IsCacheExpiredAsync("cache_events", TimeSpan.FromHours(12)))
 {
 System.Diagnostics.Debug.WriteLine("[Preload] Events cache is fresh, skipping");
 return;
 }

 var response = await _apiService.GetEventsAsync();
 if (response.Success && response.Events != null && response.Events.Count >0)
 {
 await _cacheService.CacheEventsAsync(response.Events);
 System.Diagnostics.Debug.WriteLine($"[Preload] Events cached: {response.Events.Count} events");
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Preload] Failed to preload events: {ex.Message}");
 }
 }

 private async Task PreloadTeamsAsync()
 {
 try
 {
 if (!await _cacheService.IsCacheExpiredAsync("cache_teams", TimeSpan.FromHours(24)))
 {
 System.Diagnostics.Debug.WriteLine("[Preload] Teams cache is fresh, skipping");
 return;
 }

 var response = await _apiService.GetTeamsAsync(limit:500);
 if (response.Success && response.Teams != null && response.Teams.Count >0)
 {
 await _cacheService.CacheTeamsAsync(response.Teams);
 System.Diagnostics.Debug.WriteLine($"[Preload] Teams cached: {response.Teams.Count} teams");
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Preload] Failed to preload teams: {ex.Message}");
 }
 }

 private async Task PreloadMatchesAsync()
 {
 try
 {
 var gameConfig = await _cacheService.GetCachedGameConfigAsync();
 if (gameConfig == null || string.IsNullOrEmpty(gameConfig.CurrentEventCode))
 {
 System.Diagnostics.Debug.WriteLine("[Preload] No current event in config, skipping matches preload");
 return;
 }

 var events = await _cacheService.GetCachedEventsAsync();
 if (events == null || events.Count ==0)
 {
 System.Diagnostics.Debug.WriteLine("[Preload] No events cached, fetching...");
 var eventsResponse = await _apiService.GetEventsAsync();
 if (eventsResponse.Success && eventsResponse.Events != null)
 {
 events = eventsResponse.Events;
 }
 }

 if (events != null)
 {
 var currentEvent = events.FirstOrDefault(e =>
 e.Code.Equals(gameConfig.CurrentEventCode, StringComparison.OrdinalIgnoreCase));

 if (currentEvent != null)
 {
 if (!await _cacheService.IsCacheExpiredAsync($"cache_matches_event_{currentEvent.Id}", TimeSpan.FromHours(6)))
 {
 System.Diagnostics.Debug.WriteLine($"[Preload] Matches cache for event {currentEvent.Id} is fresh, skipping");
 return;
 }

 var matchesResponse = await _apiService.GetMatchesAsync(currentEvent.Id);
 if (matchesResponse.Success && matchesResponse.Matches != null && matchesResponse.Matches.Count >0)
 {
 await _cacheService.CacheMatchesAsync(matchesResponse.Matches, currentEvent.Id);
 System.Diagnostics.Debug.WriteLine($"[Preload] Matches cached for event {currentEvent.Code}: {matchesResponse.Matches.Count} matches");
 }
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Preload] Failed to preload matches: {ex.Message}");
 }
 }

 private async Task PreloadMetricsAsync()
 {
 try
 {
 if (!await _cacheService.IsCacheExpiredAsync("cache_available_metrics", TimeSpan.FromHours(24)))
 {
 System.Diagnostics.Debug.WriteLine("[Preload] Metrics cache is fresh, skipping");
 return;
 }

 var response = await _apiService.GetAvailableMetricsAsync();
 if (response.Success && response.Metrics != null && response.Metrics.Count >0)
 {
 await _cacheService.CacheAvailableMetricsAsync(response.Metrics);
 System.Diagnostics.Debug.WriteLine($"[Preload] Metrics cached: {response.Metrics.Count} metrics");
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Preload] Failed to preload metrics: {ex.Message}");
 }
 }

 private async Task PreloadScoutingDataAsync()
 {
 try
 {
 var gameConfig = await _cacheService.GetCachedGameConfigAsync();
 if (gameConfig == null || string.IsNullOrEmpty(gameConfig.CurrentEventCode))
 {
 System.Diagnostics.Debug.WriteLine("[Preload] No current event in config, skipping scouting data preload");
 return;
 }

 var events = await _cacheService.GetCachedEventsAsync();
 if (events == null || events.Count ==0)
 {
 // Try fetching events from API as fallback
 var eventsResponse = await _apiService.GetEventsAsync();
 if (eventsResponse.Success && eventsResponse.Events != null)
 {
 events = eventsResponse.Events;
 }
 }

 if (events != null)
 {
 var currentEvent = events.FirstOrDefault(e =>
 e.Code.Equals(gameConfig.CurrentEventCode, StringComparison.OrdinalIgnoreCase));

 if (currentEvent != null)
 {
 if (!await _cacheService.IsCacheExpiredAsync("cache_scouting_data", TimeSpan.FromHours(1)))
 {
 System.Diagnostics.Debug.WriteLine("[Preload] Scouting data cache is fresh, skipping");
 return;
 }

 var scoutingResponse = await _apiService.GetAllScoutingDataAsync(eventId: currentEvent.Id, limit:200);
 if (scoutingResponse.Success && scoutingResponse.Entries != null && scoutingResponse.Entries.Count >0)
 {
 await _cacheService.CacheScoutingDataAsync(scoutingResponse.Entries);
 System.Diagnostics.Debug.WriteLine($"[Preload] Scouting data cached for event {currentEvent.Code}: {scoutingResponse.Entries.Count} entries");
 }
 else
 {
 System.Diagnostics.Debug.WriteLine("[Preload] No scouting entries returned from API");
 }
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Preload] Failed to preload scouting data: {ex.Message}");
 }
 }
}
