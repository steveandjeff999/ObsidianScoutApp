using global::Android.App;
using global::Android.Content;
using global::Android.OS;
using AndroidX.Core.App;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianScout.Models;
using ObsidianScout.Services;

namespace ObsidianScout.Platforms.Android
{
 // Use the generated Java class name as the service Name so it matches the manifest entry
 // (the runtime generates a package-prefixed class; using its name here ensures the manifest
 // foregroundServiceType attribute applies correctly on Android12+).
 [Service(Name = "crc64f3faeb7d35d8db75.ForegroundNotificationService", Enabled = true, Exported = false)]
 public class ForegroundNotificationService : Service
 {
 const string CHANNEL_ID = "obsidian_scout_channel";
 const int FOREGROUND_ID =2001;

 private Timer? _timer;
 private HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
 private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

 public override void OnCreate()
 {
 base.OnCreate();
 CreateNotificationChannel();
 StartForeground(FOREGROUND_ID, BuildForegroundNotification("ObsidianScout running"));
 // Start polling every150 seconds
 _timer = new Timer(async _ => await PollServerAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(150));
 }

 public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
 {
 return StartCommandResult.Sticky;
 }

 public override void OnDestroy()
 {
 base.OnDestroy();
 try { _timer?.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
 _timer?.Dispose();
 _http.Dispose();
 }

 public override IBinder? OnBind(Intent? intent) => null;

 private void CreateNotificationChannel()
 {
 if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
 {
 var nm = (global::Android.App.NotificationManager)GetSystemService(NotificationService);
 var existing = nm.GetNotificationChannel(CHANNEL_ID);
 if (existing == null)
 {
 var channel = new global::Android.App.NotificationChannel(CHANNEL_ID, "ObsidianScout Notifications", global::Android.App.NotificationImportance.High)
 {
 Description = "Notifications from ObsidianScout"
 };
 nm.CreateNotificationChannel(channel);
 }
 }
 }

 private Notification BuildForegroundNotification(string text)
 {
 var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
 .SetContentTitle("ObsidianScout")
 .SetContentText(text)
 .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
 .SetOngoing(true);

 return builder.Build();
 }

 private async Task PollServerAsync()
 {
 try
 {
 // Use SettingsService to read server URL and token
 var settings = new SettingsService();
 var baseUrl = await settings.GetServerUrlAsync();
 var token = await settings.GetTokenAsync();
 if (string.IsNullOrEmpty(token)) return;

 var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), "/api/mobile/notifications/scheduled"));
 req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

 var resp = await _http.SendAsync(req);
 if (!resp.IsSuccessStatusCode) return;

 var json = await resp.Content.ReadAsStringAsync();
 var scheduledResp = JsonSerializer.Deserialize<ScheduledNotificationsResponse>(json, _jsonOptions);
 if (scheduledResp?.Notifications == null) return;

 var now = DateTime.UtcNow;
 foreach (var n in scheduledResp.Notifications)
 {
 if (n.Status != "pending") continue;
 if (n.DeliveryMethods == null) continue;
 if (!n.DeliveryMethods.TryGetValue("push", out var push) || !push) continue;

 var scheduledUtc = n.ScheduledFor.Kind == DateTimeKind.Utc ? n.ScheduledFor : n.ScheduledFor.ToUniversalTime();
 var diff = scheduledUtc - now;
 if (diff <= TimeSpan.FromMinutes(1))
 {
 // Show notification
 ShowNotification(n);
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[ForegroundService] Poll error: {ex.Message}");
 }
 }

 private void ShowNotification(ScheduledNotification n)
 {
 try
 {
 var nm = NotificationManagerCompat.From(this);

 var title = !string.IsNullOrEmpty(n.NotificationType) ? n.NotificationType : "Notification";
 var body = n.MatchNumber.HasValue ? $"Match {n.MatchNumber} reminder" : n.NotificationType;

 var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
 .SetContentTitle(title)
 .SetContentText(body)
 .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
 .SetAutoCancel(true)
 .SetPriority((int)NotificationPriority.High);

 var id = n.Id >0 ? n.Id : new Random().Next(1000,9999);
 nm.Notify(id, builder.Build());
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[ForegroundService] ShowNotification failed: {ex.Message}");
 }
 }
 }
}
