using ObsidianScout.Models;
using System.Net.Http.Json;

namespace ObsidianScout.Services;

/// <summary>
/// Partial class containing team config endpoints for config editors.
/// These endpoints use /config/*/team which returns the explicit per-team config
/// (as opposed to /config/* which may return active/alliance config).
/// </summary>
public partial class ApiService
{
    /// <summary>
    /// Gets the explicit per-team game config (for editors).
    /// Uses /config/game/team endpoint.
    /// </summary>
    public async Task<GameConfigResponse> GetTeamGameConfigAsync()
    {
      if (!await ShouldUseNetworkAsync())
{
      var cachedConfig = await _cache_service.GetCachedTeamGameConfigAsync();
            if (cachedConfig != null)
 {
     System.Diagnostics.Debug.WriteLine("[API] Offline-mode - returning cached team game config immediately");
                return new GameConfigResponse { Success = true, Config = cachedConfig, Error = "Using cached data (offline mode)" };
            }
        return new GameConfigResponse { Success = false, Error = "Offline - no cached team game config available" };
        }
  
        try
        {
      await AddAuthHeaderAsync();
      var baseUrl = await GetBaseUrlAsync();
     // Use /config/game/team endpoint for explicit per-team config (for editors)
var response = await _httpClient.GetAsync($"{baseUrl}/config/game/team");

            if (response.IsSuccessStatusCode)
   {
           var result = await response.Content.ReadFromJsonAsync<GameConfigResponse>(_jsonOptions);
       
     // Cache the team game config
       if (result != null && result.Success && result.Config != null)
     {
  await _cache_service.CacheTeamGameConfigAsync(result.Config);
              }
      
        return result ?? new GameConfigResponse { Success = false, Error = "Invalid response" };
  }

         // Try to load from cache on failure
            var cachedConfig2 = await _cache_service.GetCachedTeamGameConfigAsync();
          if (cachedConfig2 != null)
            {
       System.Diagnostics.Debug.WriteLine($"[API] Using cached team game config (server returned {response.StatusCode})");
           return new GameConfigResponse 
      { 
   Success = true, 
   Config = cachedConfig2,
         Error = $"Using cached data (server error: {response.StatusCode})"
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
 System.Diagnostics.Debug.WriteLine($"[API] Team game config request failed: {ex.Message}");

            var cachedConfig3 = await _cache_service.GetCachedTeamGameConfigAsync();
            if (cachedConfig3 != null)
         {
    System.Diagnostics.Debug.WriteLine($"[API] Using cached team game config after error (offline mode)");
           return new GameConfigResponse 
        { 
    Success = true, 
          Config = cachedConfig3,
     Error = $"Using cached data ({ex.Message})"
      };
}
   
    return new GameConfigResponse
      {
     Success = false,
      Error = $"Connection error: {ex.Message}"
   };
  }
    }

    /// <summary>
    /// Gets the explicit per-team pit config (for editors).
    /// Uses /config/pit/team endpoint.
    /// </summary>
 public async Task<PitConfigResponse> GetTeamPitConfigAsync()
    {
   if (!await ShouldUseNetworkAsync())
      {
       var cachedConfig = await _cache_service.GetCachedTeamPitConfigAsync();
      if (cachedConfig != null)
      {
        System.Diagnostics.Debug.WriteLine("[API] Offline-mode - returning cached team pit config immediately");
         return new PitConfigResponse { Success = true, Config = cachedConfig, Error = "Using cached data (offline mode)" };
   }
            return new PitConfigResponse { Success = false, Error = "Offline - no cached team pit config available" };
        }
    
        try
    {
            await AddAuthHeaderAsync();
    var baseUrl = await GetBaseUrlAsync();
            // Use /config/pit/team endpoint for explicit per-team config (for editors)
       var response = await _httpClient.GetAsync($"{baseUrl}/config/pit/team");

   if (response.IsSuccessStatusCode)
 {
         var rawJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine("=== RAW TEAM PIT CONFIG JSON ===");
         System.Diagnostics.Debug.WriteLine(rawJson.Length > 2000 ? rawJson.Substring(0, 2000) + "..." : rawJson);
   
 var result = await response.Content.ReadFromJsonAsync<PitConfigResponse>(_jsonOptions);
    
     // Cache the team pit config
         if (result != null && result.Success && result.Config != null)
           {
   await _cache_service.CacheTeamPitConfigAsync(result.Config);
    }
         
     return result ?? new PitConfigResponse { Success = false, Error = "Invalid response" };
            }

            // Try to load from cache on failure
 var cachedConfig2 = await _cache_service.GetCachedTeamPitConfigAsync();
            if (cachedConfig2 != null)
            {
       System.Diagnostics.Debug.WriteLine($"[API] Using cached team pit config (server returned {response.StatusCode})");
          return new PitConfigResponse 
                { 
                Success = true, 
              Config = cachedConfig2,
      Error = $"Using cached data (server error: {response.StatusCode})"
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
         System.Diagnostics.Debug.WriteLine($"[API] Team pit config request failed: {ex.Message}");

         var cachedConfig3 = await _cache_service.GetCachedTeamPitConfigAsync();
            if (cachedConfig3 != null)
       {
                System.Diagnostics.Debug.WriteLine($"[API] Using cached team pit config after error (offline mode)");
         return new PitConfigResponse 
          { 
   Success = true, 
           Config = cachedConfig3,
        Error = $"Using cached data ({ex.Message})"
 };
         }
 
            return new PitConfigResponse
 {
                Success = false,
   Error = $"Connection error: {ex.Message}"
        };
      }
  }
}
