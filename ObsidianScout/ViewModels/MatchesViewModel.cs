using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;

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
                foreach (var match in result.Matches
                    .OrderBy(m => m.MatchType)
                    .ThenBy(m => m.MatchNumber))
                {
                    Matches.Add(match);
                }

                // Check if we're in offline mode (using cached data)
                if (!string.IsNullOrEmpty(result.Error) && result.Error.Contains("offline"))
                {
                    IsOfflineMode = true;
                    ErrorMessage = "?? Offline Mode - Using cached data";
                }

                System.Diagnostics.Debug.WriteLine($"? Loaded {Matches.Count} matches");
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
        // You could pass matchId as a parameter if needed
        await Shell.Current.GoToAsync("//ScoutingPage");
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
