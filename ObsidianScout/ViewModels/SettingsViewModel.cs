using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using System.Linq;

namespace ObsidianScout.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
        // Last update metadata returned by the update service
        private UpdateInfo? _lastUpdateInfo;

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

    private string _installerStatusMessage = string.Empty;
    public string InstallerStatusMessage
    {
        get => _installerStatusMessage;
        set
        {
            if (_installerStatusMessage == value) return;
            _installerStatusMessage = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private bool isCaching;

    // New: delay in seconds for delayed test notification
    [ObservableProperty]
    private int notificationDelaySeconds = 60;

    [ObservableProperty]
    private bool hasManagementAccess;

    [ObservableProperty]
    private string appVersion = string.Empty;

    [ObservableProperty]
    private bool autoUpdateCheck;

    [ObservableProperty]
    private bool isAndroid;

    // GitHub token removed - using public API for repo access

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

        // Platform flags
        IsAndroid = DeviceInfo.Platform == DevicePlatform.Android;

        // Load auto update preference
        _ = LoadAutoUpdatePreferenceAsync();
        _ = CheckInstallerFileProviderAsync();
        // no github token load required for public API
    }

    private async Task CheckInstallerFileProviderAsync()
    {
        try
        {
            var installer = App.Current?.Handler?.MauiContext?.Services?.GetService(typeof(IInstallerService)) as IInstallerService;
            if (installer == null)
            {
                InstallerStatusMessage = "Installer service not available.";
                return;
            }

            var ok = await installer.IsFileProviderAvailableAsync();
            InstallerStatusMessage = ok ? string.Empty : "FileProvider not configured. APK installs may fail. See app documentation to add provider to AndroidManifest.";
        }
        catch (Exception ex)
        {
            InstallerStatusMessage = $"Installer check failed: {ex.Message}";
        }
    }

    private async Task LoadAutoUpdatePreferenceAsync()
    {
        try
        {
            AutoUpdateCheck = await _settingsService.GetAutoUpdateCheckAsync();
        }
        catch { }
    }

    partial void OnAutoUpdateCheckChanged(bool value)
    {
        _ = _settingsService.SetAutoUpdateCheckAsync(value);
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateSvc = App.Current?.Handler?.MauiContext?.Services?.GetService(typeof(IUpdateService)) as IUpdateService;
            if (updateSvc == null)
            {
                await Shell.Current.DisplayAlert("Update", "Update service not available.", "OK");
                return;
            }
            var info = await updateSvc.GetLatestApkAsync();
            if (info == null)
            {
                await Shell.Current.DisplayAlert("Update", "No update metadata found.", "OK");
                return;
            }

            // remember last fetched info for install action
            _lastUpdateInfo = info;

            // Normalize and compare versions
            string NormalizeVersion(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var v = s.Trim();
                // strip leading 'v'
                if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
                // remove any trailing non-numeric suffix (e.g., " (build 1)")
                var idx = v.IndexOf(' ');
                if (idx > 0) v = v.Substring(0, idx);
                return v;
            }

            var remoteVerRaw = info.Version ?? string.Empty;
            var localVerRaw = AppInfo.VersionString ?? string.Empty;
            var remoteNorm = NormalizeVersion(remoteVerRaw);
            var localNorm = NormalizeVersion(localVerRaw);

            var canParseRemote = Version.TryParse(remoteNorm, out var remoteVer);
            var canParseLocal = Version.TryParse(localNorm, out var localVer);

            // If we can't parse remote version, still show found version or download availability
            if (!canParseRemote)
            {
                if (string.IsNullOrEmpty(info.DownloadUrl))
                {
                    await Shell.Current.DisplayAlert("Update Check", $"No installable APK found. Version discovered: {remoteVerRaw}", "OK");
                    StatusMessage = $"Found version: {remoteVerRaw}";
                    return;
                }

                // Unknown remote version but APK exists -> inform user
                await Shell.Current.DisplayAlert("Update Found", $"Update available: {remoteVerRaw}", "OK");
                StatusMessage = info.DownloadUrl;
                return;
            }

            // If local can't be parsed, treat remote as newer (conservative)
            if (!canParseLocal)
            {
                if (string.IsNullOrEmpty(info.DownloadUrl))
                {
                    await Shell.Current.DisplayAlert("Update Check", $"No installable APK found. Version discovered: {remoteVerRaw}", "OK");
                    StatusMessage = $"Found version: {remoteVerRaw}";
                    return;
                }

                await Shell.Current.DisplayAlert("Update Found", $"Update available: {remoteVerRaw}", "OK");
                StatusMessage = info.DownloadUrl;
                return;
            }

            // Both parsed - compare
            if (remoteVer > localVer)
            {
                if (string.IsNullOrEmpty(info.DownloadUrl))
                {
                    await Shell.Current.DisplayAlert("Update Check", $"New version available: {remoteVerRaw}, but no APK found.", "OK");
                    StatusMessage = $"Found version: {remoteVerRaw}";
                    return;
                }

                await Shell.Current.DisplayAlert("Update Found", $"Update available: {remoteVerRaw} (installed: {localVerRaw})", "OK");
                StatusMessage = info.DownloadUrl;
                return;
            }

            // remote <= local
            await Shell.Current.DisplayAlert("Up to Date", $"No update available. Installed: {localVerRaw}, Latest: {remoteVerRaw}", "OK");
            StatusMessage = $"Latest: {remoteVerRaw}";
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Update Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DownloadAndInstallAsync()
    {
        try
        {
            // Prefer the download URL from _lastUpdateInfo if available
            var url = _lastUpdateInfo?.DownloadUrl ?? StatusMessage;
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // Try to construct the GitHub Releases download URL
                if (_lastUpdateInfo != null && !string.IsNullOrEmpty(_lastUpdateInfo.Version) && !string.IsNullOrEmpty(_lastUpdateInfo.FileName))
                {
                    url = $"https://github.com/steveandjeff999/ObsidianScoutApp/releases/download/{_lastUpdateInfo.Version}/{_lastUpdateInfo.FileName}";
                }
                else
                {
                    await Shell.Current.DisplayAlert("Install", "No download URL available. Run 'Check for Updates' first.", "OK");
                    return;
                }
            }

            // If we have last fetched update info and it contains a version, compare with installed version
            try
            {
                if (_lastUpdateInfo != null && !string.IsNullOrEmpty(_lastUpdateInfo.Version))
                {
                    string NormalizeVersion(string s)
                    {
                        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                        var v = s.Trim();
                        if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
                        var idx = v.IndexOf(' ');
                        if (idx > 0) v = v.Substring(0, idx);
                        return v;
                    }

                    var remote = NormalizeVersion(_lastUpdateInfo.Version ?? string.Empty);
                    var local = NormalizeVersion(AppInfo.VersionString ?? string.Empty);
                    if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(local, out var localVer))
                    {
                        if (remoteVer == localVer)
                        {
                            var reinstall = await Shell.Current.DisplayAlert("Reinstall", $"The installer you selected is version {remoteVer} which matches the installed version ({localVer}). Do you want to reinstall it?", "Reinstall", "Cancel");
                            if (!reinstall) return;
                        }
                    }
                }
            }
            catch { }

            var installer = App.Current?.Handler?.MauiContext?.Services?.GetService(typeof(IInstallerService)) as IInstallerService;
            if (installer == null)
            {
                await Shell.Current.DisplayAlert("Install", "Installer service not available on this platform.", "OK");
                return;
            }

            var ok = await installer.DownloadAndInstallApkAsync(url);
            if (ok)
            {
                await Shell.Current.DisplayAlert("Install", "Installer launched.", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Install", "Failed to download or launch installer.", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Install Error", ex.Message, "OK");
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
