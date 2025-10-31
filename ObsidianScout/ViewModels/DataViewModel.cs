using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ObsidianScout.ViewModels;

public partial class DataViewModel : ObservableObject
{
    private readonly IApiService _apiService;

    // Backing full lists used for filtering
    private readonly List<Event> _allEvents = new();
    private readonly List<Team> _allTeams = new();
    private readonly List<MatchCard> _allMatches = new();
    private readonly List<ScoutingEntry> _allScouting = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Event> events = new();

    [ObservableProperty]
    private ObservableCollection<Team> teams = new();

    [ObservableProperty]
    private ObservableCollection<MatchCard> matches = new();

    [ObservableProperty]
    private ObservableCollection<ScoutingEntry> scouting = new();

    [ObservableProperty]
    private bool showEvents = true;

    [ObservableProperty]
    private bool showTeams;

    [ObservableProperty]
    private bool showMatches;

    [ObservableProperty]
    private bool showScouting;

    [ObservableProperty]
    private Event? selectedEvent;

    // Search query
    [ObservableProperty]
    private string query = string.Empty;

    public DataViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    // Called by source generator when SelectedEvent changes
    partial void OnSelectedEventChanged(Event? oldValue, Event? newValue)
    {
        if (newValue != null)
        {
            // Auto-load teams and matches for the selected event
            _ = LoadTeamsAsync();
            _ = LoadMatchesAsync();
        }
    }

    // Called by source generator when Query changes
    partial void OnQueryChanged(string oldValue, string newValue)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = string.IsNullOrWhiteSpace(Query) ? string.Empty : Query.Trim().ToLowerInvariant();

        // Filter events
        Events.Clear();
        foreach (var ev in _allEvents)
        {
            if (string.IsNullOrEmpty(q) || MatchesEvent(ev, q))
                Events.Add(ev);
        }

        // Ensure SelectedEvent is still valid
        if (SelectedEvent != null && !Events.Any(e => e.Id == SelectedEvent.Id))
        {
            SelectedEvent = Events.FirstOrDefault();
        }

        // Filter teams
        Teams.Clear();
        foreach (var t in _allTeams)
        {
            if (string.IsNullOrEmpty(q) || MatchesTeam(t, q))
                Teams.Add(t);
        }

        // Filter matches
        Matches.Clear();
        foreach (var m in _allMatches)
        {
            if (string.IsNullOrEmpty(q) || MatchesMatch(m, q))
                Matches.Add(m);
        }

