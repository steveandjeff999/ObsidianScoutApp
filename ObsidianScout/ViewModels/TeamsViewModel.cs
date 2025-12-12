using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;

namespace ObsidianScout.ViewModels;

public partial class TeamsViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private List<Team> _allTeamsList = new();

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

    [ObservableProperty]
    private string searchText = string.Empty;

    public TeamsViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilterAndSort();
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
                _allTeamsList = result.Teams;
                ApplyFilterAndSort();

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

    private void ApplyFilterAndSort()
    {
        if (_allTeamsList == null) return;

        // 1. Merge duplicate teams (DistinctBy TeamNumber)
        // 2. Sort by TeamNumber (smallest to greatest)
        var processedTeams = _allTeamsList
            .GroupBy(t => t.TeamNumber)
            .Select(g => g.First())
            .OrderBy(t => t.TeamNumber)
            .AsEnumerable();

        // 3. Filter by SearchText
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            processedTeams = processedTeams.Where(t => 
                t.TeamNumber.ToString().Contains(SearchText) || 
                (t.TeamName != null && t.TeamName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (t.Location != null && t.Location.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
        }

        Teams.Clear();
        foreach (var team in processedTeams)
        {
            Teams.Add(team);
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
