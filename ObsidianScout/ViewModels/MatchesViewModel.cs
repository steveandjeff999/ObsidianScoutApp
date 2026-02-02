using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace ObsidianScout.ViewModels;

[QueryProperty(nameof(EventId), "eventId")]
public partial class MatchesViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private List<Match> _allMatchesList = new();

    [ObservableProperty]
  private int eventId;

    [ObservableProperty]
    private Event? selectedEvent;

    [ObservableProperty]
    private ObservableCollection<Match> matches = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isOfflineMode;

    [ObservableProperty]
    private string searchText = string.Empty;

    public MatchesViewModel(IApiService apiService)
    {
      _apiService = apiService;
    }

    partial void OnEventIdChanged(int value)
    {
        if (value > 0)
  {
   _ = LoadMatchesAsync();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    public async Task InitializeAsync()
    {
    // If no event ID set, try to load current event from game config
 if (EventId == 0)
     {
            await LoadCurrentEventAsync();
     }
        else
        {
await LoadMatchesAsync();
    }
    }


  private async Task LoadCurrentEventAsync()
  {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== [MatchesViewModel] LoadCurrentEventAsync START ===");
            
            // Get game config to find current event
            var configResponse = await _apiService.GetGameConfigAsync();
            System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] GameConfig Success: {configResponse.Success}");
            
            if (configResponse.Success && configResponse.Config != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] GameConfig.Season: {configResponse.Config.Season}");
                System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] GameConfig.CurrentEventCode: '{configResponse.Config.CurrentEventCode}'");
            }
            
  if (configResponse.Success && configResponse.Config != null && !string.IsNullOrEmpty(configResponse.Config.CurrentEventCode))
        {
         // Get events and find the current one
    var eventsResult = await _apiService.GetEventsAsync();
    System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Events Success: {eventsResult.Success}, Count: {eventsResult.Events?.Count ?? 0}");
    
         if (eventsResult.Success && eventsResult.Events?.Any() == true)
           {
     // Log all available events
     System.Diagnostics.Debug.WriteLine("[MatchesViewModel] Available events:");
     foreach (var e in eventsResult.Events)
     {
         System.Diagnostics.Debug.WriteLine($"  - ID: {e.Id}, Code: '{e.Code}', Name: '{e.Name}'");
     }

     // Build the event code - check if it already includes the season prefix
     var rawEventCode = configResponse.Config.CurrentEventCode;
     string seasonCode;

     System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Raw event code: '{rawEventCode}'");
     System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Season: {configResponse.Config.Season}");

     if (configResponse.Config.Season > 0)
     {
         var seasonStr = configResponse.Config.Season.ToString();
         System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Season string: '{seasonStr}'");
         
         // If the event code already starts with the season, use it as-is
         bool alreadyHasSeason = rawEventCode.StartsWith(seasonStr, StringComparison.OrdinalIgnoreCase);
         System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Already has season prefix: {alreadyHasSeason}");
         
         if (alreadyHasSeason)
         {
             seasonCode = rawEventCode;
         }
         else
         {
             seasonCode = $"{configResponse.Config.Season}{rawEventCode}";
         }
     }
     else
     {
         seasonCode = rawEventCode;
     }

     System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Final seasonCode to search: '{seasonCode}'");

     // Try exact match with season+code (primary)
     var currentEvent = eventsResult.Events.FirstOrDefault(e => 
          string.Equals(e.Code, seasonCode, StringComparison.OrdinalIgnoreCase));
     System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Match on seasonCode '{seasonCode}': {(currentEvent != null ? $"ID={currentEvent.Id}, Code='{currentEvent.Code}'" : "NULL")}");
     
     // Fallback to raw code only
     if (currentEvent == null)
     {
         currentEvent = eventsResult.Events.FirstOrDefault(e => string.Equals(e.Code, rawEventCode, StringComparison.OrdinalIgnoreCase));
         System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Match on rawEventCode '{rawEventCode}': {(currentEvent != null ? $"ID={currentEvent.Id}, Code='{currentEvent.Code}'" : "NULL")}");
     }
            
          if (currentEvent != null)
          {
         System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] *** SELECTED EVENT: ID={currentEvent.Id}, Code='{currentEvent.Code}', Name='{currentEvent.Name}' ***");
         EventId = currentEvent.Id;
            SelectedEvent = currentEvent;
            System.Diagnostics.Debug.WriteLine("=== [MatchesViewModel] LoadCurrentEventAsync END (found event) ===");
  return;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] *** NO EVENT MATCHED! ***");
        }
     }
     }
          
    // Fallback: load first available event
    System.Diagnostics.Debug.WriteLine("[MatchesViewModel] No event found from config, falling back to first event...");
          var fallbackEventsResult = await _apiService.GetEventsAsync();
       if (fallbackEventsResult.Success && fallbackEventsResult.Events?.Any() == true)
    {
         var firstEvent = fallbackEventsResult.Events.First();
       EventId = firstEvent.Id;
              SelectedEvent = firstEvent;
              System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Fallback to first event: ID={firstEvent.Id}, Code='{firstEvent.Code}'");
      }
      
      System.Diagnostics.Debug.WriteLine("=== [MatchesViewModel] LoadCurrentEventAsync END ===");
        }
    catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load current event: {ex.Message}");
        ErrorMessage = "Failed to load current event";
        }
    }

    private async Task LoadMatchesAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        IsOfflineMode = false;

        try
        {
            System.Diagnostics.Debug.WriteLine($"=== LOADING MATCHES FOR EVENT {EventId} ===");

            var result = await _apiService.GetMatchesAsync(EventId);

            if (result.Success && result.Matches != null)
            {
                // Sort by match type order first, then by match number
                _allMatchesList = result.Matches
                    .OrderBy(m => m.MatchTypeOrder)
                    .ThenBy(m => m.MatchNumber)
                    .ToList();

                ApplyFilter();

                // Check if we're in offline mode (using cached data)
                if (!string.IsNullOrEmpty(result.Error) && result.Error.Contains("offline"))
                {
                    IsOfflineMode = true;
                    ErrorMessage = "?? Offline Mode - Using cached data";
                }

                System.Diagnostics.Debug.WriteLine($"? Loaded {Matches.Count} matches (sorted by type and number)");
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to load matches";
                IsOfflineMode = true;
                System.Diagnostics.Debug.WriteLine($"? Failed to load matches: {ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            IsOfflineMode = true;
            System.Diagnostics.Debug.WriteLine($"? Exception loading matches: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    private void ApplyFilter()
    {
        if (_allMatchesList == null) return;

        var processedMatches = _allMatchesList.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            processedMatches = processedMatches.Where(m => 
                m.MatchNumber.ToString().Contains(SearchText) || 
                (m.MatchType != null && m.MatchType.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (m.RedAlliance != null && m.RedAlliance.Contains(SearchText)) ||
                (m.BlueAlliance != null && m.BlueAlliance.Contains(SearchText)));
        }

        Matches.Clear();
        foreach (var match in processedMatches)
        {
            Matches.Add(match);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
   IsRefreshing = true;
 await LoadMatchesAsync();
    }

    [RelayCommand]
    private async Task MatchSelectedAsync(Match match)
    {
 if (match == null)
 return;

 // Navigate to scouting page with match pre-selected
 try
 {
 // Use relative route instead of absolute global route to avoid Shell routing issue
 await Shell.Current.GoToAsync("ScoutingPage");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[MatchesViewModel] Navigation to ScoutingPage failed: {ex.Message}");
 // Fallback: attempt simple relative navigation
 try
 {
 await Shell.Current.GoToAsync("ScoutingPage");
 }
 catch
 {
 // swallow - nothing more we can do here
 }
 }
 }

    [RelayCommand]
 private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
