using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace ObsidianScout.ViewModels;

/// <summary>
/// Data for a single team's qualitative assessment.
/// </summary>
public partial class QualitativeTeamData : ObservableObject
{
    public int TeamNumber { get; set; }
    public string Alliance { get; set; } = "individual";

    // Roles
    [ObservableProperty] private bool cycling;
    [ObservableProperty] private bool stealing;
    [ObservableProperty] private bool scoring;
    [ObservableProperty] private bool feeding;
    [ObservableProperty] private bool defending;
    [ObservableProperty] private bool didNotContribute;

    // Feeder types
    [ObservableProperty] private bool feederTypeContinuous;
    [ObservableProperty] private bool feederTypeStopToShoot;
    [ObservableProperty] private bool feederTypeDump;

    // Field traversal
    [ObservableProperty] private bool canScoreWhileMoving;

    // Ratings (nullable = not set)
    [ObservableProperty] private int? driverRating;
    [ObservableProperty] private int? defenseEffectiveness;
    [ObservableProperty] private int? shotAccuracy;
    [ObservableProperty] private int? robotRating;

    // Overall rating (required, 1-5)
    [ObservableProperty] private int? overallRating;

    // Ranking (1-3)
    [ObservableProperty] private int? ranking;

    // Endgame
    [ObservableProperty] private string? endgameClimbResult; // "success" or "fail"
    [ObservableProperty] private string? endgameClimbLevel;  // "low", "mid", "high"

    // Auto climb
    [ObservableProperty] private string? autoClimbResult; // "success" or "fail"

    // Beached
    [ObservableProperty] private bool gotBeached;

    // Notes
    [ObservableProperty] private string notes = string.Empty;

    /// <summary>
    /// Convert to dictionary matching the server JSON format.
    /// </summary>
    public Dictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>
        {
            ["cycling"] = Cycling,
            ["stealing"] = Stealing,
            ["scoring"] = Scoring,
            ["feeding"] = Feeding,
            ["defending"] = Defending,
            ["did_not_contribute"] = DidNotContribute,
            ["feeder_type_continuous"] = FeederTypeContinuous,
            ["feeder_type_stop_to_shoot"] = FeederTypeStopToShoot,
            ["feeder_type_dump"] = FeederTypeDump,
            ["can_score_while_moving"] = CanScoreWhileMoving,
            ["driver_rating"] = DriverRating,
            ["defense_effectiveness"] = DefenseEffectiveness,
            ["shot_accuracy"] = ShotAccuracy,
            ["got_beached"] = GotBeached,
            ["endgame_climb_result"] = EndgameClimbResult,
            ["endgame_climb_level"] = EndgameClimbLevel,
            ["auto_climb_result"] = AutoClimbResult,
            ["ranking"] = Ranking,
            ["robot_rating"] = RobotRating,
            ["overall_rating"] = OverallRating,
            ["notes"] = Notes,
            ["played_defense"] = Defending,
            ["cycler"] = Cycling,
            ["passer"] = false,
            ["cleanup"] = false
        };
    }
}

