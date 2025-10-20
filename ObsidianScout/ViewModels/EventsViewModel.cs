using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;

namespace ObsidianScout.ViewModels;

public partial class EventsViewModel : ObservableObject
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<Event> events = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isOfflineMode;

    public EventsViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task InitializeAsync()
    {
        await LoadEventsAsync();
    }

    [RelayCommand]
    private async Task LoadEventsAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        IsOfflineMode = false;

        try
        {
            var result = await _apiService.GetEventsAsync();

            if (result.Success)
            {
                Events.Clear();
                foreach (var evt in result.Events)
                {
                    Events.Add(evt);
                }

                // Check if we're in offline mode (using cached data)
                if (!string.IsNullOrEmpty(result.Error) && result.Error.Contains("offline"))
                {
                    IsOfflineMode = true;
                    ErrorMessage = "?? Offline Mode - Using cached data";
                }
            }
            else
            {
                ErrorMessage = "Failed to load events";
                IsOfflineMode = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            IsOfflineMode = true;
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
        await LoadEventsAsync();
    }

    [RelayCommand]
    private async Task EventSelectedAsync(Event evt)
    {
        if (evt == null)
            return;

        await Shell.Current.GoToAsync($"MatchesPage?eventId={evt.Id}");
    }
}
