using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;

namespace ObsidianScout.ViewModels;

public partial class TeamsViewModel : ObservableObject
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<Team> teams = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isOfflineMode;

    public TeamsViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task InitializeAsync()
    {
        await LoadTeamsAsync();
    }

    [RelayCommand]
    private async Task LoadTeamsAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        IsOfflineMode = false;

        try
        {
            var result = await _apiService.GetTeamsAsync();

            if (result.Success)
            {
                Teams.Clear();
                foreach (var team in result.Teams)
                {
                    Teams.Add(team);
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
                ErrorMessage = "Failed to load teams";
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
        await LoadTeamsAsync();
    }

    [RelayCommand]
    private async Task TeamSelectedAsync(Team team)
    {
        if (team == null)
            return;

        await Shell.Current.GoToAsync($"TeamDetailsPage?teamId={team.Id}");
    }
}