public partial class QualitativeScoutingViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly IQRCodeService _qrCodeService;
    private readonly ISettingsService _settingsService;
    private readonly ICacheService? _cacheService;

    // Scout mode: "match" or "team"
    [ObservableProperty] private bool isMatchMode = false;
    [ObservableProperty] private bool isTeamMode = true;

    // Predicted winner
    [ObservableProperty] private string? predictedWinner;

    // Match selection
    [ObservableProperty] private Match? selectedMatch;

    // Alliance selection for match mode: "red", "blue", "both"
    [ObservableProperty] private string? selectedAlliance;

    // Team selection for individual mode
    [ObservableProperty] private int? selectedTeamNumber;

    // Loading / status
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isSaving;

    // QR
    [ObservableProperty] private ImageSource? qrCodeImage;
    [ObservableProperty] private bool isQRCodeVisible;

    // Scout info
    [ObservableProperty] private string scoutName = string.Empty;
    [ObservableProperty] private string eventCode = string.Empty;

    // Game config
    [ObservableProperty] private GameConfig? gameConfig;

    // Team data cards
    public ObservableCollection<QualitativeTeamData> TeamCards { get; } = new();

    // Available matches
    public ObservableCollection<Match> Matches { get; } = new();

    // Available teams from selected match
    public ObservableCollection<TeamPickerItem> AvailableTeams { get; } = new();

    // Selected team picker item
    [ObservableProperty] private TeamPickerItem? selectedTeamPickerItem;

    // Lookup: team number -> database team ID
    private readonly Dictionary<int, int> _teamNumberToIdMap = new();

    public QualitativeScoutingViewModel(
        IApiService apiService,
        IQRCodeService qrCodeService,
        ISettingsService settingsService,
        ICacheService cacheService)
    {
        _apiService = apiService;
        _qrCodeService = qrCodeService;
        _settingsService = settingsService;
        _cacheService = cacheService;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await LoadScoutNameAsync();
            await LoadGameConfigAsync();
            await LoadTeamsAsync();
            await LoadMatchesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QualScouting] Init error: {ex.Message}");
            StatusMessage = "Error loading data";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadTeamsAsync()
    {
        try
        {
            var response = await _apiService.GetTeamsAsync(limit: 500);
            if (response?.Teams != null)
            {
                foreach (var team in response.Teams)
                {
                    _teamNumberToIdMap[team.TeamNumber] = team.Id;
                }
                System.Diagnostics.Debug.WriteLine($"[QualScouting] Loaded {_teamNumberToIdMap.Count} team mappings");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QualScouting] LoadTeams error: {ex.Message}");
        }
    }

    private int ResolveTeamId(int teamNumber)
    {
        if (_teamNumberToIdMap.TryGetValue(teamNumber, out int dbId))
            return dbId;
        System.Diagnostics.Debug.WriteLine($"[QualScouting] WARNING: No database ID found for team number {teamNumber}, using 0");
        return 0;
    }

    private async Task LoadScoutNameAsync()
    {
        try
        {
            var username = await _settingsService.GetUsernameAsync();
            if (!string.IsNullOrEmpty(username))
                ScoutName = username;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QualScouting] LoadScoutName error: {ex.Message}");
        }
    }

    private async Task LoadGameConfigAsync()
    {
        try
        {
            var response = await _apiService.GetGameConfigAsync();
            if (response?.Config != null)
            {
                GameConfig = response.Config;
                EventCode = GameConfig.CurrentEventCode ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QualScouting] LoadGameConfig error: {ex.Message}");
        }
    }

    private async Task LoadMatchesAsync()
    {
        try
        {
            // Load all matches from all events
            var eventsResponse = await _apiService.GetEventsAsync();
            if (eventsResponse?.Events == null) return;

            Matches.Clear();
            foreach (var evt in eventsResponse.Events)
            {
                try
                {
                    var matchesResponse = await _apiService.GetMatchesAsync(evt.Id);
                    if (matchesResponse?.Matches != null)
                    {
                        foreach (var match in matchesResponse.Matches.OrderBy(m => m.MatchTypeOrder).ThenBy(m => m.MatchNumber))
                        {
                            Matches.Add(match);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[QualScouting] Error loading matches for event {evt.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QualScouting] LoadMatches error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SetMatchMode()
    {
        IsMatchMode = true;
        IsTeamMode = false;
        TeamCards.Clear();
        SelectedTeamPickerItem = null;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void SetTeamMode()
    {
        IsMatchMode = false;
        IsTeamMode = true;
        TeamCards.Clear();
        SelectedAlliance = null;
        StatusMessage = string.Empty;
    }

    partial void OnSelectedMatchChanged(Match? value)
    {
        PopulateTeamsFromMatch();
        TeamCards.Clear();

        if (IsMatchMode && !string.IsNullOrEmpty(SelectedAlliance))
        {
            BuildMatchTeamCards();
        }
    }

    partial void OnSelectedAllianceChanged(string? value)
    {
        if (IsMatchMode && SelectedMatch != null)
        {
            BuildMatchTeamCards();
        }
    }

    partial void OnSelectedTeamPickerItemChanged(TeamPickerItem? value)
    {
        if (IsTeamMode && value != null)
        {
            BuildIndividualTeamCard(value.TeamNumber);
        }
    }

    private void PopulateTeamsFromMatch()
    {
        AvailableTeams.Clear();
        if (SelectedMatch == null) return;

        var redTeams = ParseTeamNumbers(SelectedMatch.RedAlliance);
        var blueTeams = ParseTeamNumbers(SelectedMatch.BlueAlliance);

        foreach (var t in redTeams)
            AvailableTeams.Add(new TeamPickerItem { TeamNumber = t, Alliance = "Red", Display = $"Team {t} (Red)" });
        foreach (var t in blueTeams)
            AvailableTeams.Add(new TeamPickerItem { TeamNumber = t, Alliance = "Blue", Display = $"Team {t} (Blue)" });
    }

    private void BuildMatchTeamCards()
    {
        TeamCards.Clear();
        if (SelectedMatch == null || string.IsNullOrEmpty(SelectedAlliance)) return;

        var redTeams = ParseTeamNumbers(SelectedMatch.RedAlliance);
        var blueTeams = ParseTeamNumbers(SelectedMatch.BlueAlliance);

        if (SelectedAlliance == "red" || SelectedAlliance == "both")
        {
            foreach (var t in redTeams)
                TeamCards.Add(new QualitativeTeamData { TeamNumber = t, Alliance = "red" });
        }
        if (SelectedAlliance == "blue" || SelectedAlliance == "both")
        {
            foreach (var t in blueTeams)
                TeamCards.Add(new QualitativeTeamData { TeamNumber = t, Alliance = "blue" });
        }
    }

    private void BuildIndividualTeamCard(int teamNumber)
    {
        TeamCards.Clear();
        TeamCards.Add(new QualitativeTeamData { TeamNumber = teamNumber, Alliance = "individual" });
    }

    private static List<int> ParseTeamNumbers(string allianceStr)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(allianceStr)) return result;

        foreach (var part in allianceStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out int num))
                result.Add(num);
        }
        return result;
    }

    private bool ValidateForm()
    {
        if (SelectedMatch == null)
        {
            StatusMessage = "Please select a match";
            return false;
        }

        if (IsTeamMode && SelectedTeamPickerItem == null)
        {
            StatusMessage = "Please select a team";
            return false;
        }

        if (IsMatchMode && string.IsNullOrEmpty(SelectedAlliance))
        {
            StatusMessage = "Please select an alliance";
            return false;
        }

        if (TeamCards.Count == 0)
        {
            StatusMessage = "No teams to scout";
            return false;
        }

        // Validate overall rating and ranking are set for all teams
        foreach (var card in TeamCards)
        {
            if (card.OverallRating == null || card.OverallRating < 1 || card.OverallRating > 5)
            {
                StatusMessage = $"Please set overall rating (1-5) for Team {card.TeamNumber}";
                return false;
            }
            if (card.Ranking == null || card.Ranking < 1 || card.Ranking > 3)
            {
                StatusMessage = $"Please set ranking (1-3) for Team {card.TeamNumber}";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Build the full qualitative payload dictionary.
    /// </summary>
    private Dictionary<string, object?> BuildPayload()
    {
        var payload = new Dictionary<string, object?>();

        payload["qualitative"] = true;
        payload["match_id"] = SelectedMatch!.Id;
        payload["match_number"] = SelectedMatch.MatchNumber;
        payload["match_type"] = SelectedMatch.MatchType;
        payload["event_code"] = EventCode;
        payload["scout_name"] = ScoutName;
        payload["timestamp"] = DateTime.UtcNow.ToString("O");

        var matchSummary = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(PredictedWinner))
            matchSummary["predicted_winner"] = PredictedWinner;

        if (IsTeamMode)
        {
            payload["individual_team"] = true;
            var teamNum = TeamCards[0].TeamNumber;
            payload["team_number"] = teamNum;
            payload["alliance_scouted"] = $"team_{teamNum}";

            var individualData = new Dictionary<string, object?>();
            individualData[$"team_{teamNum}"] = TeamCards[0].ToDictionary();

            var teamData = new Dictionary<string, object?>
            {
                ["individual"] = individualData,
                ["team_number"] = teamNum,
                ["_match_summary"] = matchSummary
            };
            payload["team_data"] = teamData;
        }
        else
        {
            payload["alliance_scouted"] = SelectedAlliance;

            var redData = new Dictionary<string, object?>();
            var blueData = new Dictionary<string, object?>();

            foreach (var card in TeamCards)
            {
                var key = $"team_{card.TeamNumber}";
                if (card.Alliance == "red")
                    redData[key] = card.ToDictionary();
                else if (card.Alliance == "blue")
                    blueData[key] = card.ToDictionary();
            }

            var teamData = new Dictionary<string, object?>
            {
                ["red"] = redData,
                ["blue"] = blueData,
                ["_match_summary"] = matchSummary
            };
            payload["team_data"] = teamData;
        }

        return payload;
    }

    /// <summary>
    /// Build cache entries for History — one entry per team so each team appears individually.
    /// </summary>
    private List<ScoutingEntry> BuildCacheEntries(Dictionary<string, object?> payload, string baseOfflineId)
    {
        var entries = new List<ScoutingEntry>();
        var timestamp = DateTime.Now;

        foreach (var card in TeamCards)
        {
            var entryPayload = new Dictionary<string, object?>(payload)
            {
                ["team_number"] = card.TeamNumber
            };
            // Include individual team's qualitative data at top level for History display
            foreach (var kvp in card.ToDictionary())
                entryPayload[$"qual_{kvp.Key}"] = kvp.Value;

            entries.Add(new ScoutingEntry
            {
                TeamId = ResolveTeamId(card.TeamNumber),
                TeamNumber = card.TeamNumber,
                MatchId = SelectedMatch!.Id,
                MatchNumber = SelectedMatch.MatchNumber,
                MatchType = SelectedMatch.MatchType,
                EventCode = EventCode,
                Alliance = card.Alliance,
                ScoutName = ScoutName,
                Timestamp = timestamp,
                Data = entryPayload.ToDictionary(k => k.Key, v => v.Value ?? new object()),
                OfflineId = TeamCards.Count > 1 ? $"{baseOfflineId}_{card.TeamNumber}" : baseOfflineId
            });
        }

        return entries;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ValidateForm()) return;

        IsSaving = true;
        StatusMessage = "Saving...";

        try
        {
            var payload = BuildPayload();

            // Resolve the team's database ID from the team number
            var teamNumber = IsTeamMode ? TeamCards[0].TeamNumber : (TeamCards.Count > 0 ? TeamCards[0].TeamNumber : 0);
            var submission = new ScoutingSubmission
            {
                TeamId = ResolveTeamId(teamNumber),
                MatchId = SelectedMatch!.Id,
                Data = payload
            };

            // Build cache entries — one per team so every team appears in History
            var entries = BuildCacheEntries(payload, submission.OfflineId);

            bool uploadSucceeded = false;
            try
            {
                var result = await _apiService.SubmitScoutingDataAsync(submission);
                uploadSucceeded = result.Success;

                if (result.Success)
                {
                    StatusMessage = "✓ Qualitative scouting data saved!";
                    if (result.ScoutingId > 0 && entries.Count > 0)
                        entries[0].Id = result.ScoutingId;
                }
                else
                {
                    StatusMessage = $"⚠ Saved locally. Upload failed: {result.Message}";
                    System.Diagnostics.Debug.WriteLine($"[QualScouting] Server upload failed: {result.Message}");
                }
            }
            catch (Exception uploadEx)
            {
                System.Diagnostics.Debug.WriteLine($"[QualScouting] Upload exception: {uploadEx.Message}");
                StatusMessage = "⚠ Saved locally. Server upload failed (offline?)";
            }

            // Always cache locally as backup — every team gets its own History entry
            try
            {
                if (_cacheService != null)
                {
                    var cached = await _cacheService.GetCachedScoutingDataAsync() ?? new List<ScoutingEntry>();
                    cached.AddRange(entries);
                    await _cacheService.CacheScoutingDataAsync(cached);

                    if (!uploadSucceeded)
                    {
                        foreach (var entry in entries)
                            await _cacheService.AddPendingScoutingAsync(entry);
                        System.Diagnostics.Debug.WriteLine($"[QualScouting] Added {entries.Count} entries to pending queue");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QualScouting] Cache error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QualScouting] Save error: {ex.Message}");
            StatusMessage = $"Error saving: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task GenerateQRCode()
    {
        if (!ValidateForm()) return;

        try
        {
            var payload = BuildPayload();
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });

            // Generate QR code
            QrCodeImage = _qrCodeService.GenerateQRCode(json);
            IsQRCodeVisible = true;

            // Cache entries for each team so they all appear in History
            try
            {
                if (_cacheService != null)
                {
                    var entries = BuildCacheEntries(payload, Guid.NewGuid().ToString());
                    var cached = await _cacheService.GetCachedScoutingDataAsync() ?? new List<ScoutingEntry>();
                    cached.AddRange(entries);
                    await _cacheService.CacheScoutingDataAsync(cached);

                    foreach (var entry in entries)
                        await _cacheService.AddPendingScoutingAsync(entry);

                    System.Diagnostics.Debug.WriteLine($"[QualScouting] QR generated and {entries.Count} entries cached for History");
                }
            }
            catch (Exception cacheEx)
            {
                System.Diagnostics.Debug.WriteLine($"[QualScouting] Cache during QR generation error: {cacheEx.Message}");
            }

            StatusMessage = "✓ QR code generated and saved to History";
        }
        catch (Exception ex)
        {
            StatusMessage = $"QR code error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[QualScouting] QR error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CloseQRCode()
    {
        IsQRCodeVisible = false;
        QrCodeImage = null;
    }

    [RelayCommand]
    private void SetOverallRating(string param)
    {
        // param format: "teamNumber:rating"
        var parts = param.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int teamNum) && int.TryParse(parts[1], out int rating))
        {
            var card = TeamCards.FirstOrDefault(c => c.TeamNumber == teamNum);
            if (card != null)
                card.OverallRating = rating;
        }
    }

    [RelayCommand]
    private void SetRanking(string param)
    {
        var parts = param.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int teamNum) && int.TryParse(parts[1], out int rank))
        {
            var card = TeamCards.FirstOrDefault(c => c.TeamNumber == teamNum);
            if (card != null)
                card.Ranking = rank;
        }
    }

    [RelayCommand]
    private void SetEndgameResult(string param)
    {
        var parts = param.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int teamNum))
        {
            var card = TeamCards.FirstOrDefault(c => c.TeamNumber == teamNum);
            if (card != null)
                card.EndgameClimbResult = parts[1];
        }
    }

    [RelayCommand]
    private void SetAutoClimbResult(string param)
    {
        var parts = param.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int teamNum))
        {
            var card = TeamCards.FirstOrDefault(c => c.TeamNumber == teamNum);
            if (card != null)
                card.AutoClimbResult = parts[1];
        }
    }

    [RelayCommand]
    private void SetPrediction(string value)
    {
        PredictedWinner = string.IsNullOrEmpty(value) ? null : value;
    }
}

public class TeamPickerItem
{
    public int TeamNumber { get; set; }
    public string Alliance { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;

    public override string ToString() => Display;
}
