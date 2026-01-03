using ObsidianScout.Models;
using System.Text.Json;
using System.IO;
using Microsoft.Maui.Storage;
using System.Globalization;

namespace ObsidianScout.Services;

/// <summary>
/// Centralized service for offline data caching
/// Caches all essential data for complete offline functionality
/// </summary>
public interface ICacheService
{
 // Preload all data on app startup
 Task PreloadAllDataAsync();
 
 // Game Config - Active (for regular pages)
 Task<GameConfig?> GetCachedGameConfigAsync();
 Task CacheGameConfigAsync(GameConfig config);
 
 // Game Config - Team (explicit per-team config for editors)
 Task<GameConfig?> GetCachedTeamGameConfigAsync();
 Task CacheTeamGameConfigAsync(GameConfig config);
 
 // Pit Config - Active (for regular pages)
 Task<PitConfig?> GetCachedPitConfigAsync();
 Task CachePitConfigAsync(PitConfig config);
 
 // Pit Config - Team (explicit per-team config for editors)
 Task<PitConfig?> GetCachedTeamPitConfigAsync();
 Task CacheTeamPitConfigAsync(PitConfig config);
 
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
    
    // Pit scouting cache
    Task<List<PitScoutingEntry>?> GetCachedPitScoutingDataAsync();
    Task CachePitScoutingDataAsync(List<PitScoutingEntry> pitData);

    // Pending uploads (history/offline queue)
    Task<List<ScoutingEntry>?> GetPendingScoutingAsync();
    Task AddPendingScoutingAsync(ScoutingEntry entry);
    Task RemovePendingScoutingAsync(Func<ScoutingEntry, bool> predicate);


    Task<List<PitScoutingEntry>?> GetPendingPitAsync();
    Task AddPendingPitAsync(PitScoutingEntry entry);
    Task RemovePendingPitAsync(Func<PitScoutingEntry, bool> predicate);

    
 
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
 Task<DateTime?> GetCacheCreatedAsync(string key);
 Task<DateTime?> GetCacheLastUpdatedAsync(string key);
 Task ClearAllCacheAsync();
 Task<bool> IsCacheExpiredAsync(string key, TimeSpan maxAge);
 Task<bool> HasCachedDataAsync();
}
public class CacheService : ICacheService
{
 private const string CACHE_KEY_GAME_CONFIG = "cache_game_config";
 private const string CACHE_KEY_TEAM_GAME_CONFIG = "cache_team_game_config";
 private const string CACHE_KEY_PIT_CONFIG = "cache_pit_config";
 private const string CACHE_KEY_TEAM_PIT_CONFIG = "cache_team_pit_config";
 private const string CACHE_KEY_EVENTS = "cache_events";
 private const string CACHE_KEY_TEAMS = "cache_teams";
 private const string CACHE_KEY_MATCHES = "cache_matches";
 private const string CACHE_KEY_SCOUTING_DATA = "cache_scouting_data";
    private const string CACHE_KEY_PENDING_SCOUTING = "cache_pending_scouting";
 private const string CACHE_KEY_AVAILABLE_METRICS = "cache_available_metrics";
 private const string CACHE_KEY_LAST_PRELOAD = "cache_last_preload";
 private const string CACHE_KEY_PROFILE_PICTURE = "cache_profile_picture";
 
 private const string TIMESTAMP_SUFFIX = "_timestamp"; // legacy
 private const string CREATED_SUFFIX = "_created";
 private const string UPDATED_SUFFIX = "_updated";
 
 // Maximum approximate length (characters) we will attempt to store in SecureStorage
 // Windows application data container and many secure stores have small limits (~8KB).
 private const int SECURE_STORAGE_MAX_VALUE_LENGTH =8 *1024; //8 KB
 
 private readonly JsonSerializerOptions _jsonOptions;

 public CacheService()
 {
 _jsonOptions = new JsonSerializerOptions
 {
 PropertyNameCaseInsensitive = true,
 WriteIndented = false
 };
 }

    // Pit scouting cache (mirror of match scouting cache)
    private const string CACHE_KEY_PIT_SCOUTING_DATA = "cache_pit_scouting_data";

