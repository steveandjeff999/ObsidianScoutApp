using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Services;

namespace ObsidianScout.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;
    private readonly IDataPreloadService _dataPreloadService;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string teamNumber = string.Empty;

    [ObservableProperty]
    private string protocol = "https";

    [ObservableProperty]
    private string serverAddress = "beta.obsidianscout.com";

    [ObservableProperty]
    private string serverPort = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool showServerConfig;

    public LoginViewModel(IApiService apiService, ISettingsService settingsService, IDataPreloadService dataPreloadService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
        _dataPreloadService = dataPreloadService;

        LoadServerConfiguration();
    }

    public string PreviewUrl => string.IsNullOrWhiteSpace(ServerPort)
        ? $"{Protocol}://{ServerAddress}"
        : $"{Protocol}://{ServerAddress}:{ServerPort}";

    partial void OnProtocolChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewUrl));
    }

    partial void OnServerAddressChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewUrl));
    }

    partial void OnServerPortChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewUrl));
    }

    private async void LoadServerConfiguration()
    {
        try
        {
            var loadedProtocol = await _settingsService.GetProtocolAsync();
            var loadedAddress = await _settingsService.GetServerAddressAsync();
            var loadedPort = await _settingsService.GetServerPortAsync();

            // Use saved values if present; otherwise default to beta.obsidianscout.com
            Protocol = string.IsNullOrWhiteSpace(loadedProtocol) ? "https" : loadedProtocol;
            ServerAddress = string.IsNullOrWhiteSpace(loadedAddress) ? "beta.obsidianscout.com" : loadedAddress;
            ServerPort = string.IsNullOrWhiteSpace(loadedPort) ? string.Empty : loadedPort;
        }
        catch
        {
            // Ignore and keep defaults
            Protocol = "https";
            ServerAddress = "beta.obsidianscout.com";
            ServerPort = string.Empty;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter username and password";
            return;
        }

        if (string.IsNullOrWhiteSpace(TeamNumber))
        {
            ErrorMessage = "Please enter team number";
            return;
        }

        if (!int.TryParse(TeamNumber, out int teamNum) || teamNum <= 0)
        {
            ErrorMessage = "Please enter a valid team number";
            return;
        }

        IsLoading = true;

        try
        {
            var result = await _apiService.LoginAsync(Username, Password, teamNum);

            if (result.Success)
            {
                System.Diagnostics.Debug.WriteLine("[Login] Login successful, triggering data preload");

                // Persist username and team number for display in AppShell
                try
                {
                    await _settingsService.SetUsernameAsync(Username);
                    await _settingsService.SetTeamNumberAsync(teamNum);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Login] Failed to save user info: {ex}");
                }

                // Trigger data preload in background after successful login
                _ = Task.Run(async () => await _dataPreloadService.PreloadAllDataAsync());

                // Update AppShell authentication state
                if (Shell.Current is AppShell appShell)
                {
                    appShell.UpdateAuthenticationState(true);
                }

                // Navigate to main page
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                // Inform user to verify server configuration and network when login fails
                ErrorMessage = "Login failed. Please check your server configuration and network connection.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleServerConfig()
    {
        ShowServerConfig = !ShowServerConfig;
    }

    [RelayCommand]
    private async Task SaveServerConfigAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerAddress))
        {
            ErrorMessage = "Please enter a server address";
            return;
        }

        // Port is optional now; only validate if provided
        if (!string.IsNullOrWhiteSpace(ServerPort))
        {
            if (!int.TryParse(ServerPort, out int portNumber) || portNumber < 1 || portNumber > 65535)
            {
                ErrorMessage = "Please enter a valid port number (1-65535)";
                return;
            }
        }

        try
        {
            await _settingsService.SetProtocolAsync(Protocol.ToLower());
            await _settingsService.SetServerAddressAsync(ServerAddress.Trim());
            await _settingsService.SetServerPortAsync(ServerPort.Trim());

            ShowServerConfig = false;
            ErrorMessage = "? Server configuration saved";

            // Clear the success message after 3 seconds
            await Task.Delay(3000);
            if (ErrorMessage == "? Server configuration saved")
            {
                ErrorMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"? Error saving configuration: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerAddress))
        {
            ErrorMessage = "Please enter a server address";
            return;
        }

        // Port is optional for testing
        ErrorMessage = string.Empty;
        IsLoading = true;

        try
        {
            // Save current configuration temporarily for testing
            await _settingsService.SetProtocolAsync(Protocol.ToLower());
            await _settingsService.SetServerAddressAsync(ServerAddress.Trim());
            await _settingsService.SetServerPortAsync(ServerPort.Trim());

            var result = await _apiService.HealthCheckAsync();

            if (result.Success)
            {
                ErrorMessage = "? Connection successful";
            }
            else
            {
                ErrorMessage = $"? Connection failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"? Connection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
