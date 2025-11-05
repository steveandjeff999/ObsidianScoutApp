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
            // Get game config to find current event
            var configResponse = await _apiService.GetGameConfigAsync();
  if (configResponse.Success && configResponse.Config != null && !string.IsNullOrEmpty(configResponse.Config.CurrentEventCode))
        {
         // Get events and find the current one
    var eventsResult = await _apiService.GetEventsAsync();
         if (eventsResult.Success && eventsResult.Events?.Any() == true)
           {
     var currentEvent = eventsResult.Events.FirstOrDefault(e => 
          e.Code.Equals(configResponse.Config.CurrentEventCode, StringComparison.OrdinalIgnoreCase));
            
          if (currentEvent != null)
          {
         EventId = currentEvent.Id;
            SelectedEvent = currentEvent;
  return;
        }
     }
     }
          
    // Fallback: load first available event
          var fallbackEventsResult = await _apiService.GetEventsAsync();
       if (fallbackEventsResult.Success && fallbackEventsResult.Events?.Any() == true)
    {
         var firstEvent = fallbackEventsResult.Events.First();
       EventId = firstEvent.Id;
              SelectedEvent = firstEvent;
      }
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
        Matches.Clear();
        
            // Sort by match type order first, then by match number
     var sortedMatches = result.Matches
     .OrderBy(m => m.MatchTypeOrder)
        .ThenBy(m => m.MatchNumber)
               .ToList();

            foreach (var match in sortedMatches)
       {
     Matches.Add(match);
         }

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
      await Shell.Current.GoToAsync("//ScoutingPage");
    }

    [RelayCommand]
 private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
