using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;

namespace ObsidianScout.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private User? currentUser;

    [ObservableProperty]
    private string welcomeMessage = "Welcome to ObsidianScout";

    [ObservableProperty]
    private bool isLoading;

    public MainViewModel(IApiService apiService, ISettingsService settingsService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
        
        LoadUserData();
    }

    private async void LoadUserData()
    {
        var token = await _settingsService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            var result = await _apiService.VerifyTokenAsync();
            if (result.Success && result.Data != null)
            {
                CurrentUser = result.Data;
                WelcomeMessage = $"Welcome, {CurrentUser.Username}!";
            }
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _settingsService.ClearAuthDataAsync();
        
        // Update AppShell authentication state
        if (Shell.Current is AppShell appShell)
        {
            appShell.UpdateAuthenticationState(false);
        }
        
        await Shell.Current.GoToAsync("//LoginPage");
    }

    [RelayCommand]
    private async Task NavigateToTeamsAsync()
    {
        await Shell.Current.GoToAsync("TeamsPage");
    }

    [RelayCommand]
    private async Task NavigateToEventsAsync()
    {
        await Shell.Current.GoToAsync("EventsPage");
    }

    [RelayCommand]
    private async Task NavigateToScoutingAsync()
    {
        await Shell.Current.GoToAsync("ScoutingPage");
    }
}
