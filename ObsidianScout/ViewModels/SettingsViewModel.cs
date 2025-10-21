using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Services;

namespace ObsidianScout.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ICacheService _cacheService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private bool isDarkMode;

    [ObservableProperty]
    private string cacheStatus = "Cache is active";

    [ObservableProperty]
    private bool isClearing;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public SettingsViewModel(ICacheService cacheService, ISettingsService settingsService)
    {
        _cacheService = cacheService;
     _settingsService = settingsService;

        LoadThemePreference();
        _ = UpdateCacheStatusAsync();
    }

    private async void LoadThemePreference()
    {
        var theme = await _settingsService.GetThemeAsync();
   IsDarkMode = theme == "Dark";
    }

    private async Task UpdateCacheStatusAsync()
    {
        try
        {
    var hasCache = await _cacheService.HasCachedDataAsync();
    var lastPreload = await _cacheService.GetCacheTimestampAsync("cache_last_preload");

            if (hasCache && lastPreload.HasValue)
            {
        var cacheAge = DateTime.UtcNow - lastPreload.Value;
      CacheStatus = $"Cache active ({cacheAge.TotalHours:F1} hours old)";
         }
 else
  {
    CacheStatus = "No cached data";
            }
        }
 catch
        {
       CacheStatus = "Unable to check cache status";
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
    private async Task RefreshCacheStatusAsync()
    {
        await UpdateCacheStatusAsync();
      StatusMessage = "? Cache status refreshed";
        await Task.Delay(1500);
        StatusMessage = string.Empty;
    }
}
