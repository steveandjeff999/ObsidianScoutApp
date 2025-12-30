using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Services;
using Microsoft.Maui.ApplicationModel;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using System.Linq;

namespace ObsidianScout.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ICacheService _cacheService;
    private readonly ISettingsService _settingsService;
    private readonly IDataPreloadService _preloadService;
    private readonly INotificationPollingService? _notificationPollingService;
    private readonly ILocalNotificationService? _localNotificationService;
    private readonly IApiService? _apiService;

    [ObservableProperty]
    private bool isDarkMode;

    [ObservableProperty]
    private string cacheStatus = "Cache is active";

    [ObservableProperty]
    private string cacheAge = string.Empty;

    [ObservableProperty]
    private string cacheLastUpdated = string.Empty;

    [ObservableProperty]
    private bool isClearing;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isCaching;

    // New: delay in seconds for delayed test notification
    [ObservableProperty]
    private int notificationDelaySeconds = 60;

    [ObservableProperty]
    private bool hasManagementAccess;

    [ObservableProperty]
    private string appVersion = string.Empty;

    // Explicit property for OfflineMode so we can persist immediately without relying on source-gen timing
    private bool _isOfflineMode;
    public bool IsOfflineMode
    {
        get => _isOfflineMode;
        set
        {
            if (_isOfflineMode == value) return;
            _isOfflineMode = value;
            OnPropertyChanged();
            // Persist change (fire-and-forget)
            _ = SetOfflineModeAsync(value);
        }
    }

    // New: Notifications enabled with immediate persistence
    private bool _isNotificationsEnabled;
    public bool IsNotificationsEnabled
    {
        get => _isNotificationsEnabled;
        set
        {
            if (_isNotificationsEnabled == value) return;
            _isNotificationsEnabled = value;
            OnPropertyChanged();
            _ = SetNotificationsEnabledAsync(value);
        }
    }

    // New: Network timeout in seconds with immediate persistence
    private int _networkTimeoutSeconds = 8;
    public int NetworkTimeoutSeconds
    {
        get => _networkTimeoutSeconds;
        set
        {
            if (_networkTimeoutSeconds == value) return;
            // Clamp between 5 and 60 seconds
            var clamped = Math.Clamp(value, 5, 60);
            _networkTimeoutSeconds = clamped;
            OnPropertyChanged();
            _ = SetNetworkTimeoutAsync(clamped);
        }
    }

    public SettingsViewModel(ICacheService cacheService, ISettingsService settingsService, IDataPreloadService preloadService, INotificationPollingService? notificationPollingService = null, ILocalNotificationService? localNotificationService = null, IApiService? apiService = null)
    {
        _cacheService = cacheService;
        _settingsService = settingsService;
        _preloadService = preloadService;
        _notificationPollingService = notificationPollingService;
        _localNotificationService = localNotificationService;
        _apiService = apiService;

        LoadThemePreference();
        _ = UpdateCacheStatusAsync();
        _ = LoadOfflineModeAsync();
        _ = CheckManagementAccessAsync();
        _ = LoadNotificationsPreferenceAsync();
        _ = LoadNetworkTimeoutAsync();

        try
        {
            // Read version from platform manifest / package info
            var version = AppInfo.VersionString ?? string.Empty;
            var build = AppInfo.BuildString ?? string.Empty;
            AppVersion = string.IsNullOrEmpty(build) ? version : $"{version} (build {build})";
        }
        catch
        {
            AppVersion = string.Empty;
        }
    }

    private async Task CheckManagementAccessAsync()
    {
        try
        {
            var roles = await _settingsService.GetUserRolesAsync();
            HasManagementAccess = roles.Any(r =>
       r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
        r.Equals("superadmin", StringComparison.OrdinalIgnoreCase) ||
      r.Equals("management", StringComparison.OrdinalIgnoreCase) ||
         r.Equals("manager", StringComparison.OrdinalIgnoreCase));

            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] HasManagementAccess: {HasManagementAccess}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to check management access: {ex.Message}");
            HasManagementAccess = false;
        }
    }

    private async void LoadThemePreference()
    {
        var theme = await _settingsService.GetThemeAsync();
        IsDarkMode = theme == "Dark";
    }

    private async Task LoadOfflineModeAsync()
    {
        try
        {
            IsOfflineMode = await _settingsService.GetOfflineModeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load offline mode: {ex.Message}");
        }
    }

    private async Task SetOfflineModeAsync(bool enabled)
    {
        try
        {
            await _settingsService.SetOfflineModeAsync(enabled);
            StatusMessage = enabled ? "Offline mode enabled" : "Offline mode disabled";
            await Task.Delay(1500);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to set offline mode: {ex.Message}";
        }
    }

    private async Task UpdateCacheStatusAsync()
    {
        try
        {
            var hasCache = await _cacheService.HasCachedDataAsync();
            // Use preload key timestamps for overall cache metadata
            var created = await _cacheService.GetCacheCreatedAsync("cache_last_preload");
            var lastUpdated = await _cacheService.GetCacheLastUpdatedAsync("cache_last_preload");

            if (hasCache && (created.HasValue || lastUpdated.HasValue))
            {
                // Compute reference time (prefer lastUpdated), ensure UTC kind
                var reference = (lastUpdated ?? created.Value).ToUniversalTime();
                var age = DateTime.UtcNow - reference;
                CacheStatus = "Cache active";
                CacheAge = $"{age.TotalHours:F1} hours old";
                CacheLastUpdated = lastUpdated.HasValue ? lastUpdated.Value.ToLocalTime().ToString("g") : "Unknown";
            }
            else
            {
                CacheStatus = "No cached data";
                CacheAge = string.Empty;
                CacheLastUpdated = string.Empty;
            }
        }
        catch
        {
            CacheStatus = "Unable to check cache status";
            CacheAge = string.Empty;
            CacheLastUpdated = string.Empty;
        }
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        _ = ChangeThemeAsync(value);
    }

    private async Task ChangeThemeAsync(bool isDark)
    {
        try
        {
            var theme = isDark ? "Dark" : "Light";
            await _settingsService.SetThemeAsync(theme);

            // Apply theme immediately
            if (Application.Current != null)
            {
                Application.Current.UserAppTheme = isDark ? AppTheme.Dark : AppTheme.Light;
            }

            StatusMessage = $"? Theme changed to {theme} mode";
            await Task.Delay(2000);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"? Failed to change theme: {ex.Message}";
        }
    }

    private async Task LoadNotificationsPreferenceAsync()
    {
        try
        {
            var enabled = await _settingsService.GetNotificationsEnabledAsync();
            IsNotificationsEnabled = enabled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to load notifications preference: {ex.Message}");
            IsNotificationsEnabled = true;
        }
    }

    private async Task SetNotificationsEnabledAsync(bool enabled)
    {
        try
        {
            await _settingsService.SetNotificationsEnabledAsync(enabled);
            StatusMessage = enabled ? "Notifications enabled" : "Notifications disabled";
            await Task.Delay(1200);
            StatusMessage = string.Empty;

            // If notification polling service is present, start/stop accordingly
            try
            {
                if (_notificationPollingService != null)
                {
                    if (enabled)
                        _notificationPollingService.Start();
                    else
                        _notificationPollingService.Stop();
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to set notifications preference: {ex.Message}";
        }
    }

    private async Task LoadNetworkTimeoutAsync()
    {
        try
        {
            var timeout = await _settingsService.GetNetworkTimeoutAsync();
            NetworkTimeoutSeconds = timeout;
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded network timeout: {timeout}s");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to load network timeout: {ex.Message}");
            NetworkTimeoutSeconds = 8; // Default
        }
    }

    private async Task SetNetworkTimeoutAsync(int timeoutSeconds)
    {
        try
        {
            await _settingsService.SetNetworkTimeoutAsync(timeoutSeconds);

            // Update the HttpClient timeout in ApiService
            if (_apiService != null)
            {
                await _apiService.UpdateHttpClientTimeoutAsync();
            }

            StatusMessage = $"Network timeout set to {timeoutSeconds}s";
            await Task.Delay(1500);
            StatusMessage = string.Empty;
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Network timeout saved: {timeoutSeconds}s");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to set network timeout: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to save network timeout: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        if (IsClearing) return;

        IsClearing = true;
        StatusMessage = "Clearing cache...";

        try
        {
            await _cacheService.ClearAllCacheAsync();
            CacheStatus = "No cached data";
            CacheAge = string.Empty;
            CacheLastUpdated = string.Empty;
            StatusMessage = "? Cache cleared successfully";

            await Task.Delay(2000);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"? Failed to clear cache: {ex.Message}";
        }
        finally
        {
            IsClearing = false;
        }
    }

    [RelayCommand]
    private async Task CacheAllAsync()
    {
        if (IsCaching) return;

        try
        {
            IsCaching = true;
            StatusMessage = "Caching all data...";

            // Force a full preload (bypass token check) so user-initiated cache all runs even if not logged in
            await _preloadService.PreloadAllDataAsync(force: true);

            // Wait for background preload to finish (with timeout)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_preloadService.IsPreloading && sw.Elapsed < TimeSpan.FromSeconds(60))
            {
                await Task.Delay(500);
            }

            // Update cache status after preload finishes (or times out)
            await UpdateCacheStatusAsync();

            StatusMessage = "? All data cached";
            await Task.Delay(1500);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"? Failed to cache all data: {ex.Message}";
        }
        finally
        {
            IsCaching = false;
        }
    }

    [RelayCommand]
    private async Task RefreshCacheStatusAsync()
    {
        await UpdateCacheStatusAsync();
        StatusMessage = "? Cache status refreshed";
        await Task.Delay(1500);
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task TestNotificationAsync()
    {
        try
        {
            // Trigger a manual poll and show result
            if (_notificationPollingService != null)
            {
                // Start a single poll by calling API directly here for clarity
                var api = App.Current?.Handler?.MauiContext?.Services?.GetService(typeof(IApiService)) as IApiService;
                if (api != null)
                {
                    var resp = await api.GetScheduledNotificationsAsync(limit: 10);
                    if (resp.Success && resp.Notifications != null && resp.Notifications.Count > 0)
                    {
                        var n = resp.Notifications[0];
                        await Shell.Current.DisplayAlert("Test Notification", $"Found scheduled: {n.NotificationType} at {n.ScheduledFor}", "OK");
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert("Test Notification", "No scheduled notifications found.", "OK");
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlert("Test Notification", "API service not available.", "OK");
                }
            }
            else
            {
                await Shell.Current.DisplayAlert("Test Notification", "Notification service not configured.", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Test Notification", $"Error: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task TestLocalNotificationAsync()
    {
        try
        {
            // Build a simple local notification message and display it
            var title = "Local Test Notification";
            var body = "This is a local test notification. Push not required.";

            // Use shell alert as a cross-platform simple notification
            await Shell.Current.DisplayAlert(title, body, "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Local Test Notification", $"Error: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task TestPushNotificationAsync()
    {
        try
        {
            // Prefer platform service (shows outside the app on Android/Windows)
            if (_localNotificationService != null)
            {
                await _localNotificationService.ShowAsync("Test Push Notification", "This is a system notification (local).", id: 9001);
                await Shell.Current.DisplayAlert("Push Test", "System notification sent.", "OK");
                return;
            }

            // Fallback: schedule via Plugin.LocalNotification (may or may not appear outside app depending on platform setup)
            var req = new NotificationRequest
            {
                NotificationId = 9001,
                Title = "Test Push Notification",
                Description = "This simulates a push notification (local).",
                ReturningData = "DummyData",
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = DateTime.Now.AddSeconds(1)
                }
            };

            req.Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
            {
                Priority = Plugin.LocalNotification.AndroidOption.AndroidPriority.High
            };

            LocalNotificationCenter.Current.Show(req);
            await Shell.Current.DisplayAlert("Push Test", "Local push notification scheduled (1s)", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Push Test Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task TestPushNotificationDelayedAsync()
    {
        try
        {
            var delay = notificationDelaySeconds;
            if (delay < 0) delay = 0;

            // If platform service present, wait then show (works while app runs)
            if (_localNotificationService != null)
            {
                await Shell.Current.DisplayAlert("Push Test", $"Scheduling system notification in {delay} seconds...", "OK");
                await Task.Delay(TimeSpan.FromSeconds(delay));
                await _localNotificationService.ShowAsync("Delayed Test Notification", $"This notification was delayed by {delay} seconds.");
                return;
            }

            // Fallback: schedule via Plugin.LocalNotification
            var notifyTime = DateTime.Now.AddSeconds(delay);
            var req = new NotificationRequest
            {
                NotificationId = 9002,
                Title = "Delayed Test Push",
                Description = $"This notification was delayed by {delay} seconds.",
                ReturningData = "DelayedDummy",
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = notifyTime
                }
            };

            req.Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
            {
                Priority = Plugin.LocalNotification.AndroidOption.AndroidPriority.High
            };

            LocalNotificationCenter.Current.Show(req);
            await Shell.Current.DisplayAlert("Push Test", $"Local push notification scheduled for {notifyTime:T}", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Delayed Push Test Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task NavigateToGameConfigAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("GameConfigEditorPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Navigation to GameConfigEditorPage failed: {ex}");
            await Shell.Current.DisplayAlert("Navigation Error", "Could not open Game Config Editor", "OK");
        }
    }

    [RelayCommand]
    private async Task NavigateToManagementAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("ManagementPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Navigation to ManagementPage failed: {ex}");
            await Shell.Current.DisplayAlert("Navigation Error", "Could not open Management page", "OK");
        }
    }

    [RelayCommand]
    private async Task NavigateToPitScoutingAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("PitConfigEditorPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Navigation to PitConfigEditorPage failed: {ex}");
            await Shell.Current.DisplayAlert("Navigation Error", "Could not open Pit Config Editor", "OK");
        }
    }
}
