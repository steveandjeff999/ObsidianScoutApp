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
    [Service(Name = "crc64f3faeb7d35d8db75.ForegroundNotificationService", Enabled = true, Exported = false)]
    public class ForegroundNotificationService : Service
    {
        const string CHANNEL_ID = "obsidian_scout_channel";
        const int FOREGROUND_ID = 2001;

private IBackgroundNotificationService? _backgroundNotificationService;
    private HttpClient? _http;
      private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
      // REMOVED: PowerManager.WakeLock - let Android Doze mode handle power management
private bool _isRunning = false;
        private bool _isInitialized = false;
  private int _initRetryCount = 0;
        private const int MAX_INIT_RETRIES = 5;

  public override void OnCreate()
        {
   base.OnCreate();
   
       try
        {
System.Diagnostics.Debug.WriteLine("[ForegroundService] ===== OnCreate called =====");
  
   CreateNotificationChannel();
    StartForeground(FOREGROUND_ID, BuildForegroundNotification("ObsidianScout notifications active"));
     
      System.Diagnostics.Debug.WriteLine("[ForegroundService] Service started in foreground (battery optimized mode)");
        }
     catch (Exception ex)
   {
         System.Diagnostics.Debug.WriteLine($"[ForegroundService] OnCreate exception: {ex.Message}");
       }
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
   System.Diagnostics.Debug.WriteLine("[ForegroundService] ===== OnStartCommand called =====");
   System.Diagnostics.Debug.WriteLine($"[ForegroundService] Intent: {intent?.Action ?? "null"}");
     System.Diagnostics.Debug.WriteLine($"[ForegroundService] Flags: {flags}");
   System.Diagnostics.Debug.WriteLine($"[ForegroundService] StartId: {startId}");
    
         // Check if started from boot
      var startedFromBoot = intent?.GetBooleanExtra("started_from_boot", false) ?? false;
     var startedFromDelayedBoot = intent?.GetBooleanExtra("started_from_delayed_boot", false) ?? false;
       System.Diagnostics.Debug.WriteLine($"[ForegroundService] Started from boot: {startedFromBoot}");
     System.Diagnostics.Debug.WriteLine($"[ForegroundService] Started from delayed boot: {startedFromDelayedBoot}");
    System.Diagnostics.Debug.WriteLine($"[ForegroundService] IsInitialized: {_isInitialized}");
        System.Diagnostics.Debug.WriteLine($"[ForegroundService] IsRunning: {_isRunning}");
   
    // Check battery optimization status
   CheckBatteryOptimization();
    
    // Always ensure foreground notification
       try
            {
  StartForeground(FOREGROUND_ID, BuildForegroundNotification("ObsidianScout notifications active"));
      System.Diagnostics.Debug.WriteLine("[ForegroundService] ? Started in foreground");
     }
        catch (Exception ex)
  {
        System.Diagnostics.Debug.WriteLine($"[ForegroundService] ? Failed to start foreground: {ex.Message}");
         }
        
   // Initialize service if needed or if restarting after boot
    if (!_isInitialized || !_isRunning || startedFromBoot || startedFromDelayedBoot)
      {
     System.Diagnostics.Debug.WriteLine("[ForegroundService] Initializing background notification service...");
      InitializeBackgroundService(startedFromBoot || startedFromDelayedBoot);
 }
            else
     {
System.Diagnostics.Debug.WriteLine("[ForegroundService] Service already initialized and running");
            }
   
   // Return START_STICKY so service restarts if killed by system
      return StartCommandResult.Sticky;
        }

  private void CheckBatteryOptimization()
    {
        try
  {
   if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
   {
        var powerManager = (PowerManager?)GetSystemService(PowerService);
   if (powerManager != null)
   {
     var packageName = PackageName;
     var isIgnoringOptimizations = powerManager.IsIgnoringBatteryOptimizations(packageName);
   
    if (isIgnoringOptimizations)
        {
          System.Diagnostics.Debug.WriteLine("[ForegroundService] ? Battery optimization disabled - full background access");
     }
     else
       {
              System.Diagnostics.Debug.WriteLine("[ForegroundService] ?? Battery optimization ENABLED - service may be restricted in Doze mode");
      System.Diagnostics.Debug.WriteLine("[ForegroundService] ?? Recommend disabling battery optimization for reliable notifications");
     }
    }
       }
      }
        catch (Exception ex)
 {
       System.Diagnostics.Debug.WriteLine($"[ForegroundService] Error checking battery optimization: {ex.Message}");
        }
    }

    private void InitializeBackgroundService(bool isBootStart = false)
        {
    try
       {
            _isRunning = true;
 
    // REMOVED: Wake lock acquisition - let Android Doze mode handle power
       // Modern Android (6.0+) handles background tasks efficiently with Doze mode
   // Foreground service already exempts us from most restrictions
       
   // Initialize HttpClient if needed
       if (_http == null)
   {
  _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
   }

         // Initialize background notification service with retry logic
            Task.Run(async () =>
   {
     for (int attempt = 0; attempt < MAX_INIT_RETRIES; attempt++)
      {
           try
       {
       System.Diagnostics.Debug.WriteLine($"[ForegroundService] Initialization attempt {attempt + 1}/{MAX_INIT_RETRIES}");
    
       // Calculate delay - longer delays after boot to let system settle
       int delay;
     if (isBootStart)
{
      // After boot: wait 5s, then 10s, then 20s, etc.
           delay = attempt == 0 ? 5000 : (int)Math.Pow(2, attempt) * 5000;
}
       else
    {
     // Regular start: wait 2s, then 4s, then 8s, etc.
    delay = attempt == 0 ? 2000 : (int)Math.Pow(2, attempt) * 2000;
       }
          
  System.Diagnostics.Debug.WriteLine($"[ForegroundService] Waiting {delay}ms before attempt...");
   await Task.Delay(delay);
       
       // Create new instances of services
   var apiService = new ApiService(_http!, new SettingsService(), new CacheService(), new ConnectivityService());
   var settingsService = new SettingsService();
  var localNotificationService = new LocalNotificationService();

    _backgroundNotificationService = new BackgroundNotificationService(apiService, settingsService, localNotificationService);
     await _backgroundNotificationService.StartAsync();
      
      _isInitialized = true;
    _initRetryCount = 0;
   
   System.Diagnostics.Debug.WriteLine("[ForegroundService] ? Background notification service started successfully (power optimized)");
       UpdateForegroundNotification("ObsidianScout - Notifications active (battery optimized)");
 
     return; // Success!
   }
             catch (Exception ex)
    {
   System.Diagnostics.Debug.WriteLine($"[ForegroundService] ? Attempt {attempt + 1} failed: {ex.Message}");
   System.Diagnostics.Debug.WriteLine($"[ForegroundService] Error details: {ex.StackTrace}");
     
   if (attempt == MAX_INIT_RETRIES - 1)
      {
    System.Diagnostics.Debug.WriteLine("[ForegroundService] ? All initialization attempts failed");
UpdateForegroundNotification("ObsidianScout - Initialization failed (restart app)");
   }
         }
         }
            });
        }
  catch (Exception ex)
        {
    System.Diagnostics.Debug.WriteLine($"[ForegroundService] ? InitializeBackgroundService exception: {ex.Message}");
 }
    }

        private void UpdateForegroundNotification(string text)
        {
   try
      {
      var notification = BuildForegroundNotification(text);
       var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
     notificationManager?.Notify(FOREGROUND_ID, notification);
    }
      catch (Exception ex)
            {
         System.Diagnostics.Debug.WriteLine($"[ForegroundService] Error updating notification: {ex.Message}");
      }
        }

  public override void OnDestroy()
        {
   System.Diagnostics.Debug.WriteLine("[ForegroundService] ===== OnDestroy called =====");

   _isRunning = false;
       _isInitialized = false;
   
    base.OnDestroy();

     // Stop background notification service
            try
            {
  _backgroundNotificationService?.Stop();
   System.Diagnostics.Debug.WriteLine("[ForegroundService] Background notification service stopped");
        }
     catch (Exception ex)
       {
      System.Diagnostics.Debug.WriteLine($"[ForegroundService] Error stopping background notification service: {ex.Message}");
       }

     // REMOVED: Wake lock release - no longer using wake locks            
   // Dispose HttpClient
    _http?.Dispose();
  _http = null;
    }

        public override void OnTaskRemoved(Intent? rootIntent)
        {
try
         {
   System.Diagnostics.Debug.WriteLine("[ForegroundService] OnTaskRemoved - app swiped away from recents");
        System.Diagnostics.Debug.WriteLine("[ForegroundService] Service will continue running (battery optimized)");
          
         // Service continues running even when task is removed
    // This is the desired behavior for background notifications

    base.OnTaskRemoved(rootIntent);
     }
 catch (Exception ex)
     {
System.Diagnostics.Debug.WriteLine($"[ForegroundService] OnTaskRemoved error: {ex.Message}");
    }
        }

   public override IBinder? OnBind(Intent? intent) => null;

 private void CreateNotificationChannel()
        {
 if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
   {
   var nm = (global::Android.App.NotificationManager?)GetSystemService(NotificationService);
       if (nm == null) return;
       
     var existing = nm.GetNotificationChannel(CHANNEL_ID);
    if (existing == null)
  {
    // HIGH importance for audible notifications with sound and vibration
   var channel = new global::Android.App.NotificationChannel(CHANNEL_ID, "ObsidianScout Notifications", global::Android.App.NotificationImportance.High)
 {
Description = "Match and chat notifications",
   LockscreenVisibility = NotificationVisibility.Public,
     // Enable sound (uses default notification sound)
       // Enable vibration
    };
       channel.EnableVibration(true);
            channel.SetVibrationPattern(new long[] { 0, 250, 250, 250 }); // Vibrate pattern: wait 0ms, vibrate 250ms, pause 250ms, vibrate 250ms
   
      nm.CreateNotificationChannel(channel);
     System.Diagnostics.Debug.WriteLine("[ForegroundService] Notification channel created (HIGH priority with sound and vibration)");
     }
    }
        }

        private Notification BuildForegroundNotification(string text)
        {
 var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
 .SetContentTitle("ObsidianScout")
      .SetContentText(text)
 .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
  .SetOngoing(true)
    .SetPriority((int)NotificationPriority.Low) // Keep foreground service notification low priority
  .SetCategory(NotificationCompat.CategoryService)
   .SetShowWhen(false)
     .SetSilent(true); // Keep service notification silent

    return builder.Build();
     }
    }
}
