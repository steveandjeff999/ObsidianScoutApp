using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ObsidianScout.Services;
using ObsidianScout.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ObsidianScout.ViewModels;

public class ManageUserCreateViewModel : ObservableObject
{
    private readonly IApiService _api_service;
    private readonly ISettingsService _settings_service;

    public ManageUserCreateViewModel(IApiService apiService, ISettingsService settingsService)
    {
        _api_service = apiService;
        _settings_service = settingsService;
        CreateCommand = new Command(async () => await CreateAsync());
        CancelCommand = new Command(async () => await CancelAsync());
    }

    private string _username = string.Empty;
    public string Username { get => _username; set => SetProperty(ref _username, value); }

    private string _password = string.Empty;
    public string Password { get => _password; set => SetProperty(ref _password, value); }

    private string _confirmPassword = string.Empty;
    public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }

    private string _teamNumber = string.Empty;
    public string TeamNumber { get => _teamNumber; set => SetProperty(ref _teamNumber, value); }

    private string _email = string.Empty;
    public string Email { get => _email; set => SetProperty(ref _email, value); }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

    private string _errorMessage = string.Empty;
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    public async Task CreateAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(TeamNumber))
        {
            ErrorMessage = "Please fill username, password and team number";
            return;
        }

        if (!int.TryParse(TeamNumber, out var tn) || tn <= 0)
        {
            ErrorMessage = "Please enter a valid team number";
            return;
        }

        if (!string.IsNullOrEmpty(ConfirmPassword) && ConfirmPassword != Password)
        {
            ErrorMessage = "Password and confirm password do not match";
            return;
        }

        IsLoading = true;
        try
        {
            // Use the public register endpoint for creating scoped scout accounts
            var resp = await _api_service.PublicRegisterAsync(Username.Trim(), Password, string.IsNullOrWhiteSpace(ConfirmPassword) ? null : ConfirmPassword, tn, string.IsNullOrWhiteSpace(Email) ? null : Email.Trim());
            // Note: PublicRegisterAsync mirrors server register behavior and DOES NOT persist tokens/settings
            
            // If you need admin-created users with roles, switch back to CreateAdminUserAsync and provide roles
            // var req = new CreateUserRequest { Username = Username.Trim(), Password = Password, Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(), ScoutingTeamNumber = tn };
            // var resp2 = await _api_service.CreateAdminUserAsync(req);
            // use resp2 for admin flows

            var resp2 = resp;
            if (resp != null && resp.Success)
            {
                await Application.Current.MainPage.DisplayAlert("Success", "User created", "OK");
                await Shell.Current.GoToAsync("..", true);
            }
            else
            {
                ErrorMessage = resp2?.Error ?? "Failed to create user";
            }
        }
        catch (System.Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..", true);
    }
}