    public async Task<List<PitScoutingEntry>?> GetCachedPitScoutingDataAsync()
    {
        try
        {
            var json = await GetStringFromCacheAsync(CACHE_KEY_PIT_SCOUTING_DATA);
            if (!string.IsNullOrEmpty(json))
            {
                if (!JsonUtils.TryDeserialize<List<PitScoutingEntry>>(json, _jsonOptions, out var data, out var err))
                {
                    System.Diagnostics.Debug.WriteLine($"[Cache] Failed to parse cached pit scouting data: {err}");
                    return null;
                }

                var timestamp = await GetCacheTimestampAsync(CACHE_KEY_PIT_SCOUTING_DATA);
                if (timestamp.HasValue)
                {
                    var age = DateTime.UtcNow - timestamp.Value;
                    System.Diagnostics.Debug.WriteLine($"[Cache] Pit scouting data loaded from cache (age: {age.TotalHours:F1}h, count: {data?.Count ??0})");
                }

                return data;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load pit scouting data: {ex.Message}");
        }
        return null;
    }

    public async Task CachePitScoutingDataAsync(List<PitScoutingEntry> pitData)
    {
        try
        {
            var json = JsonSerializer.Serialize(pitData, _jsonOptions);
            await SaveStringToCacheAsync(CACHE_KEY_PIT_SCOUTING_DATA, json);
            await SetCacheTimestampAsync(CACHE_KEY_PIT_SCOUTING_DATA);
            System.Diagnostics.Debug.WriteLine($"[Cache] Cached {pitData.Count} pit scouting entries");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache pit scouting data: {ex.Message}");
        }
    }

    #region Pending Uploads

    public async Task<List<ScoutingEntry>?> GetPendingScoutingAsync()
    {
        try
        {
            var json = await GetStringFromCacheAsync(CACHE_KEY_PENDING_SCOUTING);
            if (!string.IsNullOrEmpty(json))
            {
                var list = JsonSerializer.Deserialize<List<ScoutingEntry>>(json, _jsonOptions);
                return list;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load pending scouting: {ex.Message}");
        }
        return new List<ScoutingEntry>();
    }

    public async Task AddPendingScoutingAsync(ScoutingEntry entry)
    {
        try
        {
            var list = await GetPendingScoutingAsync() ?? new List<ScoutingEntry>();
            // Remove duplicates: match by OfflineId if present, otherwise by Id or timestamp+team+match
            try
            {
                list = list.Where(e =>
                {
                    if (!string.IsNullOrEmpty(entry.OfflineId) && !string.IsNullOrEmpty(e.OfflineId))
                        return e.OfflineId != entry.OfflineId;
                    if (entry.Id > 0 && e.Id > 0)
                        return e.Id != entry.Id;
                    return !(e.Timestamp == entry.Timestamp && e.TeamId == entry.TeamId && e.MatchId == entry.MatchId);
                }).ToList();
            }
            catch { }

            list.Add(entry);
            var json = JsonSerializer.Serialize(list, _jsonOptions);
            await SaveStringToCacheAsync(CACHE_KEY_PENDING_SCOUTING, json);
            await SetCacheTimestampAsync(CACHE_KEY_PENDING_SCOUTING);
            System.Diagnostics.Debug.WriteLine("[Cache] Added pending scouting entry (deduped)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] Failed to add pending scouting: {ex.Message}");
        }
    }

    public async Task RemovePendingScoutingAsync(Func<ScoutingEntry, bool> predicate)
    {
        try
        {
            var list = await GetPendingScoutingAsync() ?? new List<ScoutingEntry>();
            var remaining = list.Where(e => !predicate(e)).ToList();
            var json = JsonSerializer.Serialize(remaining, _jsonOptions);
            await SaveStringToCacheAsync(CACHE_KEY_PENDING_SCOUTING, json);
            await SetCacheTimestampAsync(CACHE_KEY_PENDING_SCOUTING);
            System.Diagnostics.Debug.WriteLine("[Cache] Removed matching pending scouting entries");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] Failed to remove pending scouting: {ex.Message}");
        }
    }

    private const string CACHE_KEY_PENDING_PIT = "cache_pending_pit";

    public async Task<List<PitScoutingEntry>?> GetPendingPitAsync()
    {
        try
        {
            var json = await GetStringFromCacheAsync(CACHE_KEY_PENDING_PIT);
            if (!string.IsNullOrEmpty(json))
            {
                var list = JsonSerializer.Deserialize<List<PitScoutingEntry>>(json, _jsonOptions);
                return list;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load pending pit scouting: {ex.Message}");
        }
        return new List<PitScoutingEntry>();
    }

    public async Task AddPendingPitAsync(PitScoutingEntry entry)
    {
        try
        {
            var list = await GetPendingPitAsync() ?? new List<PitScoutingEntry>();
            list.Add(entry);
            var json = JsonSerializer.Serialize(list, _jsonOptions);
            await SaveStringToCacheAsync(CACHE_KEY_PENDING_PIT, json);
            await SetCacheTimestampAsync(CACHE_KEY_PENDING_PIT);
            System.Diagnostics.Debug.WriteLine("[Cache] Added pending pit entry");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] Failed to add pending pit: {ex.Message}");
        }
    }

    public async Task RemovePendingPitAsync(Func<PitScoutingEntry, bool> predicate)
    {
        try
        {
            var list = await GetPendingPitAsync() ?? new List<PitScoutingEntry>();
            var remaining = list.Where(e => !predicate(e)).ToList();
            var json = JsonSerializer.Serialize(remaining, _jsonOptions);
            await SaveStringToCacheAsync(CACHE_KEY_PENDING_PIT, json);
            await SetCacheTimestampAsync(CACHE_KEY_PENDING_PIT);
            System.Diagnostics.Debug.WriteLine("[Cache] Removed matching pending pit entries");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] Failed to remove pending pit: {ex.Message}");
        }
    }

