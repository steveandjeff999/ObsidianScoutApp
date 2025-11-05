using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;

namespace ObsidianScout.ViewModels;

// CRITICAL: Add QueryProperty attributes for deep linking support from notifications
[QueryProperty(nameof(EventId), "eventId")]
[QueryProperty(nameof(EventCode), "eventCode")]
[QueryProperty(nameof(MatchNumber), "matchNumber")]
public partial class MatchPredictionViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;
    private GameConfig? _gameConfig;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Event> events = new();

    [ObservableProperty]
    private Event? selectedEvent;

    [ObservableProperty]
    private ObservableCollection<Match> matches = new();

    [ObservableProperty]
    private Match? selectedMatch;

    [ObservableProperty]
    private MatchPrediction? prediction;

    [ObservableProperty]
    private bool hasPrediction;

    [ObservableProperty]
    private bool showInsufficientDataWarning;

    [ObservableProperty]
    private string insufficientDataMessage = string.Empty;

  // NEW: Properties for deep linking from notifications
    private string? _eventId;
    public string? EventId
    {
        get => _eventId;
        set
        {
            _eventId = value;
        System.Diagnostics.Debug.WriteLine($"[MatchPredictionVM] EventId set to: {value}");
     _ = HandleDeepLinkAsync();
   }
    }

    private string? _eventCode;
    public string? EventCode
    {
        get => _eventCode;
        set
      {
        _eventCode = value;
            System.Diagnostics.Debug.WriteLine($"[MatchPredictionVM] EventCode set to: {value}");
        }
    }

    private string? _matchNumber;
    public string? MatchNumber
    {
  get => _matchNumber;
        set
        {
            _matchNumber = value;
         System.Diagnostics.Debug.WriteLine($"[MatchPredictionVM] MatchNumber set to: {value}");
     }
    }

    public MatchPredictionViewModel(IApiService apiService, ISettingsService settingsService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
    }

    public async Task InitializeAsync()
    {
    await LoadGameConfigAsync();
        await LoadEventsAsync();
    }

    // NEW: Handle deep link from notification
    private async Task HandleDeepLinkAsync()
    {
        // Wait for initialization to complete
        if (Events.Count == 0)
     {
   await Task.Delay(500);
            if (Events.Count == 0)
            {
   System.Diagnostics.Debug.WriteLine("[MatchPredictionVM] Events not loaded yet, waiting...");
       return;
     }
        }

        if (!string.IsNullOrEmpty(_eventId) && int.TryParse(_eventId, out var eventIdInt))
        {
       System.Diagnostics.Debug.WriteLine($"[MatchPredictionVM] Handling deep link to event {eventIdInt}, match {_matchNumber}");

            // Find and select the event
            var targetEvent = Events.FirstOrDefault(e => e.Id == eventIdInt);
 if (targetEvent != null)
            {
      System.Diagnostics.Debug.WriteLine($"[MatchPredictionVM] Found event: {targetEvent.Name}");
        SelectedEvent = targetEvent;

         // Wait for matches to load
  await Task.Delay(1000);

     // If match number specified, find and select it
             if (!string.IsNullOrEmpty(_matchNumber) && int.TryParse(_matchNumber, out var matchNumInt))
      {
       var targetMatch = Matches.FirstOrDefault(m => m.MatchNumber == matchNumInt);
 if (targetMatch != null)
         {
  System.Diagnostics.Debug.WriteLine($"[MatchPredictionVM] Found match {matchNumInt}, auto-selecting");
              SelectedMatch = targetMatch;

          // Give UI time to update, then auto-predict
  await Task.Delay(500);
     await PredictMatchAsync();
       
      StatusMessage = $"?? Opened from notification - Match {matchNumInt}";
          }
  else
          {
    System.Diagnostics.Debug.WriteLine($"[MatchPredictionVM] Match {matchNumInt} not found in loaded matches");
        StatusMessage = $"Match {matchNumInt} selected - tap 'Predict Match' to analyze";
             }
       }
else
       {
  StatusMessage = $"Event {targetEvent.Name} selected - choose a match to predict";
        }
            }
       else
  {
                System.Diagnostics.Debug.WriteLine($"[MatchPredictionVM] Event {eventIdInt} not found");
     StatusMessage = $"Could not find event (ID: {eventIdInt})";
     }
        }
    }

    private async Task LoadGameConfigAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Loading game config for predictions...");
     var response = await _apiService.GetGameConfigAsync();
          
    if (response.Success && response.Config != null)
            {
        _gameConfig = response.Config;
        System.Diagnostics.Debug.WriteLine($"Game config loaded: {_gameConfig.GameName}");
         }
            else
        {
                System.Diagnostics.Debug.WriteLine($"Failed to load game config: {response.Error}");
            }
        }
        catch (Exception ex)
        {
     System.Diagnostics.Debug.WriteLine($"Error loading game config: {ex.Message}");
  }
    }

    private async Task LoadEventsAsync()
    {
try
   {
         IsLoading = true;
   StatusMessage = "Loading events...";

var response = await _apiService.GetEventsAsync();
         
            if (response.Success && response.Events != null)
 {
                Events = new ObservableCollection<Event>(response.Events);
    StatusMessage = $"{Events.Count} events loaded";
            
                // Try to auto-select the current event from game config (if not deep linking)
              Event? eventToSelect = null;
                
         // Don't auto-select if we're handling a deep link
          if (string.IsNullOrEmpty(_eventId))
          {
         if (_gameConfig != null && !string.IsNullOrEmpty(_gameConfig.CurrentEventCode))
     {
   eventToSelect = Events.FirstOrDefault(e => 
    e.Code.Equals(_gameConfig.CurrentEventCode, StringComparison.OrdinalIgnoreCase));
         
       if (eventToSelect != null)
      {
  System.Diagnostics.Debug.WriteLine($"Auto-selected current event: {eventToSelect.Name}");
         }
  }
            
    // Fallback to first event if current event not found
            if (eventToSelect == null && Events.Count > 0)
    {
      eventToSelect = Events[0];
          }
 
   if (eventToSelect != null)
           {
        SelectedEvent = eventToSelect;
    }
      }
      else
      {
       // We have a deep link, trigger the handler
  await HandleDeepLinkAsync();
    }
            }
    else
            {
   StatusMessage = response.Error ?? "Failed to load events";
     }
        }
        catch (Exception ex)
        {
   StatusMessage = $"Error loading events: {ex.Message}";
        }
    finally
   {
     IsLoading = false;
        }
    }

    partial void OnSelectedEventChanged(Event? oldValue, Event? newValue)
    {
        if (newValue != null)
        {
        // Don't clear selected match if we're handling a deep link
  if (string.IsNullOrEmpty(_matchNumber))
{
     SelectedMatch = null;
       }
            
   Prediction = null;
            HasPrediction = false;
            _ = LoadMatchesAsync();
}
    }

    private async Task LoadMatchesAsync()
    {
        if (SelectedEvent == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Loading matches...";

            var response = await _apiService.GetMatchesAsync(SelectedEvent.Id);
            
            if (response.Success && response.Matches != null)
            {
                // Sort matches by type and number
                var sortedMatches = response.Matches
                    .OrderBy(m => m.MatchType == "Qualification" ? 0 : 1)
                    .ThenBy(m => m.MatchNumber)
                    .ToList();
                
                Matches = new ObservableCollection<Match>(sortedMatches);
                StatusMessage = $"{Matches.Count} matches loaded";
            }
            else
            {
                StatusMessage = response.Error ?? "Failed to load matches";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading matches: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PredictMatchAsync()
    {
        if (SelectedMatch == null)
        {
            StatusMessage = "Please select a match";
            return;
        }

        if (SelectedEvent == null)
        {
            StatusMessage = "Please select an event";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Analyzing team data...";
            ShowInsufficientDataWarning = false;

            System.Diagnostics.Debug.WriteLine($"=== PREDICTING MATCH {SelectedMatch.MatchNumber} ===");
            
            // Parse team numbers from alliances
            var redTeams = ParseTeamNumbers(SelectedMatch.RedAlliance);
            var blueTeams = ParseTeamNumbers(SelectedMatch.BlueAlliance);
            
            System.Diagnostics.Debug.WriteLine($"Red Alliance: {string.Join(", ", redTeams)}");
            System.Diagnostics.Debug.WriteLine($"Blue Alliance: {string.Join(", ", blueTeams)}");

            if (redTeams.Count == 0 || blueTeams.Count == 0)
            {
                StatusMessage = "Invalid match - missing team data";
                return;
            }

            // Get historical stats for all teams
            var allTeams = redTeams.Concat(blueTeams).ToList();
            var teamStats = new Dictionary<int, TeamHistoricalStats>();
            var teamsWithoutData = new List<int>();

            foreach (var teamNumber in allTeams)
            {
                var stats = await GetTeamHistoricalStatsAsync(teamNumber, SelectedEvent.Id);
                if (stats != null && stats.MatchCount > 0)
                {
                    teamStats[teamNumber] = stats;
                    System.Diagnostics.Debug.WriteLine($"Team {teamNumber}: {stats.MatchCount} matches, Avg={stats.AvgTotalPoints:F1}");
                }
                else
                {
                    teamsWithoutData.Add(teamNumber);
                    System.Diagnostics.Debug.WriteLine($"Team {teamNumber}: NO DATA");
                }
            }

            // Build prediction
            var matchPrediction = new MatchPrediction
            {
                Match = SelectedMatch,
                HasSufficientData = teamsWithoutData.Count == 0
            };

            // Predict red alliance
            foreach (var teamNumber in redTeams)
            {
                var teamPred = CreateTeamPrediction(teamNumber, teamStats, "red");
                matchPrediction.RedAlliance.Add(teamPred);
                
                matchPrediction.RedPredictedAutoPoints += teamPred.PredictedAutoPoints;
                matchPrediction.RedPredictedTeleopPoints += teamPred.PredictedTeleopPoints;
                matchPrediction.RedPredictedEndgamePoints += teamPred.PredictedEndgamePoints;
                matchPrediction.RedPredictedTotalPoints += teamPred.PredictedTotalPoints;
            }

            // Predict blue alliance
            foreach (var teamNumber in blueTeams)
            {
                var teamPred = CreateTeamPrediction(teamNumber, teamStats, "blue");
                matchPrediction.BlueAlliance.Add(teamPred);
                
                matchPrediction.BluePredictedAutoPoints += teamPred.PredictedAutoPoints;
                matchPrediction.BluePredictedTeleopPoints += teamPred.PredictedTeleopPoints;
                matchPrediction.BluePredictedEndgamePoints += teamPred.PredictedEndgamePoints;
                matchPrediction.BluePredictedTotalPoints += teamPred.PredictedTotalPoints;
            }

            // Calculate win probability using a simple normal distribution model
            var scoreDiff = matchPrediction.RedPredictedTotalPoints - matchPrediction.BluePredictedTotalPoints;
            var redStdDev = Math.Sqrt(matchPrediction.RedAlliance.Sum(t => Math.Pow(t.TotalPointsStd, 2)));
            var blueStdDev = Math.Sqrt(matchPrediction.BlueAlliance.Sum(t => Math.Pow(t.TotalPointsStd, 2)));
            var combinedStdDev = Math.Sqrt(redStdDev * redStdDev + blueStdDev * blueStdDev);
            
            if (combinedStdDev > 0)
            {
                // Z-score for score difference
                var zScore = scoreDiff / combinedStdDev;
                matchPrediction.RedWinProbability = NormalCDF(zScore);
                matchPrediction.BlueWinProbability = 1 - matchPrediction.RedWinProbability;
            }
            else
            {
                // No variance, use simple comparison
                if (scoreDiff > 0)
                {
                    matchPrediction.RedWinProbability = 0.75;
                    matchPrediction.BlueWinProbability = 0.25;
                }
                else if (scoreDiff < 0)
                {
                    matchPrediction.RedWinProbability = 0.25;
                    matchPrediction.BlueWinProbability = 0.75;
                }
                else
                {
                    matchPrediction.RedWinProbability = 0.5;
                    matchPrediction.BlueWinProbability = 0.5;
                }
            }

            // Set warning message if insufficient data
            if (teamsWithoutData.Count > 0)
            {
                matchPrediction.WarningMessage = $"?? {teamsWithoutData.Count} team(s) have no historical data at this event: {string.Join(", ", teamsWithoutData)}";
                ShowInsufficientDataWarning = true;
                InsufficientDataMessage = matchPrediction.WarningMessage;
            }

            Prediction = matchPrediction;
            HasPrediction = true;
            
            StatusMessage = $"Prediction complete! Red {matchPrediction.RedWinProbability:P0} vs Blue {matchPrediction.BlueWinProbability:P0}";
            
            System.Diagnostics.Debug.WriteLine($"=== PREDICTION COMPLETE ===");
            System.Diagnostics.Debug.WriteLine($"Red Total: {matchPrediction.RedPredictedTotalPoints:F1}");
            System.Diagnostics.Debug.WriteLine($"Blue Total: {matchPrediction.BluePredictedTotalPoints:F1}");
            System.Diagnostics.Debug.WriteLine($"Red Win Prob: {matchPrediction.RedWinProbability:P1}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error predicting match: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<int> ParseTeamNumbers(string alliance)
    {
        if (string.IsNullOrEmpty(alliance)) return new List<int>();

        return alliance.Split(',')
            .Select(s => s.Trim())
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToList();
    }

    private async Task<TeamHistoricalStats?> GetTeamHistoricalStatsAsync(int teamNumber, int eventId)
    {
        try
        {
            // Fetch scouting data for this team at this event
            var response = await _apiService.GetAllScoutingDataAsync(
                teamNumber: teamNumber, 
                eventId: eventId,
                limit: 100
            );
            
            if (!response.Success || response.Entries == null || response.Entries.Count == 0)
            {
                return null;
            }

            // Filter to only entries for this specific team
            var entries = response.Entries
                .Where(e => e.TeamNumber == teamNumber)
                .ToList();

            if (entries.Count == 0)
            {
                return null;
            }

            // Calculate statistics
            var autoPoints = new List<double>();
            var teleopPoints = new List<double>();
            var endgamePoints = new List<double>();
            var totalPoints = new List<double>();

            foreach (var entry in entries)
            {
                var auto = CalculateAutoPoints(entry.Data);
                var teleop = CalculateTeleopPoints(entry.Data);
                var endgame = CalculateEndgamePoints(entry.Data);
                var total = auto + teleop + endgame;

                autoPoints.Add(auto);
                teleopPoints.Add(teleop);
                endgamePoints.Add(endgame);
                totalPoints.Add(total);
            }

            var stats = new TeamHistoricalStats
            {
                TeamNumber = teamNumber,
                TeamName = entries.FirstOrDefault()?.TeamName ?? $"Team {teamNumber}",
                
                AvgAutoPoints = autoPoints.Average(),
                StdAutoPoints = CalculateStdDev(autoPoints),
                
                AvgTeleopPoints = teleopPoints.Average(),
                StdTeleopPoints = CalculateStdDev(teleopPoints),
                
                AvgEndgamePoints = endgamePoints.Average(),
                StdEndgamePoints = CalculateStdDev(endgamePoints),
                
                AvgTotalPoints = totalPoints.Average(),
                StdTotalPoints = CalculateStdDev(totalPoints),
                
                MatchCount = entries.Count,
                Consistency = CalculateConsistency(totalPoints)
            };

            return stats;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting stats for team {teamNumber}: {ex.Message}");
            return null;
        }
    }

    private TeamMatchPrediction CreateTeamPrediction(
        int teamNumber, 
        Dictionary<int, TeamHistoricalStats> teamStats,
        string alliance)
    {
        if (teamStats.TryGetValue(teamNumber, out var stats))
        {
            return new TeamMatchPrediction
            {
                TeamNumber = teamNumber,
                TeamName = stats.TeamName,
                
                PredictedAutoPoints = stats.AvgAutoPoints,
                PredictedTeleopPoints = stats.AvgTeleopPoints,
                PredictedEndgamePoints = stats.AvgEndgamePoints,
                PredictedTotalPoints = stats.AvgTotalPoints,
                
                AutoPointsStd = stats.StdAutoPoints,
                TeleopPointsStd = stats.StdTeleopPoints,
                EndgamePointsStd = stats.StdEndgamePoints,
                TotalPointsStd = stats.StdTotalPoints,
                
                MatchCount = stats.MatchCount,
                Consistency = stats.Consistency,
                Alliance = alliance
            };
        }
        else
        {
            // No data available for this team
            return new TeamMatchPrediction
            {
                TeamNumber = teamNumber,
                TeamName = $"Team {teamNumber}",
                PredictedAutoPoints = 0,
                PredictedTeleopPoints = 0,
                PredictedEndgamePoints = 0,
                PredictedTotalPoints = 0,
                MatchCount = 0,
                Consistency = 0,
                Alliance = alliance
            };
        }
    }

    private double CalculateAutoPoints(Dictionary<string, object> data)
    {
        if (_gameConfig == null || _gameConfig.AutoPeriod == null)
            return 0;

        double points = 0;
        foreach (var element in _gameConfig.AutoPeriod.ScoringElements)
        {
            if (data.TryGetValue(element.Id, out var value))
            {
                var count = ConvertToDouble(value);
                points += count * element.Points;
            }
        }
        return points;
    }

    private double CalculateTeleopPoints(Dictionary<string, object> data)
    {
        if (_gameConfig == null || _gameConfig.TeleopPeriod == null)
            return 0;

        double points = 0;
        foreach (var element in _gameConfig.TeleopPeriod.ScoringElements)
        {
            if (data.TryGetValue(element.Id, out var value))
            {
                var count = ConvertToDouble(value);
                points += count * element.Points;
            }
        }
        return points;
    }

    private double CalculateEndgamePoints(Dictionary<string, object> data)
    {
        if (_gameConfig == null || _gameConfig.EndgamePeriod == null)
            return 0;

        double points = 0;
        foreach (var element in _gameConfig.EndgamePeriod.ScoringElements)
        {
            if (data.TryGetValue(element.Id, out var value))
            {
                if (element.Type.ToLower() == "counter")
                {
                    var count = ConvertToDouble(value);
                    points += count * element.Points;
                }
                else if (element.Type.ToLower() == "boolean")
                {
                    var isTrue = ConvertToBoolean(value);
                    if (isTrue) points += element.Points;
                }
                else if (element.Type.ToLower() == "multiple_choice")
                {
                    var selectedOption = ConvertToString(value);
                    var option = element.Options?.FirstOrDefault(o => o.Name == selectedOption);
                    if (option != null)
                        points += option.Points;
                }
            }
        }
        return points;
    }

    private double ConvertToDouble(object? value)
    {
        if (value == null) return 0;

        try
        {
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is float f) return f;
            if (value is decimal dec) return (double)dec;
            if (value is string s && double.TryParse(s, out var result)) return result;
            
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                    return jsonElement.GetDouble();
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    return double.TryParse(str, out var r) ? r : 0;
                }
            }

            return Convert.ToDouble(value);
        }
        catch
        {
            return 0;
        }
    }

    private bool ConvertToBoolean(object? value)
    {
        if (value == null) return false;

        try
        {
            if (value is bool b) return b;
            if (value is string s) return bool.TryParse(s, out var result) && result;
            
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.False) return false;
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    return bool.TryParse(str, out var r) && r;
                }
            }

            return Convert.ToBoolean(value);
        }
        catch
        {
            return false;
        }
    }

    private string ConvertToString(object? value, string defaultValue = "")
    {
        if (value == null) return defaultValue;

        try
        {
            if (value is string s) return s;
            
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    return jsonElement.GetString() ?? defaultValue;
                return jsonElement.ToString() ?? defaultValue;
            }

            return value.ToString() ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private double CalculateStdDev(List<double> values)
    {
        if (values.Count < 2) return 0;

        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }

    private double CalculateConsistency(List<double> values)
    {
        if (values.Count == 0) return 0;
        
        var avg = values.Average();
        if (avg == 0) return 0;
        
        var stdDev = CalculateStdDev(values);
        var cv = stdDev / avg; // Coefficient of variation
        
        // Convert to 0-1 scale (lower CV = higher consistency)
        // CV of 0 = 1.0 consistency, CV of 1.0 = 0 consistency
        return Math.Max(0, 1.0 - cv);
    }

    // Cumulative distribution function for standard normal distribution
    private double NormalCDF(double x)
    {
        // Approximation using error function
        return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
    }

    // Error function approximation
    private double Erf(double x)
    {
        // Abramowitz and Stegun approximation
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }
}
