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
    private volatile bool _disposed = false;
    private volatile bool _started = false;

    public NotificationPollingService(IApiService apiService, ISettingsService settingsService, ILocalNotificationService? localNotificationService = null)
    {
        _backgroundNotificationService = new BackgroundNotificationService(apiService, settingsService, localNotificationService);
    }

    public async Task StartAsync()
    {
        if (_disposed || _started) return;
        _started = true;
        
        System.Diagnostics.Debug.WriteLine("[NotificationPolling] Starting notification polling service");
        try
        {
            await _backgroundNotificationService.StartAsync();
            System.Diagnostics.Debug.WriteLine("[NotificationPolling] Notification polling service started");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationPolling] Start failed: {ex.Message}");
            _started = false;
        }
    }

    public void Start()
    {
        if (_disposed || _started) return;
        Task.Run(async () => 
        {
            try
            {
                await StartAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationPolling] Start task failed: {ex.Message}");
            }
        });
    }

    public void Stop()
    {
        if (_disposed) return;
        
        System.Diagnostics.Debug.WriteLine("[NotificationPolling] Stopping notification polling service");
        try
        {
            _backgroundNotificationService.Stop();
            _started = false;
            System.Diagnostics.Debug.WriteLine("[NotificationPolling] Notification polling service stopped");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationPolling] Stop error: {ex.Message}");
        }
    }

    public async Task ForceCheckAsync()
    {
        if (_disposed) return;
        
        System.Diagnostics.Debug.WriteLine("[NotificationPolling] Force check requested");
        try
        {
            await _backgroundNotificationService.ForceCheckAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationPolling] ForceCheck error: {ex.Message}");
        }
    }

    public void OnAppBackground()
    {
        if (_disposed) return;
        try
        {
            _backgroundNotificationService.OnAppBackground();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationPolling] OnAppBackground error: {ex.Message}");
        }
    }

    public void OnAppForeground()
    {
        if (_disposed) return;
        try
        {
            _backgroundNotificationService.OnAppForeground();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationPolling] OnAppForeground error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        try
        {
            Stop();
            
            if (_backgroundNotificationService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationPolling] Dispose error: {ex.Message}");
        }
    }
}
