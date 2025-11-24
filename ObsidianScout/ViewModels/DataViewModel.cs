using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ObsidianScout.ViewModels;

public partial class DataViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _loadCancellationTokenSource;

    // Backing full lists used for filtering
    private readonly List<Event> _allEvents = new();
  private readonly List<Team> _allTeams = new();
    private readonly List<MatchCard> _allMatches = new();
    private readonly List<ScoutingEntry> _allScouting = new();

    // Track consecutive 401 errors
    private int _consecutive401Count = 0;
    private const int MAX_401_BEFORE_ALERT = 3;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool hasAuthError;

    [ObservableProperty]
    private string authErrorMessage = string.Empty;

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

    // Loading progress indicators
    [ObservableProperty]
  private bool isLoadingEvents;

    [ObservableProperty]
    private bool isLoadingTeams;

  [ObservableProperty]
    private bool isLoadingMatches;

    [ObservableProperty]
    private bool isLoadingScouting;

    public DataViewModel(IApiService apiService, ISettingsService settingsService)
  {
        _apiService = apiService;
     _settingsService = settingsService;
    }

    // Called by source generator when SelectedEvent changes
    partial void OnSelectedEventChanged(Event? oldValue, Event? newValue)
    {
        if (newValue == null) return;

 // Prevent auto-load when a global LoadAll is in progress or when individual loads are already running
 if (IsLoading || IsLoadingTeams || IsLoadingMatches)
 {
 System.Diagnostics.Debug.WriteLine("[DataViewModel] Skipping auto-load on SelectedEvent change because a load is already in progress");
 return;
 }

 // Run sequential loads in background to avoid re-entrancy problems
 _ = Task.Run(async () =>
 {
 try
 {
 await LoadTeamsAsync();
 await LoadMatchesAsync();
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[DataViewModel] Auto-load on SelectedEvent changed failed: {ex.Message}");
 }
 });
    }

    // Called by source generator when Query changes
    partial void OnQueryChanged(string oldValue, string newValue)
    {
        // Debounce filter application
        _ = Task.Run(async () =>
        {
      await Task.Delay(300); // Wait 300ms before filtering
         if (Query == newValue) // Only filter if query hasn't changed
 {
       await MainThread.InvokeOnMainThreadAsync(() => ApplyFilter());
     }
        });
    }

    private void ApplyFilter()
    {
     try
      {
            var q = string.IsNullOrWhiteSpace(Query) ? string.Empty : Query.Trim().ToLowerInvariant();

   // Filter events
     Events.Clear();
   foreach (var ev in _allEvents.Take(100)) // Limit to prevent UI freeze
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
            foreach (var t in _allTeams.Take(100)) // Limit to prevent UI freeze
            {
              if (string.IsNullOrEmpty(q) || MatchesTeam(t, q))
        Teams.Add(t);
    }

            // Filter matches
            Matches.Clear();
        foreach (var m in _allMatches.Take(100)) // Limit to prevent UI freeze
            {
    if (string.IsNullOrEmpty(q) || MatchesMatch(m, q))
           Matches.Add(m);
        }

            // Filter scouting
            Scouting.Clear();
 foreach (var s in _allScouting.Take(100)) // Limit to prevent UI freeze
      {
      if (string.IsNullOrEmpty(q) || MatchesScouting(s, q))
        Scouting.Add(s);
     }
        }
 catch (Exception ex)
        {
 System.Diagnostics.Debug.WriteLine($"[DataViewModel] Filter error: {ex.Message}");
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

    private bool Check401Error(string? error)
    {
    if (error != null && (error.Contains("401") || error.Contains("Unauthorized") || error.Contains("authentication")))
        {
            _consecutive401Count++;
            System.Diagnostics.Debug.WriteLine($"[DataViewModel] 401 error detected (count: {_consecutive401Count})");

    if (_consecutive401Count >= MAX_401_BEFORE_ALERT)
        {
  HasAuthError = true;
          AuthErrorMessage = "?? Server auth token rejected\n\nPlease log out and back in";
      MainThread.BeginInvokeOnMainThread(async () =>
   {
          await Shell.Current.DisplayAlert(
           "Authentication Error",
          "Your authentication token has been rejected by the server multiple times.\n\nPlease log out and log back in to continue.",
 "OK");
    });
  return true;
         }
      }
        else
        {
   // Reset counter on successful request
      _consecutive401Count = 0;
      }
        return false;
    }

    [RelayCommand]
    private async Task LoadEventsAsync()
    {
        if (IsLoadingEvents) return;

  try
        {
    IsLoadingEvents = true;
       IsLoading = true;
   StatusMessage = "Loading events...";

 var resp = await _apiService.GetEventsAsync();
   
        if (Check401Error(resp.Error))
       return;

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
        StartDate = DateTime.MinValue,
     EndDate = DateTime.MinValue,
          Timezone = string.Empty,
 TeamCount = 0
        };

         _allEvents.Add(all);

                foreach (var ev in resp.Events)
        _allEvents.Add(ev);

     // Apply filter to update UI collection on main thread
   await MainThread.InvokeOnMainThreadAsync(() =>
         {
        ApplyFilter();
         // Default to All Events if nothing selected
       if (SelectedEvent == null)
    SelectedEvent = Events.FirstOrDefault();
        });

    StatusMessage = $"? Loaded {_allEvents.Count - 1} events";
     await ShowSectionAsync("events");
          }
            else
            {
             StatusMessage = resp.Error ?? "Failed to load events";
            }
     }
     catch (Exception ex)
   {
            System.Diagnostics.Debug.WriteLine($"[DataViewModel] LoadEvents error: {ex.Message}");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
    IsLoadingEvents = false;
    IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadTeamsAsync()
    {
        if (IsLoadingTeams) return;

      try
        {
  IsLoadingTeams = true;
            IsLoading = true;
          StatusMessage = "Loading teams...";

            // Small delay to prevent UI freeze
       await Task.Delay(100);

    // If selected event is "All Events" (Id ==0) treat as no filter
       int? eventId = (SelectedEvent == null || SelectedEvent.Id == 0) ? null : SelectedEvent.Id;

            var resp = await _apiService.GetTeamsAsync(eventId: eventId, limit: 500);

            if (Check401Error(resp.Error))
                return;

         _allTeams.Clear();

     if (resp.Success && resp.Teams != null)
   {
     foreach (var t in resp.Teams.OrderBy(t => t.TeamNumber))
      _allTeams.Add(t);

        // Apply filter to update UI collection on main thread
     await MainThread.InvokeOnMainThreadAsync(() => ApplyFilter());

      StatusMessage = $"? Loaded {_allTeams.Count} teams";
      await ShowSectionAsync("teams");
            }
          else
            {
     StatusMessage = resp.Error ?? "Failed to load teams";
          }
        }
 catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataViewModel] LoadTeams error: {ex.Message}");
     StatusMessage = $"Error: {ex.Message}";
}
     finally
    {
            IsLoadingTeams = false;
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMatchesAsync()
    {
    if (IsLoadingMatches) return;

 try
        {
IsLoadingMatches = true;
        IsLoading = true;
            StatusMessage = "Loading matches...";

            // Small delay to prevent UI freeze
            await Task.Delay(100);

 _allMatches.Clear();

  if (SelectedEvent != null && SelectedEvent.Id != 0)
    {
                var mResp = await _apiService.GetMatchesAsync(SelectedEvent.Id);
    
      if (Check401Error(mResp.Error))
        return;

       if (mResp.Success && mResp.Matches != null)
      {
   foreach (var m in mResp.Matches.OrderBy(mm => mm.MatchNumber))
      _allMatches.Add(new MatchCard { EventName = SelectedEvent.Name, Match = m });
 }
    }
            else
            {
     // All events - load matches for all events (but limit to prevent overload)
      var evResp = await _apiService.GetEventsAsync();
                
              if (Check401Error(evResp.Error))
       return;

   if (evResp.Success && evResp.Events != null)
  {
 var eventsToLoad = evResp.Events.Take(5).ToList(); // Limit to 5 events to prevent overload
    StatusMessage = $"Loading matches from {eventsToLoad.Count} events...";

           foreach (var ev in eventsToLoad)
      {
     var mResp = await _apiService.GetMatchesAsync(ev.Id);
 
         if (Check401Error(mResp.Error))
      return;

     if (mResp.Success && mResp.Matches != null)
         {
               foreach (var m in mResp.Matches.OrderBy(mm => mm.MatchNumber).Take(50)) // Limit matches per event
       _allMatches.Add(new MatchCard { EventName = ev.Name, Match = m });
    }

 // Small delay between requests to prevent server overload
       await Task.Delay(200);
          }
 }
            }

      // Apply filter after populating all matches on main thread
await MainThread.InvokeOnMainThreadAsync(() => ApplyFilter());

            StatusMessage = $"? Loaded {_allMatches.Count} matches";
            await ShowSectionAsync("matches");
        }
        catch (Exception ex)
        {
          System.Diagnostics.Debug.WriteLine($"[DataViewModel] LoadMatches error: {ex.Message}");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
      {
     IsLoadingMatches = false;
            IsLoading = false;
   }
    }

    [RelayCommand]
    private async Task LoadScoutingAsync()
    {
  if (IsLoadingScouting) return;

        try
        {
            IsLoadingScouting = true;
            IsLoading = true;
            StatusMessage = "Loading scouting data...";

            // Small delay to prevent UI freeze
   await Task.Delay(100);

     int? eventId = (SelectedEvent == null || SelectedEvent.Id == 0) ? null : SelectedEvent.Id;
            var resp = await _apiService.GetAllScoutingDataAsync(eventId: eventId, limit: 500);

      if (Check401Error(resp.Error))
      return;

            _allScouting.Clear();

            if (resp.Success && resp.Entries != null)
            {
    foreach (var e in resp.Entries.OrderByDescending(e => e.Timestamp))
    _allScouting.Add(e);

                // Apply filter to update UI collection on main thread
      await MainThread.InvokeOnMainThreadAsync(() => ApplyFilter());

    StatusMessage = $"? Loaded {_allScouting.Count} scouting entries";
     await ShowSectionAsync("scouting");
            }
            else
      {
   StatusMessage = resp.Error ?? "Failed to load scouting data";
      }
        }
     catch (Exception ex)
        {
    System.Diagnostics.Debug.WriteLine($"[DataViewModel] LoadScouting error: {ex.Message}");
 StatusMessage = $"Error: {ex.Message}";
      }
        finally
        {
     IsLoadingScouting = false;
            IsLoading = false;
        }
    }

    [RelayCommand]
 public async Task LoadAllAsync()
 {
 // Prevent starting multiple concurrent full-loads
 if (IsLoading)
 {
 System.Diagnostics.Debug.WriteLine("[DataViewModel] LoadAllAsync called but already loading - ignoring duplicate call");
 StatusMessage = "Already loading...";
 return;
 }

 // Cancel any existing load operation
 _loadCancellationTokenSource?.Cancel();
 _loadCancellationTokenSource = new CancellationTokenSource();
 var token = _loadCancellationTokenSource.Token;

 try
 {
 // Reset auth error state
 HasAuthError = false;
 AuthErrorMessage = string.Empty;
 _consecutive401Count =0;

 IsLoading = true;
 StatusMessage = "Loading all data...";

 // Load sequentially with delays to prevent server overload and UI freeze
 await LoadEventsAsync();
 if (token.IsCancellationRequested) return;

 await Task.Delay(500, token);

 await LoadTeamsAsync();
 if (token.IsCancellationRequested) return;

 await Task.Delay(500, token);

 await LoadMatchesAsync();
 if (token.IsCancellationRequested) return;

 await Task.Delay(500, token);

 await LoadScoutingAsync();
 if (token.IsCancellationRequested) return;

 StatusMessage = "? All data loaded successfully";

 // Show all sections after a full load
 ShowEvents = true;
 ShowTeams = true;
 ShowMatches = true;
 ShowScouting = true;
 }
 catch (OperationCanceledException)
 {
 StatusMessage = "Load cancelled";
 System.Diagnostics.Debug.WriteLine("[DataViewModel] Load operation cancelled");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[DataViewModel] LoadAll error: {ex.Message}");
 StatusMessage = $"Error: {ex.Message}";
 }
 finally
 {
 IsLoading = false;
 // Dispose cancellation token source to avoid reuse
 try { _loadCancellationTokenSource?.Dispose(); } catch { }
 _loadCancellationTokenSource = null;
 }
 }

    [RelayCommand]
    private void ClearAuthError()
    {
        HasAuthError = false;
    AuthErrorMessage = string.Empty;
  _consecutive401Count = 0;
    }
}
