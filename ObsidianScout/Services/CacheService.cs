using ObsidianScout.Models;
using System.Text.Json;
using System.IO;
using Microsoft.Maui.Storage;

namespace ObsidianScout.Services;

/// <summary>
/// Centralized service for offline data caching
/// Caches all essential data for complete offline functionality
/// </summary>
public interface ICacheService
{
 // Preload all data on app startup
 Task PreloadAllDataAsync();
 
 // Game Config
 Task<GameConfig?> GetCachedGameConfigAsync();
 Task CacheGameConfigAsync(GameConfig config);
 
 // Pit Config
 Task<PitConfig?> GetCachedPitConfigAsync();
 Task CachePitConfigAsync(PitConfig config);
 
 // Events
 Task<List<Event>?> GetCachedEventsAsync();
 Task CacheEventsAsync(List<Event> events);
 
 // Teams
 Task<List<Team>?> GetCachedTeamsAsync();
 Task CacheTeamsAsync(List<Team> teams);
 
 // Matches
 Task<List<Match>?> GetCachedMatchesAsync(int? eventId = null);
 Task CacheMatchesAsync(List<Match> matches, int? eventId = null);
 
 // Scouting Data
 Task<List<ScoutingEntry>?> GetCachedScoutingDataAsync();
 Task CacheScoutingDataAsync(List<ScoutingEntry> scoutingData);
 
 // Team Metrics
 Task<TeamMetrics?> GetCachedTeamMetricsAsync(int teamId, int eventId);
 Task CacheTeamMetricsAsync(int teamId, int eventId, TeamMetrics metrics);
 
 // Available Metrics
 Task<List<MetricDefinition>?> GetCachedAvailableMetricsAsync();
 Task CacheAvailableMetricsAsync(List<MetricDefinition> metrics);
 
 // Profile picture (binary stored as base64 string)
 Task<byte[]?> GetCachedProfilePictureAsync();
 Task CacheProfilePictureAsync(byte[] pictureBytes);
 
 // Cache Management
 Task<DateTime?> GetCacheTimestampAsync(string key);
 Task ClearAllCacheAsync();
 Task<bool> IsCacheExpiredAsync(string key, TimeSpan maxAge);
 Task<bool> HasCachedDataAsync();
}

public class CacheService : ICacheService
{
 private const string CACHE_KEY_GAME_CONFIG = "cache_game_config";
 private const string CACHE_KEY_PIT_CONFIG = "cache_pit_config";
 private const string CACHE_KEY_EVENTS = "cache_events";
 private const string CACHE_KEY_TEAMS = "cache_teams";
 private const string CACHE_KEY_MATCHES = "cache_matches";
 private const string CACHE_KEY_SCOUTING_DATA = "cache_scouting_data";
 private const string CACHE_KEY_AVAILABLE_METRICS = "cache_available_metrics";
 private const string CACHE_KEY_LAST_PRELOAD = "cache_last_preload";
 private const string CACHE_KEY_PROFILE_PICTURE = "cache_profile_picture";
 
 private const string TIMESTAMP_SUFFIX = "_timestamp";
 
 private readonly JsonSerializerOptions _jsonOptions;

 public CacheService()
 {
 _jsonOptions = new JsonSerializerOptions
 {
 PropertyNameCaseInsensitive = true,
 WriteIndented = false
 };
 }

 #region Storage helpers

 private string GetFilePathForKey(string key) => Path.Combine(FileSystem.AppDataDirectory, key + ".json");

 private async Task SaveStringToCacheAsync(string key, string value)
 {
 // Try SecureStorage first (encrypted), fallback to file storage for large values
 try
 {
 await SecureStorage.SetAsync(key, value);
 return;
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] SecureStorage write failed for {key}: {ex.Message}. Falling back to file.");
 }

