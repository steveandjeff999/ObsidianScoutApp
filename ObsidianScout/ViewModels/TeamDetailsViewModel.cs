using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;

namespace ObsidianScout.ViewModels;

[QueryProperty(nameof(TeamId), "teamId")]
public partial class TeamDetailsViewModel : ObservableObject
{
    private readonly IApiService _apiService;

    [ObservableProperty]
    private int teamId;

    [ObservableProperty]
    private Team? selectedTeam;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public TeamDetailsViewModel(IApiService apiService)
    {
        _apiService = apiService;
    }

    partial void OnTeamIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadTeamDetailsAsync();
        }
    }

    private async Task LoadTeamDetailsAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            // Get all teams and find the specific one
            var result = await _apiService.GetTeamsAsync();

            if (result.Success && result.Teams != null)
            {
                SelectedTeam = result.Teams.FirstOrDefault(t => t.Id == TeamId);
                
                if (SelectedTeam == null)
                {
                    ErrorMessage = "Team not found";
                }
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to load team details";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error loading team details: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