        // Filter scouting
        Scouting.Clear();
        foreach (var s in _allScouting)
        {
            if (string.IsNullOrEmpty(q) || MatchesScouting(s, q))
                Scouting.Add(s);
        }
    }

    private static bool MatchesEvent(Event ev, string q)
    {
        if (ev == null) return false;
        if (!string.IsNullOrEmpty(ev.Name) && ev.Name.ToLowerInvariant().Contains(q)) return true;
        if (!string.IsNullOrEmpty(ev.Code) && ev.Code.ToLowerInvariant().Contains(q)) return true;
        if (!string.IsNullOrEmpty(ev.Location) && ev.Location.ToLowerInvariant().Contains(q)) return true;
        if (ev.StartDate != DateTime.MinValue && ev.StartDate.ToString("yyyy-MM-dd").Contains(q)) return true;
        return false;
    }

    private static bool MatchesTeam(Team t, string q)
    {
        if (t == null) return false;
        if (!string.IsNullOrEmpty(t.TeamName) && t.TeamName.ToLowerInvariant().Contains(q)) return true;
        if (t.TeamNumber.ToString().Contains(q)) return true;
        if (!string.IsNullOrEmpty(t.Location) && t.Location.ToLowerInvariant().Contains(q)) return true;
        return false;
    }

    private static bool MatchesMatch(MatchCard m, string q)
    {
        if (m == null) return false;
        if (!string.IsNullOrEmpty(m.EventName) && m.EventName.ToLowerInvariant().Contains(q)) return true;
        if (m.Match != null)
        {
            if (!string.IsNullOrEmpty(m.Match.MatchType) && m.Match.MatchType.ToLowerInvariant().Contains(q)) return true;
            if (m.Match.MatchNumber.ToString().Contains(q)) return true;
            if (!string.IsNullOrEmpty(m.Match.RedAlliance) && m.Match.RedAlliance.ToLowerInvariant().Contains(q)) return true;
            if (!string.IsNullOrEmpty(m.Match.BlueAlliance) && m.Match.BlueAlliance.ToLowerInvariant().Contains(q)) return true;
        }
        return false;
    }

    private static bool MatchesScouting(ScoutingEntry s, string q)
    {
        if (s == null) return false;
        if (!string.IsNullOrEmpty(s.TeamName) && s.TeamName.ToLowerInvariant().Contains(q)) return true;
        if (s.TeamNumber.ToString().Contains(q)) return true;
        if (!string.IsNullOrEmpty(s.ScoutName) && s.ScoutName.ToLowerInvariant().Contains(q)) return true;
        if (s.MatchNumber.ToString().Contains(q)) return true;
        return false;
    }

    [RelayCommand]
    private Task ShowSectionAsync(string section)
    {
        ShowEvents = section == "events";
        ShowTeams = section == "teams";
        ShowMatches = section == "matches";
        ShowScouting = section == "scouting";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadEventsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading events...";

            var resp = await _apiService.GetEventsAsync();
            _allEvents.Clear();

            if (resp.Success && resp.Events != null)
            {
                // Insert a synthetic "All Events" option at top
                var all = new Event
                {
                    Id = 0,
                    Name = "All Events",
                    Code = "ALL",
                    Location = string.Empty,
                    StartDate = System.DateTime.MinValue,
                    EndDate = System.DateTime.MinValue,
                    Timezone = string.Empty,
                    TeamCount = 0
                };

                _allEvents.Add(all);

                foreach (var ev in resp.Events)
                    _allEvents.Add(ev);

                // Apply filter to update UI collection
                ApplyFilter();

                // Default to All Events if nothing selected
                SelectedEvent = Events.FirstOrDefault();

                StatusMessage = $"Loaded { _allEvents.Count -1 } events";
                await ShowSectionAsync("events");
            }
            else
            {
                StatusMessage = resp.Error ?? "Failed to load events";
            }
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadTeamsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading teams...";

            // If selected event is "All Events" (Id ==0) treat as no filter
            int? eventId = (SelectedEvent == null || SelectedEvent.Id ==0) ? null : SelectedEvent.Id;

            var resp = await _apiService.GetTeamsAsync(eventId: eventId, limit:1000);
            _allTeams.Clear();

            if (resp.Success && resp.Teams != null)
            {
                foreach (var t in resp.Teams.OrderBy(t => t.TeamNumber))
                    _allTeams.Add(t);

                // Apply filter to update UI collection
                ApplyFilter();

                StatusMessage = $"Loaded {_allTeams.Count} teams";
                await ShowSectionAsync("teams");
            }
            else
            {
                StatusMessage = resp.Error ?? "Failed to load teams";
            }
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMatchesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading matches...";

            _allMatches.Clear();

            if (SelectedEvent != null && SelectedEvent.Id !=0)
            {
                var mResp = await _apiService.GetMatchesAsync(SelectedEvent.Id);
                if (mResp.Success && mResp.Matches != null)
                {
                    foreach (var m in mResp.Matches.OrderBy(mm => mm.MatchNumber))
                        _allMatches.Add(new MatchCard { EventName = SelectedEvent.Name, Match = m });
                }
            }
            else
            {
                // All events - load matches for all events
                var evResp = await _api_service_GetEventsAsync();
                if (evResp.Success && evResp.Events != null)
                {
                    foreach (var ev in evResp.Events)
                    {
                        var mResp = await _apiService.GetMatchesAsync(ev.Id);
                        if (mResp.Success && mResp.Matches != null)
                        {
                            foreach (var m in mResp.Matches.OrderBy(mm => mm.MatchNumber))
                                _allMatches.Add(new MatchCard { EventName = ev.Name, Match = m });
                        }
                    }
                }
            }

            // Apply filter after populating all matches
            ApplyFilter();

            StatusMessage = $"Loaded {_allMatches.Count} matches";
            await ShowSectionAsync("matches");
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // helper to fetch events when needed inside matches loader
    private Task<EventsResponse> _api_service_GetEventsAsync() => _apiService.GetEventsAsync();

    [RelayCommand]
    private async Task LoadScoutingAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading scouting data (may be large)...";

            int? eventId = (SelectedEvent == null || SelectedEvent.Id ==0) ? null : SelectedEvent.Id;
            var resp = await _apiService.GetAllScoutingDataAsync(eventId: eventId, limit:1000);
            _allScouting.Clear();

            if (resp.Success && resp.Entries != null)
            {
                foreach (var e in resp.Entries.OrderByDescending(e => e.Timestamp))
                    _allScouting.Add(e);

                // Apply filter to update UI collection
                ApplyFilter();

                StatusMessage = $"Loaded {_allScouting.Count} scouting entries";
                await ShowSectionAsync("scouting");
            }
            else
            {
                StatusMessage = resp.Error ?? "Failed to load scouting data";
            }
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadAllAsync()
    {
        // Load all data (events, teams, matches, scouting)
        await LoadEventsAsync();
        await LoadTeamsAsync();
        await LoadMatchesAsync();
        await LoadScoutingAsync();

        StatusMessage = "All data loaded";

        // Show all sections after a full load so the UI displays everything
        ShowEvents = true;
        ShowTeams = true;
        ShowMatches = true;
        ShowScouting = true;
    }
}