 try
 {
 var path = GetFilePathForKey(key);
 await File.WriteAllTextAsync(path, value);
 System.Diagnostics.Debug.WriteLine($"[Cache] Wrote {key} to file: {path}");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] File write failed for {key}: {ex.Message}");
 }
 }

 private async Task<string?> GetStringFromCacheAsync(string key)
 {
 try
 {
 var s = await SecureStorage.GetAsync(key);
 if (!string.IsNullOrEmpty(s))
 return s;
 }
 catch { /* ignore secure storage read errors and try file fallback */ }

 try
 {
 var path = GetFilePathForKey(key);
 if (File.Exists(path))
 {
 return await File.ReadAllTextAsync(path);
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] File read failed for {key}: {ex.Message}");
 }

 return null;
 }

 private void RemoveCacheKey(string key)
 {
 try { SecureStorage.Remove(key); } catch { }
 try
 {
 var path = GetFilePathForKey(key);
 if (File.Exists(path)) File.Delete(path);
 }
 catch { }

 try { SecureStorage.Remove(key + TIMESTAMP_SUFFIX); } catch { }
 try
 {
 var tsPath = GetFilePathForKey(key + TIMESTAMP_SUFFIX);
 if (File.Exists(tsPath)) File.Delete(tsPath);
 }
 catch { }
 }

 #endregion

 #region Preload

 /// <summary>
 /// Preloads all essential data on app startup if cache is missing or stale
 /// Data persists across app restarts via SecureStorage (uses platform-specific secure storage) or file fallback
 /// </summary>
 public async Task PreloadAllDataAsync()
 {
 try
 {
 System.Diagnostics.Debug.WriteLine("=== CACHE PRELOAD START ===");
 
 // Check if we have existing cache
 var hasCache = await HasCachedDataAsync();
 var lastPreload = await GetCacheTimestampAsync(CACHE_KEY_LAST_PRELOAD);
 
 if (hasCache && lastPreload.HasValue)
 {
 var cacheAge = DateTime.UtcNow - lastPreload.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Existing cache found (age: {cacheAge.TotalHours:F1}h)");
 
 // Cache is valid, no need to preload
 if (cacheAge < TimeSpan.FromHours(24))
 {
 System.Diagnostics.Debug.WriteLine("[Cache] Cache is fresh, skipping preload");
 System.Diagnostics.Debug.WriteLine("=== CACHE PRELOAD SKIPPED ===");
 return;
 }
 
 System.Diagnostics.Debug.WriteLine("[Cache] Cache is stale (>24h), refreshing in background");
 }
 else
 {
 System.Diagnostics.Debug.WriteLine("[Cache] No existing cache found, preloading all data");
 }

 // Mark preload start
 await SetCacheTimestampAsync(CACHE_KEY_LAST_PRELOAD);
 
 System.Diagnostics.Debug.WriteLine("[Cache] Preload complete - data will refresh from API calls");
 System.Diagnostics.Debug.WriteLine("=== CACHE PRELOAD END ===");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Preload failed: {ex.Message}");
 System.Diagnostics.Debug.WriteLine($"[Cache] Stack: {ex.StackTrace}");
 }
 }

 /// <summary>
 /// Check if we have any cached data available
 /// </summary>
 public async Task<bool> HasCachedDataAsync()
 {
 try
 {
 // Check if we have at least game config and events cached
 var gameConfig = await GetStringFromCacheAsync(CACHE_KEY_GAME_CONFIG);
 var events = await GetStringFromCacheAsync(CACHE_KEY_EVENTS);
 
 return !string.IsNullOrEmpty(gameConfig) && !string.IsNullOrEmpty(events);
 }
 catch
 {
 return false;
 }
 }

 #endregion

 #region Game Config

 public async Task<GameConfig?> GetCachedGameConfigAsync()
 {
 try
 {
 var json = await GetStringFromCacheAsync(CACHE_KEY_GAME_CONFIG);
 if (!string.IsNullOrEmpty(json))
 {
 var config = JsonSerializer.Deserialize<GameConfig>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_GAME_CONFIG);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Game config loaded from cache (age: {age.TotalHours:F1}h)");
 }
 
 return config;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load game config: {ex.Message}");
 }
 return null;
 }

 public async Task CacheGameConfigAsync(GameConfig config)
 {
 try
 {
 var json = JsonSerializer.Serialize(config, _jsonOptions);
 await SaveStringToCacheAsync(CACHE_KEY_GAME_CONFIG, json);
 await SetCacheTimestampAsync(CACHE_KEY_GAME_CONFIG);
 System.Diagnostics.Debug.WriteLine("[Cache] Game config cached successfully");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache game config: {ex.Message}");
 }
 }

 #endregion

 #region Pit Config

 public async Task<PitConfig?> GetCachedPitConfigAsync()
 {
 try
 {
 var json = await GetStringFromCacheAsync(CACHE_KEY_PIT_CONFIG);
 if (!string.IsNullOrEmpty(json))
 {
 var config = JsonSerializer.Deserialize<PitConfig>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_PIT_CONFIG);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Pit config loaded from cache (age: {age.TotalHours:F1}h)");
 }
 
 return config;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load pit config: {ex.Message}");
 }
 return null;
 }

 public async Task CachePitConfigAsync(PitConfig config)
 {
 try
 {
 var json = JsonSerializer.Serialize(config, _jsonOptions);
 await SaveStringToCacheAsync(CACHE_KEY_PIT_CONFIG, json);
 await SetCacheTimestampAsync(CACHE_KEY_PIT_CONFIG);
 System.Diagnostics.Debug.WriteLine("[Cache] Pit config cached successfully");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache pit config: {ex.Message}");
 }
 }

 #endregion

 #region Events

 public async Task<List<Event>?> GetCachedEventsAsync()
 {
 try
 {
 var json = await GetStringFromCacheAsync(CACHE_KEY_EVENTS);
 if (!string.IsNullOrEmpty(json))
 {
 var events = JsonSerializer.Deserialize<List<Event>>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_EVENTS);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Events loaded from cache (age: {age.TotalHours:F1}h, count: {events?.Count ??0})");
 }
 
 return events;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load events: {ex.Message}");
 }
 return null;
 }

 public async Task CacheEventsAsync(List<Event> events)
 {
 try
 {
 var json = JsonSerializer.Serialize(events, _jsonOptions);
 await SaveStringToCacheAsync(CACHE_KEY_EVENTS, json);
 await SetCacheTimestampAsync(CACHE_KEY_EVENTS);
 System.Diagnostics.Debug.WriteLine($"[Cache] Cached {events.Count} events");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache events: {ex.Message}");
 }
 }

 #endregion

 #region Teams

 public async Task<List<Team>?> GetCachedTeamsAsync()
 {
 try
 {
 var json = await GetStringFromCacheAsync(CACHE_KEY_TEAMS);
 if (!string.IsNullOrEmpty(json))
 {
 var teams = JsonSerializer.Deserialize<List<Team>>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_TEAMS);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Teams loaded from cache (age: {age.TotalHours:F1}h, count: {teams?.Count ??0})");
 }
 
 return teams;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load teams: {ex.Message}");
 }
 return null;
 }

 public async Task CacheTeamsAsync(List<Team> teams)
 {
 try
 {
 var json = JsonSerializer.Serialize(teams, _jsonOptions);
 await SaveStringToCacheAsync(CACHE_KEY_TEAMS, json);
 await SetCacheTimestampAsync(CACHE_KEY_TEAMS);
 System.Diagnostics.Debug.WriteLine($"[Cache] Cached {teams.Count} teams");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache teams: {ex.Message}");
 }
 }

 #endregion

 #region Matches

 public async Task<List<Match>?> GetCachedMatchesAsync(int? eventId = null)
 {
 try
 {
 var cacheKey = eventId.HasValue 
 ? $"{CACHE_KEY_MATCHES}_event_{eventId.Value}"
 : CACHE_KEY_MATCHES;
 
 var json = await GetStringFromCacheAsync(cacheKey);
 if (!string.IsNullOrEmpty(json))
 {
 var matches = JsonSerializer.Deserialize<List<Match>>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(cacheKey);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Matches loaded from cache (age: {age.TotalHours:F1}h, count: {matches?.Count ??0}, event: {eventId?.ToString() ?? "all"})");
 }
 
 return matches;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load matches: {ex.Message}");
 }
 return null;
 }

 public async Task CacheMatchesAsync(List<Match> matches, int? eventId = null)
 {
 try
 {
 var cacheKey = eventId.HasValue 
 ? $"{CACHE_KEY_MATCHES}_event_{eventId.Value}"
 : CACHE_KEY_MATCHES;
 
 var json = JsonSerializer.Serialize(matches, _jsonOptions);
 await SaveStringToCacheAsync(cacheKey, json);
 await SetCacheTimestampAsync(cacheKey);
 System.Diagnostics.Debug.WriteLine($"[Cache] Cached {matches.Count} matches (event: {eventId?.ToString() ?? "all"})");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache matches: {ex.Message}");
 }
 }

 #endregion

 #region Available Metrics

 public async Task<List<MetricDefinition>?> GetCachedAvailableMetricsAsync()
 {
 try
 {
 var json = await GetStringFromCacheAsync(CACHE_KEY_AVAILABLE_METRICS);
 if (!string.IsNullOrEmpty(json))
 {
 var metrics = JsonSerializer.Deserialize<List<MetricDefinition>>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_AVAILABLE_METRICS);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Available metrics loaded from cache (age: {age.TotalHours:F1}h, count: {metrics?.Count ??0})");
 }
 
 return metrics;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load available metrics: {ex.Message}");
 }
 return null;
 }

 public async Task CacheAvailableMetricsAsync(List<MetricDefinition> metrics)
 {
 try
 {
 var json = JsonSerializer.Serialize(metrics, _jsonOptions);
 await SaveStringToCacheAsync(CACHE_KEY_AVAILABLE_METRICS, json);
 await SetCacheTimestampAsync(CACHE_KEY_AVAILABLE_METRICS);
 System.Diagnostics.Debug.WriteLine($"[Cache] Cached {metrics.Count} available metrics");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache available metrics: {ex.Message}");
 }
 }

 #endregion

 #region Scouting Data

 public async Task<List<ScoutingEntry>?> GetCachedScoutingDataAsync()
 {
 try
 {
 var json = await GetStringFromCacheAsync(CACHE_KEY_SCOUTING_DATA);
 if (!string.IsNullOrEmpty(json))
 {
 var data = JsonSerializer.Deserialize<List<ScoutingEntry>>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_SCOUTING_DATA);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Scouting data loaded from cache (age: {age.TotalHours:F1}h, count: {data?.Count ??0})");
 }
 
 return data;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load scouting data: {ex.Message}");
 }
 return null;
 }

 public async Task CacheScoutingDataAsync(List<ScoutingEntry> scoutingData)
 {
 try
 {
 var json = JsonSerializer.Serialize(scoutingData, _jsonOptions);
 await SaveStringToCacheAsync(CACHE_KEY_SCOUTING_DATA, json);
 await SetCacheTimestampAsync(CACHE_KEY_SCOUTING_DATA);
 System.Diagnostics.Debug.WriteLine($"[Cache] Cached {scoutingData.Count} scouting entries");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache scouting data: {ex.Message}");
 }
 }

 #endregion

 #region Team Metrics

 public async Task<TeamMetrics?> GetCachedTeamMetricsAsync(int teamId, int eventId)
 {
 try
 {
 var cacheKey = $"cache_team_metrics_{teamId}_{eventId}";
 var json = await GetStringFromCacheAsync(cacheKey);
 if (!string.IsNullOrEmpty(json))
 {
 var metrics = JsonSerializer.Deserialize<TeamMetrics>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(cacheKey);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Team metrics loaded from cache (age: {age.TotalHours:F1}h, team: {teamId}, event: {eventId})");
 }
 
 return metrics;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load team metrics: {ex.Message}");
 }
 return null;
 }

 public async Task CacheTeamMetricsAsync(int teamId, int eventId, TeamMetrics metrics)
 {
 try
 {
 var cacheKey = $"cache_team_metrics_{teamId}_{eventId}";
 var json = JsonSerializer.Serialize(metrics, _jsonOptions);
 await SaveStringToCacheAsync(cacheKey, json);
 await SetCacheTimestampAsync(cacheKey);
 System.Diagnostics.Debug.WriteLine($"[Cache] Cached team metrics (team: {teamId}, event: {eventId})");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache team metrics: {ex.Message}");
 }
 }

 #endregion

 #region Profile Picture

 public async Task<byte[]?> GetCachedProfilePictureAsync()
 {
 try
 {
 var b64 = await GetStringFromCacheAsync(CACHE_KEY_PROFILE_PICTURE);
 if (!string.IsNullOrEmpty(b64))
 {
 try
 {
 var bytes = Convert.FromBase64String(b64);
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_PROFILE_PICTURE);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Profile picture loaded from cache (age: {age.TotalHours:F1}h, bytes: {bytes.Length})");
 }
 return bytes;
 }
 catch (FormatException fex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Profile picture base64 decode failed: {fex.Message}");
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load profile picture: {ex.Message}");
 }
 return null;
 }

 public async Task CacheProfilePictureAsync(byte[] pictureBytes)
 {
 try
 {
 if (pictureBytes == null || pictureBytes.Length ==0) return;
 var b64 = Convert.ToBase64String(pictureBytes);
 await SaveStringToCacheAsync(CACHE_KEY_PROFILE_PICTURE, b64);
 await SetCacheTimestampAsync(CACHE_KEY_PROFILE_PICTURE);
 System.Diagnostics.Debug.WriteLine($"[Cache] Profile picture cached successfully ({pictureBytes.Length} bytes)");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache profile picture: {ex.Message}");
 }
 }

 #endregion

 #region Cache Management

 public async Task<DateTime?> GetCacheTimestampAsync(string key)
 {
 try
 {
 var timestampStr = await GetStringFromCacheAsync(key + TIMESTAMP_SUFFIX);
 if (!string.IsNullOrEmpty(timestampStr) && DateTime.TryParse(timestampStr, out var timestamp))
 {
 return timestamp;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to get timestamp for {key}: {ex.Message}");
 }
 return null;
 }

 private async Task SetCacheTimestampAsync(string key)
 {
 try
 {
 await SaveStringToCacheAsync(key + TIMESTAMP_SUFFIX, DateTime.UtcNow.ToString("O"));
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to set timestamp for {key}: {ex.Message}");
 }
 }

 public async Task<bool> IsCacheExpiredAsync(string key, TimeSpan maxAge)
 {
 var timestamp = await GetCacheTimestampAsync(key);
 if (!timestamp.HasValue)
 return true;

 var age = DateTime.UtcNow - timestamp.Value;
 return age > maxAge;
 }

 public async Task ClearAllCacheAsync()
 {
 try
 {
 System.Diagnostics.Debug.WriteLine("[Cache] Clearing all cached data...");
 
 // Clear all known cache keys
 var cacheKeys = new[]
 {
 CACHE_KEY_GAME_CONFIG,
 CACHE_KEY_PIT_CONFIG,
 CACHE_KEY_EVENTS,
 CACHE_KEY_TEAMS,
 CACHE_KEY_MATCHES,
 CACHE_KEY_SCOUTING_DATA,
 CACHE_KEY_AVAILABLE_METRICS,
 CACHE_KEY_PROFILE_PICTURE
 };

 foreach (var key in cacheKeys)
 {
 RemoveCacheKey(key);
 }

 // Also remove any per-event match files (cache_matches_event_{id}) and any other cache_*.json files
 try
 {
 var dir = FileSystem.AppDataDirectory;
 var files = Directory.GetFiles(dir, "cache_*.json");
 foreach (var f in files)
 {
 try { File.Delete(f); } catch { }
 }
 }
 catch { }

 System.Diagnostics.Debug.WriteLine("[Cache] All cache cleared successfully");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to clear cache: {ex.Message}");
 }
 }

 #endregion
}