    // Deleted items tracking implementations (persist markers so deletions survive restarts)
    public async Task<List<string>?> GetDeletedScoutingOfflineIdsAsync()
    {
        try
        {
            var json = await GetStringFromCacheAsync(CACHE_KEY_PENDING_SCOUTING + "_deleted_offline");
            if (!string.IsNullOrEmpty(json))
            {
                var list = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);
                return list;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] GetDeletedScoutingOfflineIdsAsync failed: {ex.Message}");
        }
        return new List<string>();
    }

    public async Task AddDeletedScoutingOfflineIdAsync(string offlineId)
    {
        try
        {
            var list = await GetDeletedScoutingOfflineIdsAsync() ?? new List<string>();
            if (!list.Contains(offlineId)) list.Add(offlineId);
            var json = JsonSerializer.Serialize(list, _jsonOptions);
            await SaveStringToCacheAsync(CACHE_KEY_PENDING_SCOUTING + "_deleted_offline", json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] AddDeletedScoutingOfflineIdAsync failed: {ex.Message}");
        }
    }

    public async Task<List<int>?> GetDeletedScoutingIdsAsync()
    {
        try
        {
            var json = await GetStringFromCacheAsync(CACHE_KEY_PENDING_SCOUTING + "_deleted_ids");
            if (!string.IsNullOrEmpty(json))
            {
                var list = JsonSerializer.Deserialize<List<int>>(json, _jsonOptions);
                return list;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] GetDeletedScoutingIdsAsync failed: {ex.Message}");
        }
        return new List<int>();
    }

    public async Task AddDeletedScoutingIdAsync(int id)
    {
        try
        {
            var list = await GetDeletedScoutingIdsAsync() ?? new List<int>();
            if (!list.Contains(id)) list.Add(id);
            var json = JsonSerializer.Serialize(list, _jsonOptions);
            await SaveStringToCacheAsync(CACHE_KEY_PENDING_SCOUTING + "_deleted_ids", json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] AddDeletedScoutingIdAsync failed: {ex.Message}");
        }
    }

    public async Task<List<int>?> GetDeletedPitIdsAsync()
    {
        try
        {
            var json = await GetStringFromCacheAsync(CACHE_KEY_PENDING_PIT + "_deleted_ids");
            if (!string.IsNullOrEmpty(json))
            {
                var list = JsonSerializer.Deserialize<List<int>>(json, _jsonOptions);
                return list;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] GetDeletedPitIdsAsync failed: {ex.Message}");
        }
        return new List<int>();
    }

    public async Task AddDeletedPitIdAsync(int id)
    {
        try
        {
            var list = await GetDeletedPitIdsAsync() ?? new List<int>();
            if (!list.Contains(id)) list.Add(id);
            var json = JsonSerializer.Serialize(list, _jsonOptions);
            await SaveStringToCacheAsync(CACHE_KEY_PENDING_PIT + "_deleted_ids", json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache] AddDeletedPitIdAsync failed: {ex.Message}");
        }
    }

    #endregion

 #region Storage helpers

 private string GetFilePathForKey(string key) => Path.Combine(FileSystem.AppDataDirectory, key + ".json");

 private async Task SaveStringToCacheAsync(string key, string value)
 {
 // Try SecureStorage first (encrypted), fallback to file storage for large values
 try
 {
 // If the value is too large, skip attempting SecureStorage to avoid platform exceptions
 if (!string.IsNullOrEmpty(value) && value.Length > SECURE_STORAGE_MAX_VALUE_LENGTH)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Value too large for SecureStorage for {key} ({value.Length} chars). Using file fallback.");
 }
 else
 {
 try
 {
 await SecureStorage.SetAsync(key, value);
 return;
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] SecureStorage write failed for {key}: {ex.Message}. Falling back to file.");
 }
 }
 }
 catch (Exception ex)
 {
 // Catching any unexpected exceptions interacting with SecureStorage
 System.Diagnostics.Debug.WriteLine($"[Cache] SecureStorage attempt error for {key}: {ex.Message}. Falling back to file.");
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

 // Remove legacy and new timestamp keys
 try { SecureStorage.Remove(key + TIMESTAMP_SUFFIX); } catch { }
 try { SecureStorage.Remove(key + CREATED_SUFFIX); } catch { }
 try { SecureStorage.Remove(key + UPDATED_SUFFIX); } catch { }
 try
 {
 var tsPath = GetFilePathForKey(key + TIMESTAMP_SUFFIX);
 if (File.Exists(tsPath)) File.Delete(tsPath);
 }
 catch { }
 try
 {
 var createdPath = GetFilePathForKey(key + CREATED_SUFFIX);
 if (File.Exists(createdPath)) File.Delete(createdPath);
 }
 catch { }
 try
 {
 var updatedPath = GetFilePathForKey(key + UPDATED_SUFFIX);
 if (File.Exists(updatedPath)) File.Delete(updatedPath);
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
 System.Diagnostics.Debug.WriteLine($"[Cache] Game config (active) loaded from cache (age: {age.TotalHours:F1}h)");
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
 System.Diagnostics.Debug.WriteLine("[Cache] Game config (active) cached successfully");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache game config: {ex.Message}");
 }
 }

 public async Task<GameConfig?> GetCachedTeamGameConfigAsync()
 {
 try
 {
 var json = await GetStringFromCacheAsync(CACHE_KEY_TEAM_GAME_CONFIG);
 if (!string.IsNullOrEmpty(json))
 {
 var config = JsonSerializer.Deserialize<GameConfig>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_TEAM_GAME_CONFIG);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Game config (team) loaded from cache (age: {age.TotalHours:F1}h)");
 }
 
 return config;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load team game config: {ex.Message}");
 }
 return null;
 }

 public async Task CacheTeamGameConfigAsync(GameConfig config)
 {
 try
 {
 var json = JsonSerializer.Serialize(config, _jsonOptions);
 await SaveStringToCacheAsync(CACHE_KEY_TEAM_GAME_CONFIG, json);
 await SetCacheTimestampAsync(CACHE_KEY_TEAM_GAME_CONFIG);
 System.Diagnostics.Debug.WriteLine("[Cache] Game config (team) cached successfully");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache team game config: {ex.Message}");
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
 System.Diagnostics.Debug.WriteLine($"[Cache] Pit config (active) loaded from cache (age: {age.TotalHours:F1}h)");
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
 System.Diagnostics.Debug.WriteLine("[Cache] Pit config (active) cached successfully");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache pit config: {ex.Message}");
 }
 }

 public async Task<PitConfig?> GetCachedTeamPitConfigAsync()
 {
 try
 {
 var json = await GetStringFromCacheAsync(CACHE_KEY_TEAM_PIT_CONFIG);
 if (!string.IsNullOrEmpty(json))
 {
 var config = JsonSerializer.Deserialize<PitConfig>(json, _jsonOptions);
 
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_TEAM_PIT_CONFIG);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Pit config (team) loaded from cache (age: {age.TotalHours:F1}h)");
 }
 
 return config;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to load team pit config: {ex.Message}");
 }
 return null;
 }

 public async Task CacheTeamPitConfigAsync(PitConfig config)
 {
 try
 {
 var json = JsonSerializer.Serialize(config, _jsonOptions);
 await SaveStringToCacheAsync(CACHE_KEY_TEAM_PIT_CONFIG, json);
 await SetCacheTimestampAsync(CACHE_KEY_TEAM_PIT_CONFIG);
 System.Diagnostics.Debug.WriteLine("[Cache] Pit config (team) cached successfully");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to cache team pit config: {ex.Message}");
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
  if (!JsonUtils.TryDeserialize<TeamMetrics>(json, _jsonOptions, out var metrics, out var _err2))
  {
      System.Diagnostics.Debug.WriteLine($"[Cache] Failed to parse cached team metrics");
      return null;
  }
 
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
 // Prefer binary file storage for profile pictures to avoid base64 expansion and secure storage limits
 var binPath = GetFilePathForKey(CACHE_KEY_PROFILE_PICTURE).Replace(".json", ".bin");
 if (File.Exists(binPath))
 {
 try
 {
 var bytes = await File.ReadAllBytesAsync(binPath);
 var timestamp = await GetCacheTimestampAsync(CACHE_KEY_PROFILE_PICTURE);
 if (timestamp.HasValue)
 {
 var age = DateTime.UtcNow - timestamp.Value;
 System.Diagnostics.Debug.WriteLine($"[Cache] Profile picture loaded from binary cache (age: {age.TotalHours:F1}h, bytes: {bytes.Length})");
 }
 return bytes;
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to read binary profile picture: {ex.Message}");
 }
 }

 // Fallback: legacy base64 string storage
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
 // If the image is reasonably large, write as binary file to avoid SecureStorage limits and base64 expansion
 var binPath = GetFilePathForKey(CACHE_KEY_PROFILE_PICTURE).Replace(".json", ".bin");
 try
 {
 await File.WriteAllBytesAsync(binPath, pictureBytes);
 System.Diagnostics.Debug.WriteLine($"[Cache] Wrote binary profile picture to file: {binPath}");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to write binary profile picture: {ex.Message}");
 // Fallback to base64 string storage if binary write fails
 var b64Fallback = Convert.ToBase64String(pictureBytes);
 await SaveStringToCacheAsync(CACHE_KEY_PROFILE_PICTURE, b64Fallback);
 System.Diagnostics.Debug.WriteLine($"[Cache] Wrote profile picture as base64 fallback (chars: {b64Fallback.Length})");
 }

 // Update timestamp metadata
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
 // Keep legacy method but route to last-updated semantics
 return await GetCacheLastUpdatedAsync(key);
 }

 public async Task<DateTime?> GetCacheCreatedAsync(string key)
 {
 try
 {
 var createdStr = await GetStringFromCacheAsync(key + CREATED_SUFFIX);
 if (!string.IsNullOrEmpty(createdStr))
 {
 // Parse ISO8601 round-trip format as UTC to avoid local timezone interpretation
 if (DateTime.TryParseExact(createdStr, "O", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var created))
 {
 return created;
 }
 // Fallback to permissive parse
 if (DateTime.TryParse(createdStr, null, DateTimeStyles.AdjustToUniversal, out created))
 {
 return created;
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to get created timestamp for {key}: {ex.Message}");
 }
 return null;
 }

 public async Task<DateTime?> GetCacheLastUpdatedAsync(string key)
 {
 try
 {
 // Prefer explicit updated key
 var updatedStr = await GetStringFromCacheAsync(key + UPDATED_SUFFIX);
 if (!string.IsNullOrEmpty(updatedStr))
 {
 if (DateTime.TryParseExact(updatedStr, "O", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var updated))
 {
 return updated;
 }
 if (DateTime.TryParse(updatedStr, null, DateTimeStyles.AdjustToUniversal, out updated))
 {
 return updated;
 }
 }

 // Fallback to legacy timestamp suffix for backwards compatibility
 var timestampStr = await GetStringFromCacheAsync(key + TIMESTAMP_SUFFIX);
 if (!string.IsNullOrEmpty(timestampStr))
 {
 if (DateTime.TryParseExact(timestampStr, "O", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var timestamp))
 {
 return timestamp;
 }
 if (DateTime.TryParse(timestampStr, null, DateTimeStyles.AdjustToUniversal, out timestamp))
 {
 return timestamp;
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Cache] Failed to get last-updated timestamp for {key}: {ex.Message}");
 }
 return null;
 }

 private async Task SetCacheTimestampAsync(string key)
 {
 try
 {
 var now = DateTime.UtcNow.ToString("O");

 // Write updated timestamp
 await SaveStringToCacheAsync(key + UPDATED_SUFFIX, now);

 // Maintain legacy timestamp key for any existing consumers
 await SaveStringToCacheAsync(key + TIMESTAMP_SUFFIX, now);

 // If no created timestamp exists, set it now (preserve original creation time)
 var created = await GetStringFromCacheAsync(key + CREATED_SUFFIX);
 if (string.IsNullOrEmpty(created))
 {
 await SaveStringToCacheAsync(key + CREATED_SUFFIX, now);
 }
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
 CACHE_KEY_TEAM_GAME_CONFIG,
 CACHE_KEY_PIT_CONFIG,
 CACHE_KEY_TEAM_PIT_CONFIG,
 CACHE_KEY_EVENTS,
 CACHE_KEY_TEAMS,
 CACHE_KEY_MATCHES,
 CACHE_KEY_SCOUTING_DATA,
 CACHE_KEY_AVAILABLE_METRICS,
 CACHE_KEY_PROFILE_PICTURE,
 // Also clear last preload markers
 CACHE_KEY_LAST_PRELOAD
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
