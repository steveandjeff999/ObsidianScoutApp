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
}

public class NotificationPollingService : INotificationPollingService, IDisposable
{
 private readonly IApiService _apiService;
 private readonly ISettingsService _settingsService;
 private Timer? _timer;
 private readonly TimeSpan _interval = TimeSpan.FromSeconds(150); //2.5 minutes
 private bool _running;

 public NotificationPollingService(IApiService apiService, ISettingsService settingsService)
 {
 _apiService = apiService;
 _settingsService = settingsService;
 }

 public async Task StartAsync()
 {
 if (_running) return;
 _running = true;
 // Start immediately then every interval
 _timer = new Timer(async _ => await PollOnceAsync(), null, TimeSpan.Zero, _interval);
 }

 public void Start()
 {
 Task.Run(async () => await StartAsync());
 }

 public void Stop()
 {
 _timer?.Change(Timeout.Infinite, Timeout.Infinite);
 _timer?.Dispose();
 _timer = null;
 _running = false;
 }

 private async Task PollOnceAsync()
 {
 try
 {
 var resp = await _apiService.GetScheduledNotificationsAsync(limit:200);
 if (resp.Success && resp.Notifications != null)
 {
 foreach (var n in resp.Notifications)
 {
 // Only handle pending notifications with push delivery enabled
 if (n.Status == "pending" && n.DeliveryMethods != null && n.DeliveryMethods.TryGetValue("push", out var pushEnabled) && pushEnabled)
 {
 // If scheduled_for is in the past or within next minute, show a local notification
 var now = DateTime.UtcNow;
 var scheduledUtc = n.ScheduledFor.Kind == DateTimeKind.Utc ? n.ScheduledFor : n.ScheduledFor.ToUniversalTime();
 var diff = scheduledUtc - now;
 if (diff <= TimeSpan.FromMinutes(1))
 {
 // Publish a platform-agnostic notification via MainThread (UI)
 MainThread.BeginInvokeOnMainThread(async () =>
 {
 try
 {
 // Simple in-app alert; platform push should have been delivered by server but show fallback
 await Shell.Current.DisplayAlertAsync("Scheduled Notification", GetNotificationMessage(n), "OK");
 }
 catch { }
 });
 }
 }
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[Notifications] Poll error: {ex.Message}");
 }
 }

 private string GetNotificationMessage(ScheduledNotification n)
 {
 if (!string.IsNullOrEmpty(n.NotificationType) && n.NotificationType.Contains("match"))
 {
 return $"Match reminder: Match #{n.MatchNumber} (Event {n.EventCode})";
 }
 return $"Notification: {n.NotificationType}";
 }

 public void Dispose()
 {
 Stop();
 }
}
