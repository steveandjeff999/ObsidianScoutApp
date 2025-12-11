using System;
using System.Threading;
using System.Threading.Tasks;
using ObsidianScout.Models;

namespace ObsidianScout.Services;

public interface INotificationPollingService
{
    void Start();
    void Stop();
    Task StartAsync();
    Task ForceCheckAsync();
    void OnAppBackground();
    void OnAppForeground();
}

public class NotificationPollingService : INotificationPollingService, IDisposable
{
    private readonly IBackgroundNotificationService _backgroundNotificationService;

    public NotificationPollingService(IApiService apiService, ISettingsService settingsService, ILocalNotificationService? localNotificationService = null)
    {
        _backgroundNotificationService = new BackgroundNotificationService(apiService, settingsService, localNotificationService);
    }

    public async Task StartAsync()
    {
        System.Diagnostics.Debug.WriteLine("[NotificationPolling] Starting notification polling service");
        await _backgroundNotificationService.StartAsync();
        System.Diagnostics.Debug.WriteLine("[NotificationPolling] Notification polling service started");
    }

    public void Start()
    {
        Task.Run(async () => await StartAsync());
    }

    public void Stop()
    {
        System.Diagnostics.Debug.WriteLine("[NotificationPolling] Stopping notification polling service");
        _backgroundNotificationService.Stop();
        System.Diagnostics.Debug.WriteLine("[NotificationPolling] Notification polling service stopped");
    }

    public async Task ForceCheckAsync()
    {
        System.Diagnostics.Debug.WriteLine("[NotificationPolling] Force check requested");
        await _backgroundNotificationService.ForceCheckAsync();
    }

    public void OnAppBackground()
    {
        _backgroundNotificationService.OnAppBackground();
    }

    public void OnAppForeground()
    {
        _backgroundNotificationService.OnAppForeground();
    }

    public void Dispose()
    {
        Stop();
    }
}
