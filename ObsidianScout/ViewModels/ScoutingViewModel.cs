using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace ObsidianScout.ViewModels;

public partial class ScoutingViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly IQRCodeService _qrCodeService;
    private readonly ISettingsService _settingsService;
    private System.Threading.Timer? _refreshTimer;

    [ObservableProperty]
    private int teamId;

    [ObservableProperty]
    private int matchId;

    [ObservableProperty]
    private Team? selectedTeam;

    [ObservableProperty]
    private Match? selectedMatch;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private GameConfig? gameConfig;

    [ObservableProperty]
    private ImageSource? qrCodeImage;

    [ObservableProperty]
    private bool isQRCodeVisible;

    [ObservableProperty]
    private string scoutName = string.Empty;

    [ObservableProperty]
    private bool isOfflineMode;

    [ObservableProperty]
    private DateTime? lastRefresh;
    
    // Points display properties
    [ObservableProperty]
    private int totalPoints;

    [ObservableProperty]
    private int autoPoints;

    [ObservableProperty]
    private int teleopPoints;

    [ObservableProperty]
    private int endgamePoints;

    // Store dynamic field values
    private Dictionary<string, object?> fieldValues = new();
    
    // Cache keys
    private const string CACHE_KEY_GAME_CONFIG = "cached_game_config";
    private const string CACHE_KEY_CONFIG_TIMESTAMP = "cached_config_timestamp";
    private const string CACHE_KEY_TEAMS = "cached_teams";
    private const string CACHE_KEY_TEAMS_TIMESTAMP = "cached_teams_timestamp";
    private const string CACHE_KEY_MATCHES = "cached_matches";
    private const string CACHE_KEY_MATCHES_TIMESTAMP = "cached_matches_timestamp";

    public ObservableCollection<Team> Teams { get; } = new();
    public ObservableCollection<Match> Matches { get; } = new();
    public ObservableCollection<ScoringElement> AutoElements { get; } = new();
    public ObservableCollection<ScoringElement> TeleopElements { get; } = new();
    public ObservableCollection<ScoringElement> EndgameElements { get; } = new();
    public ObservableCollection<RatingElement> RatingElements { get; } = new();
    public ObservableCollection<TextElement> TextElements { get; } = new();

    public ScoutingViewModel(IApiService apiService, IQRCodeService qrCodeService, ISettingsService settingsService)
    {
        _apiService = apiService;
        _qrCodeService = qrCodeService;
        _settingsService = settingsService;
        
        // Load initial data
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Load game config
        LoadGameConfigAsync();
        
        // Load teams and wait for them
        await LoadTeamsAsync();
        
        // Auto-fill scout name from logged-in username
        await LoadScoutNameAsync();
        
        // Auto-load matches now that teams are loaded
        await AutoLoadMatchesAsync(silent: false);
        
        // Start periodic refresh (every 60 seconds)
        StartPeriodicRefresh();
    }

    private void StartPeriodicRefresh()
    {
        // Refresh every 120 seconds (reduced from 60) - runs OFF UI thread to prevent lag
        _refreshTimer = new System.Threading.Timer(
            async _ =>
            {
                // Run refresh on background thread to avoid UI freezes
                await Task.Run(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("=== BACKGROUND REFRESH (Off UI Thread) ===");
                        
                        // Refresh teams silently
                        await LoadTeamsAsync(silent: true);
                        
                        // Refresh matches if we have an event
                        if (GameConfig != null && !string.IsNullOrEmpty(GameConfig.CurrentEventCode))
    {
      await AutoLoadMatchesAsync(silent: true);
             }
         
        // Update timestamp on UI thread
         await MainThread.InvokeOnMainThreadAsync(() =>
                   {
    LastRefresh = DateTime.Now;
            });
                         
                        System.Diagnostics.Debug.WriteLine($"✓ Background refresh completed at {DateTime.Now:HH:mm:ss}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Background refresh failed: {ex.Message}");
                    }
                });
            },
            null,
        TimeSpan.FromSeconds(120),  // Initial delay - increased from 60s
            TimeSpan.FromSeconds(120)); // Repeat interval - increased from 60s
    }

    private async Task RefreshDataInBackground()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== BACKGROUND REFRESH ===");
            
            // Refresh teams silently
            await LoadTeamsAsync(silent: true);
            
            // Refresh matches if we have an event
            if (GameConfig != null && !string.IsNullOrEmpty(GameConfig.CurrentEventCode))
            {
                await AutoLoadMatchesAsync(silent: true);
            }
            
            LastRefresh = DateTime.Now;
            System.Diagnostics.Debug.WriteLine($"✓ Background refresh completed at {LastRefresh:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Background refresh failed: {ex.Message}");
        }
    }

    public async Task InitialLoadAsync()
    {
        // This method is called when the page appears
        System.Diagnostics.Debug.WriteLine("=== INITIAL LOAD ON PAGE APPEAR ===");
        
        // Load matches automatically if we have config
        if (GameConfig != null && !string.IsNullOrEmpty(GameConfig.CurrentEventCode))
        {
            await AutoLoadMatchesAsync(silent: false);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("⚠️ Cannot auto-load matches: Missing config or event code");
        }
    }

    private async Task LoadScoutNameAsync()
    {
        try
        {
            var username = await _settingsService.GetUsernameAsync();
            if (!string.IsNullOrEmpty(username))
            {
                ScoutName = username;
                System.Diagnostics.Debug.WriteLine($"✓ Auto-filled scout name: {username}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load scout name: {ex.Message}");
        }
    }

    partial void OnSelectedTeamChanged(Team? value)
    {
        if (value != null)
        {
            TeamId = value.Id;
            // Auto-load matches when team is selected
            _ = AutoLoadMatchesAsync(silent: false);
        }
    }

    partial void OnSelectedMatchChanged(Match? value)
    {
        if (value != null)
        {
            MatchId = value.Id;
        }
    }

    private async void LoadGameConfigAsync()
    {
        IsLoading = true;
        try
        {
            var response = await _apiService.GetGameConfigAsync();
            if (response.Success && response.Config != null)
            {
                GameConfig = response.Config;
                LoadScoringElements();
                InitializeFieldValues();
                
                // Check if we're using cached data
                if (!string.IsNullOrEmpty(response.Error) && response.Error.Contains("offline"))
                {
                    IsOfflineMode = true;
                    StatusMessage = "⚠️ Using cached game config (offline mode)";
                    System.Diagnostics.Debug.WriteLine("✓ Game config loaded from cache (offline)");
                    
                    await Task.Delay(3000);
                    if (StatusMessage == "⚠️ Using cached game config (offline mode)")
                    {
                        StatusMessage = string.Empty;
                    }
                }
                else
                {
                    IsOfflineMode = false;
                    System.Diagnostics.Debug.WriteLine("✓ Game config loaded from server");
                }
            }
            else
            {
                StatusMessage = $"Failed to load game config: {response.Error}";
                System.Diagnostics.Debug.WriteLine($"✗ Failed to load config: {response.Error}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading config: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"✗ Exception loading config: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CacheGameConfigAsync(GameConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config);
            await SecureStorage.SetAsync(CACHE_KEY_GAME_CONFIG, json);
            await SecureStorage.SetAsync(CACHE_KEY_CONFIG_TIMESTAMP, DateTime.UtcNow.ToString("O"));
            System.Diagnostics.Debug.WriteLine("✓ Game config cached successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cache game config: {ex.Message}");
        }
    }

    private async Task<GameConfig?> LoadCachedGameConfigAsync()
    {
        try
        {
            var json = await SecureStorage.GetAsync(CACHE_KEY_GAME_CONFIG);
            if (!string.IsNullOrEmpty(json))
            {
                var config = JsonSerializer.Deserialize<GameConfig>(json);
                
                // Check cache age
                var timestampStr = await SecureStorage.GetAsync(CACHE_KEY_CONFIG_TIMESTAMP);
                if (!string.IsNullOrEmpty(timestampStr) && DateTime.TryParse(timestampStr, out var timestamp))
                {
                    var age = DateTime.UtcNow - timestamp;
                    System.Diagnostics.Debug.WriteLine($"Cached config age: {age.TotalHours:F1} hours");
                }
                
                return config;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load cached game config: {ex.Message}");
        }
        return null;
    }

    private async Task CacheTeamsAsync(List<Team> teams)
    {
        try
        {
            var json = JsonSerializer.Serialize(teams);
            await SecureStorage.SetAsync(CACHE_KEY_TEAMS, json);
            await SecureStorage.SetAsync(CACHE_KEY_TEAMS_TIMESTAMP, DateTime.UtcNow.ToString("O"));
            System.Diagnostics.Debug.WriteLine($"✓ Cached {teams.Count} teams");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cache teams: {ex.Message}");
        }
    }

    private async Task<List<Team>?> LoadCachedTeamsAsync()
    {
        try
        {
            var json = await SecureStorage.GetAsync(CACHE_KEY_TEAMS);
            if (!string.IsNullOrEmpty(json))
            {
                var teams = JsonSerializer.Deserialize<List<Team>>(json);
                
                // Check cache age
                var timestampStr = await SecureStorage.GetAsync(CACHE_KEY_TEAMS_TIMESTAMP);
                if (!string.IsNullOrEmpty(timestampStr) && DateTime.TryParse(timestampStr, out var timestamp))
                {
                    var age = DateTime.UtcNow - timestamp;
                    System.Diagnostics.Debug.WriteLine($"Cached teams age: {age.TotalHours:F1} hours");
                }
                
                return teams;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load cached teams: {ex.Message}");
        }
        return null;
    }

    private async Task CacheMatchesAsync(List<Match> matches)
    {
        try
        {
            var json = JsonSerializer.Serialize(matches);
            await SecureStorage.SetAsync(CACHE_KEY_MATCHES, json);
            await SecureStorage.SetAsync(CACHE_KEY_MATCHES_TIMESTAMP, DateTime.UtcNow.ToString("O"));
            System.Diagnostics.Debug.WriteLine($"✓ Cached {matches.Count} matches");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cache matches: {ex.Message}");
        }
    }

    private async Task<List<Match>?> LoadCachedMatchesAsync()
    {
        try
        {
            var json = await SecureStorage.GetAsync(CACHE_KEY_MATCHES);
            if (!string.IsNullOrEmpty(json))
            {
                var matches = JsonSerializer.Deserialize<List<Match>>(json);
                
                // Check cache age
                var timestampStr = await SecureStorage.GetAsync(CACHE_KEY_MATCHES_TIMESTAMP);
                if (!string.IsNullOrEmpty(timestampStr) && DateTime.TryParse(timestampStr, out var timestamp))
                {
                    var age = DateTime.UtcNow - timestamp;
                    System.Diagnostics.Debug.WriteLine($"Cached matches age: {age.TotalHours:F1} hours");
                }
                
                return matches;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load cached matches: {ex.Message}");
        }
        return null;
    }

    private async Task LoadTeamsAsync(bool silent = false)
    {
        try
        {
            if (!silent)
            {
                StatusMessage = "Loading teams...";
            }

            var response = await _apiService.GetTeamsAsync(limit: 500);
            
            if (response.Success && response.Teams != null && response.Teams.Count > 0)
            {
                Teams.Clear();
                foreach (var team in response.Teams.OrderBy(t => t.TeamNumber))
                {
                    Teams.Add(team);
                }
                
                // Check if we're using cached data
                if (!string.IsNullOrEmpty(response.Error) && response.Error.Contains("offline"))
                {
                    IsOfflineMode = true;
                    if (!silent)
                    {
                        StatusMessage = $"⚠️ Loaded {Teams.Count} cached teams (offline)";
                        await Task.Delay(2000);
                        if (StatusMessage.StartsWith("⚠️"))
                        {
                            StatusMessage = string.Empty;
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {Teams.Count} teams from cache");
                }
                else
                {
                    IsOfflineMode = false;
                    if (!silent)
                    {
                        StatusMessage = $"Loaded {Teams.Count} teams";
                        await Task.Delay(2000);
                        if (StatusMessage == $"Loaded {Teams.Count} teams")
                        {
                            StatusMessage = string.Empty;
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {Teams.Count} teams from server");
                }
            }
            else
            {
                if (!silent)
                {
                    StatusMessage = response.Success 
                        ? "No teams found" 
                        : "Failed to load teams";
                }
                IsOfflineMode = true;
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                StatusMessage = $"Error loading teams: {ex.Message}";
            }
            IsOfflineMode = true;
            System.Diagnostics.Debug.WriteLine($"✗ Error loading teams: {ex.Message}");
        }
    }

    private async Task AutoLoadMatchesAsync(bool silent = false)
    {
        System.Diagnostics.Debug.WriteLine("=== AUTO-LOADING MATCHES ===");
        
        try
        {
            // First check if we have game config
            if (GameConfig == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Cannot auto-load matches: No game config");
                return;
            }

            // Check if we have an event code
            if (string.IsNullOrEmpty(GameConfig.CurrentEventCode))
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Cannot auto-load matches: No event code in config");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Looking for event: {GameConfig.CurrentEventCode}");

            var eventsResponse = await _apiService.GetEventsAsync();
            
            if (eventsResponse.Success && eventsResponse.Events != null && eventsResponse.Events.Count > 0)
            {
                var currentEvent = eventsResponse.Events
                    .FirstOrDefault(e => e.Code.Equals(GameConfig.CurrentEventCode, StringComparison.OrdinalIgnoreCase));
                
                if (currentEvent != null)
                {
                    var matchesResponse = await _apiService.GetMatchesAsync(currentEvent.Id);
                    
                    if (matchesResponse.Success && matchesResponse.Matches != null && matchesResponse.Matches.Count > 0)
                    {
                        Matches.Clear();
                        foreach (var match in matchesResponse.Matches
                            .OrderBy(m => m.MatchType)
                            .ThenBy(m => m.MatchNumber))
                        {
                            Matches.Add(match);
                        }
                        
                        // Check if we're using cached data
                        if (!string.IsNullOrEmpty(matchesResponse.Error) && matchesResponse.Error.Contains("offline"))
                        {
                            IsOfflineMode = true;
                            System.Diagnostics.Debug.WriteLine($"✓ Auto-loaded {Matches.Count} matches from cache (offline)");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"✓ Auto-loaded {Matches.Count} matches from server");
                        }
                        return;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("⚠️ No matches available");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during auto-load: {ex.Message}");
        }
        finally
        {
            System.Diagnostics.Debug.WriteLine("=== END AUTO-LOAD MATCHES ===\n");
        }
    }

    [RelayCommand]
    private async Task LoadMatchesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading matches...";
        
        try
        {
            // First check if we have game config
            if (GameConfig == null)
            {
                StatusMessage = "❌ Game configuration not loaded";
                return;
            }

            // Check if we have an event code
            if (string.IsNullOrEmpty(GameConfig.CurrentEventCode))
            {
                StatusMessage = "❌ No event selected in game configuration";
                return;
            }

            StatusMessage = $"🔍 Looking for event: {GameConfig.CurrentEventCode}";

            // Get events to find the current event ID
            var eventsResponse = await _apiService.GetEventsAsync();
            
            if (!eventsResponse.Success)
            {
                StatusMessage = $"❌ Failed to load events from server. Error: {eventsResponse.Error ?? "Unknown error"}";
                IsOfflineMode = true;
                return;
            }

            if (eventsResponse.Events == null || eventsResponse.Events.Count == 0)
            {
                StatusMessage = "❌ No events found on server";
                return;
            }

            StatusMessage = $"🔍 Searching {eventsResponse.Events.Count} events...";

            // Find event by code (case-insensitive)
            var currentEvent = eventsResponse.Events
                .FirstOrDefault(e => e.Code.Equals(GameConfig.CurrentEventCode, StringComparison.OrdinalIgnoreCase));
            
            if (currentEvent == null)
            {
                var availableEvents = string.Join(", ", eventsResponse.Events.Select(e => $"'{e.Code}'"));
                StatusMessage = $"❌ Event '{GameConfig.CurrentEventCode}' not found. Available: {availableEvents}";
                return;
            }

            StatusMessage = $"📡 Loading matches for {currentEvent.Name} (ID: {currentEvent.Id})...";

            // Load matches for the event
            var matchesResponse = await _apiService.GetMatchesAsync(currentEvent.Id);
            
            if (!matchesResponse.Success)
            {
                StatusMessage = $"❌ Failed to load matches for {currentEvent.Name}. Check server logs.";
                IsOfflineMode = true;
                return;
            }

            if (matchesResponse.Matches == null || matchesResponse.Matches.Count == 0)
            {
                StatusMessage = $"⚠️ No matches found for {currentEvent.Name}. Import matches for this event.";
                Matches.Clear();
                return;
            }

            // Populate matches collection
            Matches.Clear();
            foreach (var match in matchesResponse.Matches
                .OrderBy(m => m.MatchType)
                .ThenBy(m => m.MatchNumber))
            {
                Matches.Add(match);
            }

            // Check if we're using cached data
            if (!string.IsNullOrEmpty(matchesResponse.Error) && matchesResponse.Error.Contains("offline"))
            {
                IsOfflineMode = true;
                StatusMessage = $"⚠️ Loaded {Matches.Count} cached matches for {currentEvent.Name} (offline)";
            }
            else
            {
                StatusMessage = $"✓ Loaded {Matches.Count} matches for {currentEvent.Name}";
            }
            
            // Clear status message after 3 seconds
            await Task.Delay(3000);
            if (StatusMessage.StartsWith("✓") || StatusMessage.StartsWith("⚠️"))
            {
                StatusMessage = string.Empty;
            }
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = $"❌ Network error: {httpEx.Message}";
            IsOfflineMode = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
            IsOfflineMode = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadScoringElements()
    {
        if (GameConfig == null) return;

        AutoElements.Clear();
        TeleopElements.Clear();
        EndgameElements.Clear();
        RatingElements.Clear();
        TextElements.Clear();

        if (GameConfig.AutoPeriod?.ScoringElements != null)
        {
            foreach (var element in GameConfig.AutoPeriod.ScoringElements)
            {
                AutoElements.Add(element);
            }
        }

        if (GameConfig.TeleopPeriod?.ScoringElements != null)
        {
            foreach (var element in GameConfig.TeleopPeriod.ScoringElements)
            {
                TeleopElements.Add(element);
            }
        }

        if (GameConfig.EndgamePeriod?.ScoringElements != null)
        {
            foreach (var element in GameConfig.EndgamePeriod.ScoringElements)
            {
                EndgameElements.Add(element);
            }
        }

        if (GameConfig.PostMatch?.RatingElements != null)
        {
            foreach (var element in GameConfig.PostMatch.RatingElements)
            {
                RatingElements.Add(element);
            }
        }

        if (GameConfig.PostMatch?.TextElements != null)
        {
            foreach (var element in GameConfig.PostMatch.TextElements)
            {
                TextElements.Add(element);
            }
        }
    }

    private void InitializeFieldValues()
    {
        fieldValues.Clear();

        // Initialize all fields with their default values
        foreach (var element in AutoElements)
        {
            fieldValues[element.Id] = element.Default ?? GetDefaultForType(element.Type);
        }

        foreach (var element in TeleopElements)
        {
            fieldValues[element.Id] = element.Default ?? GetDefaultForType(element.Type);
        }

        foreach (var element in EndgameElements)
        {
            fieldValues[element.Id] = element.Default ?? GetDefaultForType(element.Type);
        }

        foreach (var element in RatingElements)
        {
            fieldValues[element.Id] = element.Default;
        }

        foreach (var element in TextElements)
        {
            fieldValues[element.Id] = string.Empty;
        }
        
        // Calculate initial points (all zeros or defaults)
        CalculatePoints();
    }

    private object GetDefaultForType(string type)
    {
        return type.ToLower() switch
        {
            "counter" => 0,
            "boolean" => false,
            "multiple_choice" => string.Empty,
            "text" => string.Empty,
            "number" => 0,
            _ => 0
        };
    }

    // Helper method to safely convert values, handling JsonElement
    private object? ConvertValueForSerialization(object? value)
    {
        if (value == null) return null;

        // Handle JsonElement
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => jsonElement.TryGetInt32(out var intVal) ? intVal : jsonElement.GetDouble(),
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => jsonElement.ToString()
            };
        }

        // Return value as-is if it's already a simple type
        return value;
    }

    // Helper method to safely convert to int, handling JsonElement
    private int SafeConvertToInt(object? value)
    {
        if (value == null) return 0;

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
            {
                return jsonElement.TryGetInt32(out var intVal) ? intVal : (int)jsonElement.GetDouble();
            }
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                return int.TryParse(jsonElement.GetString(), out var intVal) ? intVal : 0;
            }
            return 0;
        }

        if (value is int intValue) return intValue;
        if (value is string strValue) return int.TryParse(strValue, out var parsed) ? parsed : 0;
        
        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return 0;
        }
    }

    // Helper method to safely convert to bool, handling JsonElement
    private bool SafeConvertToBool(object? value)
    {
        if (value == null) return false;

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.True) return true;
            if (jsonElement.ValueKind == JsonValueKind.False) return false;
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                return bool.TryParse(jsonElement.GetString(), out var boolVal) && boolVal;
            }
            return false;
        }

        if (value is bool boolValue) return boolValue;
        if (value is string strValue) return bool.TryParse(strValue, out var parsed) && parsed;
        
        try
        {
            return Convert.ToBoolean(value);
        }
        catch
        {
            return false;
        }
    }

    // Helper method to safely convert to string, handling JsonElement
    private string SafeConvertToString(object? value)
    {
        if (value == null) return string.Empty;

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                return jsonElement.GetString() ?? string.Empty;
            }
            return jsonElement.ToString() ?? string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }

    public object? GetFieldValue(string fieldId)
    {
        return fieldValues.TryGetValue(fieldId, out var value) ? value : null;
    }

    public void SetFieldValue(string fieldId, object? value)
    {
        fieldValues[fieldId] = value;
        // Notify that field values changed
        OnPropertyChanged("FieldValuesChanged");
     
        // Recalculate points
        CalculatePoints();
    }

    public void IncrementCounter(string fieldId)
    {
        if (fieldValues.TryGetValue(fieldId, out var value))
        {
            var intValue = value switch
            {
                int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => 0
            };

 // Find the element to check max value
   var element = AutoElements.FirstOrDefault(e => e.Id == fieldId)
              ?? TeleopElements.FirstOrDefault(e => e.Id == fieldId)
         ?? EndgameElements.FirstOrDefault(e => e.Id == fieldId);

     if (element != null && element.Max.HasValue && intValue >= element.Max.Value)
           return;

       fieldValues[fieldId] = intValue + 1;
     OnPropertyChanged("FieldValuesChanged");
    
            // Recalculate points
     CalculatePoints();
        }
    }

    public void DecrementCounter(string fieldId)
    {
        if (fieldValues.TryGetValue(fieldId, out var value))
        {
var intValue = value switch
    {
      int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
       _ => 0
  };

   // Find the element to check min value
      var element = AutoElements.FirstOrDefault(e => e.Id == fieldId)
            ?? TeleopElements.FirstOrDefault(e => e.Id == fieldId)
         ?? EndgameElements.FirstOrDefault(e => e.Id == fieldId);

        var minValue = element?.Min ?? 0;
   if (intValue > minValue)
            {
        fieldValues[fieldId] = intValue - 1;
          OnPropertyChanged("FieldValuesChanged");
 
             // Recalculate points
             CalculatePoints();
    }
        }
    }
  
    private void CalculatePoints()
    {
        if (GameConfig == null) return;

        double auto = 0, teleop = 0, endgame = 0;

  // Calculate auto points
        if (GameConfig.AutoPeriod?.ScoringElements != null)
        {
            foreach (var element in GameConfig.AutoPeriod.ScoringElements)
     {
             if (fieldValues.TryGetValue(element.Id, out var value))
       {
        if (element.Type == "counter" || element.Type == "number")
   {
  var count = SafeConvertToInt(value);
          auto += count * element.Points;
             }
         else if (element.Type == "boolean" && SafeConvertToBool(value))
        {
        auto += element.Points;
        }
    }
     }
        }

        // Calculate teleop points
        if (GameConfig.TeleopPeriod?.ScoringElements != null)
   {
            foreach (var element in GameConfig.TeleopPeriod.ScoringElements)
{
                if (fieldValues.TryGetValue(element.Id, out var value))
      {
             if (element.Type == "counter" || element.Type == "number")
    {
    var count = SafeConvertToInt(value);
      teleop += count * element.Points;
        }
 else if (element.Type == "boolean" && SafeConvertToBool(value))
    {
        teleop += element.Points;
      }
     }
        }
    }

   // Calculate endgame points
        if (GameConfig.EndgamePeriod?.ScoringElements != null)
        {
            foreach (var element in GameConfig.EndgamePeriod.ScoringElements)
      {
       if (fieldValues.TryGetValue(element.Id, out var value))
     {
   if (element.Type == "counter" || element.Type == "number")
         {
          var count = SafeConvertToInt(value);
   endgame += count * element.Points;
        }
     else if (element.Type == "boolean" && SafeConvertToBool(value))
     {
     endgame += element.Points;
        }
           else if (element.Type == "multiple_choice" && element.Options != null)
         {
                 var valueStr = SafeConvertToString(value);
          var selectedOption = element.Options.FirstOrDefault(o => o.Name == valueStr);
if (selectedOption != null)
       {
      endgame += selectedOption.Points;
            }
     }
      }
       }
        }

        // Update properties
  AutoPoints = (int)auto;
        TeleopPoints = (int)teleop;
    EndgamePoints = (int)endgame;
        TotalPoints = AutoPoints + TeleopPoints + EndgamePoints;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
 // Validate inputs first
   if (TeamId <= 0 || MatchId <= 0)
        {
            StatusMessage = "Please select a team and match";
            return;
        }

   if (SelectedTeam == null)
        {
  StatusMessage = "Please select a valid team";
  return;
        }

     if (SelectedMatch == null)
        {
    StatusMessage = "Please select a valid match";
     return;
        }

        IsLoading = true;
        StatusMessage = "Submitting...";

        try
        {
            // Convert all field values to simple types (handle JsonElement)
       var convertedData = new Dictionary<string, object?>();
    foreach (var kvp in fieldValues)
     {
      var converted = ConvertValueForSerialization(kvp.Value);
        convertedData[kvp.Key] = converted;
            }

         // Add scout_name to the data as required by the API
     if (!string.IsNullOrEmpty(ScoutName))
        {
  convertedData["scout_name"] = ScoutName;
    }

        var submission = new ScoutingSubmission
    {
           TeamId = TeamId,
     MatchId = MatchId,
     Data = convertedData
     };

       var result = await _apiService.SubmitScoutingDataAsync(submission);

            if (result.Success)
   {
     StatusMessage = "✓ Scouting data submitted successfully!";
     
       await Task.Delay(3000);
  if (StatusMessage == "✓ Scouting data submitted successfully!")
                {
            StatusMessage = string.Empty;
    ResetForm();
       }
            }
   else
{
     StatusMessage = $"✗ {result.Error}";
       }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Error: {ex.Message}";
        }
        finally
        {
         IsLoading = false;
 }
    }

    [RelayCommand]
    private async Task SaveWithQRCodeAsync()
    {
        if (SelectedTeam == null || SelectedMatch == null)
    {
            StatusMessage = "❌ Please select both a team and a match";
  return;
        }

 // Capture current selections to ensure UI pickers remain selected while QR overlay is visible
 var preservedTeam = SelectedTeam;
 var preservedMatch = SelectedMatch;

        TeamId = SelectedTeam.Id;
 MatchId = SelectedMatch.Id;
        
  if (TeamId <= 0 || MatchId <= 0)
        {
            StatusMessage = "❌ Invalid team or match selection";
      return;
}

 IsLoading = true;
        StatusMessage = "Generating QR Code...";

        try
        {
    var qrData = new Dictionary<string, object?>
     {
  ["team_id"] = TeamId,
           ["team_number"] = SelectedTeam.TeamNumber,
     ["match_id"] = MatchId,
                ["match_number"] = SelectedMatch.MatchNumber,
      ["alliance"] = "unknown",
            ["scout_name"] = ScoutName
        };

foreach (var kvp in fieldValues)
   {
       qrData[kvp.Key] = ConvertValueForSerialization(kvp.Value);
         }

        if (GameConfig != null)
      {
    qrData["auto_period_timer_enabled"] = false;
 qrData["auto_points_points"] = AutoPoints;
      qrData["teleop_points_points"] = TeleopPoints;
           qrData["endgame_points_points"] = EndgamePoints;
     qrData["total_points_points"] = TotalPoints;
    }

       // Add metadata
 qrData["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
qrData["offline_generated"] = true;

            System.Diagnostics.Debug.WriteLine($"Generating QR code with {qrData.Count} fields");

     // Serialize to JSON
      var jsonData = _qrCodeService.SerializeScoutingData(qrData);
            QrCodeImage = _qrCodeService.GenerateQRCode(jsonData);

 // Show overlay but preserve selected items explicitly
 IsQRCodeVisible = true;
 StatusMessage = string.Empty;

 // Re-assign preserved selections to ensure pickers keep their values
 SelectedTeam = preservedTeam;
 SelectedMatch = preservedMatch;
 }
        catch (Exception ex)
   {
         StatusMessage = $"✗ Error generating QR code: {ex.Message}";
        }
        finally
      {
            IsLoading = false;
     }
    }

    [RelayCommand]
    private void CloseQRCode()
  {
        IsQRCodeVisible = false;
        QrCodeImage = null;
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (SelectedTeam == null || SelectedMatch == null)
     {
            StatusMessage = "❌ Please select both a team and a match";
       return;
        }

    TeamId = SelectedTeam.Id;
        MatchId = SelectedMatch.Id;
    
        if (TeamId <= 0 || MatchId <= 0)
    {
    StatusMessage = "❌ Invalid team or match selection";
            return;
  }

        try
    {
            StatusMessage = "Exporting JSON...";

            var jsonData = new Dictionary<string, object?>
 {
              ["team_id"] = TeamId,
   ["team_number"] = SelectedTeam.TeamNumber,
       ["match_id"] = MatchId,
         ["match_number"] = SelectedMatch.MatchNumber,
     ["alliance"] = "unknown",
    ["scout_name"] = ScoutName
  };

      foreach (var kvp in fieldValues)
      {
        jsonData[kvp.Key] = ConvertValueForSerialization(kvp.Value);
            }

            if (GameConfig != null)
     {
       jsonData["auto_period_timer_enabled"] = false;
                jsonData["auto_points_points"] = AutoPoints;
 jsonData["teleop_points_points"] = TeleopPoints;
              jsonData["endgame_points_points"] = EndgamePoints;
            jsonData["total_points_points"] = TotalPoints;
            }

  jsonData["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            jsonData["offline_generated"] = true;

            // Serialize to JSON with formatting
      var options = new JsonSerializerOptions
            {
    WriteIndented = true,
  PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};
  var json = JsonSerializer.Serialize(jsonData, options);

            var filename = $"scout_team{SelectedTeam.TeamNumber}_match{SelectedMatch.MatchNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var result = await SaveJsonToFileAsync(json, filename);

   if (result)
       {
        StatusMessage = $"✓ Exported to {filename}";
             await Task.Delay(3000);
      if (StatusMessage.StartsWith("✓ Exported"))
                {
              StatusMessage = string.Empty;
          }
    }
else
      {
         StatusMessage = "✗ Failed to save JSON file";
       }
        }
        catch (Exception ex)
        {
        StatusMessage = $"✗ Error exporting JSON: {ex.Message}";
        }
    }

    private async Task<bool> SaveJsonToFileAsync(string json, string filename)
    {
        try
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
       var scoutingFolder = Path.Combine(documentsPath, "ObsidianScout", "Exports");
  
   if (!Directory.Exists(scoutingFolder))
       {
        Directory.CreateDirectory(scoutingFolder);
  }

     var filePath = Path.Combine(scoutingFolder, filename);
     await File.WriteAllTextAsync(filePath, json);
            return true;
        }
      catch (Exception ex)
     {
            System.Diagnostics.Debug.WriteLine($"Failed to save JSON file: {ex.Message}");
            return false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        LoadGameConfigAsync();
        await LoadTeamsAsync();
  }

    [RelayCommand]
    private void ResetForm()
    {
    SelectedTeam = null;
        SelectedMatch = null;
        TeamId = 0;
        MatchId = 0;
        ScoutName = string.Empty;
        InitializeFieldValues();
        OnPropertyChanged("FieldValuesChanged");
  StatusMessage = string.Empty;
        IsQRCodeVisible = false;
        QrCodeImage = null;
    }
}
