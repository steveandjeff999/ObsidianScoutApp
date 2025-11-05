using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Android;
using Android.Content;
using Android.Provider;

namespace ObsidianScout
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable })]
 public class MainActivity : MauiAppCompatActivity
    {
   private bool _isDestroying = false;
      
        // Store pending navigation from notification intent
        private static string? _pendingNavigationUri = null;
        private static bool _hasPendingNavigation = false;
        private static readonly object _navigationLock = new object();

     protected override void OnCreate(Bundle? savedInstanceState)
    {
            base.OnCreate(savedInstanceState);

            try
      {
      System.Diagnostics.Debug.WriteLine("[MainActivity] OnCreate called");
       
            // Android13+ requires runtime permission for notifications
    if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
      {
    if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.PostNotifications) != (int)Permission.Granted)
    {
        ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.PostNotifications }, 1001);
    }
    }

            // Create a default notification channel to ensure notifications are delivered
  var context = global::Android.App.Application.Context;
   if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
         {
    var channelId = "obsidian_scout_channel";
   var channelName = "ObsidianScout Notifications";
          var channelDesc = "General notifications from ObsidianScout";

   var nm = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
         if (nm != null)
        {
    var existing = nm.GetNotificationChannel(channelId);
 if (existing == null)
      {
    var channel = new global::Android.App.NotificationChannel(channelId, channelName, global::Android.App.NotificationImportance.High)
         {
    Description = channelDesc
           };
      nm.CreateNotificationChannel(channel);
   }
          }
          }

    // Request battery optimization exemption
     RequestBatteryOptimizationExemption();

   // CRITICAL: Process the intent immediately
        ProcessNotificationIntent(Intent);
            }
         catch (System.Exception ex)
            {
 System.Diagnostics.Debug.WriteLine($"[MainActivity] Notification init failed: {ex.Message}");
      }
        }

     protected override void OnNewIntent(Intent? intent)
      {
   base.OnNewIntent(intent);
            
          // CRITICAL: Update the Intent property so it's available to the activity
          Intent = intent;

        try
     {
      System.Diagnostics.Debug.WriteLine("[MainActivity] OnNewIntent - handling notification tap while app is running");
        
                // Process intent immediately when app is already running
        ProcessNotificationIntent(intent);
   
     // Trigger navigation immediately if app is in foreground
 if (!_isDestroying)
           {
       System.Diagnostics.Debug.WriteLine("[MainActivity] App is in foreground, triggering immediate navigation");
        TriggerNavigationInApp();
     }
}
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] OnNewIntent error: {ex.Message}");
        }
        }

        private void ProcessNotificationIntent(Intent? intent)
        {
         if (intent == null)
         {
     System.Diagnostics.Debug.WriteLine("[MainActivity] Intent is null, skipping");
    return;
     }

       try
   {
     var type = intent.GetStringExtra("type");
          var sourceType = intent.GetStringExtra("sourceType");
        var sourceId = intent.GetStringExtra("sourceId");
    var messageId = intent.GetStringExtra("messageId");
    var eventCode = intent.GetStringExtra("eventCode");
                var eventId = intent.GetStringExtra("eventId");
    var matchNumber = intent.GetStringExtra("matchNumber");

        System.Diagnostics.Debug.WriteLine($"[MainActivity] Processing intent extras:");
      System.Diagnostics.Debug.WriteLine($"  type: {type}");
System.Diagnostics.Debug.WriteLine($"  sourceType: {sourceType}");
            System.Diagnostics.Debug.WriteLine($"  sourceId: {sourceId}");
    System.Diagnostics.Debug.WriteLine($"  eventCode: {eventCode}");
 System.Diagnostics.Debug.WriteLine($"eventId: {eventId}");
             System.Diagnostics.Debug.WriteLine($"  matchNumber: {matchNumber}");

                string? navUri = null;

            if (type == "chat" && !string.IsNullOrEmpty(sourceType) && !string.IsNullOrEmpty(sourceId))
         {
 // Build navigation URI for chat using ABSOLUTE route
        navUri = $"//ChatPage?sourceType={sourceType}&sourceId={System.Uri.EscapeDataString(sourceId)}";
       System.Diagnostics.Debug.WriteLine($"[MainActivity] ✓ Chat intent detected");
    }
else if (type == "match")
        {
            // IMPROVED: Handle match notifications even without full data
       if (!string.IsNullOrEmpty(eventId))
    {
       // Full match notification with event info - navigate to prediction page
  navUri = $"//MatchPredictionPage?eventId={eventId}";
  
       if (!string.IsNullOrEmpty(eventCode))
       {
          navUri += $"&eventCode={System.Uri.EscapeDataString(eventCode)}";
  }
  
 if (!string.IsNullOrEmpty(matchNumber))
      {
              navUri += $"&matchNumber={matchNumber}";
         }
  
        System.Diagnostics.Debug.WriteLine($"[MainActivity] ✓ Match intent detected with eventId");
   }
        else
   {
        // Match notification without event info - just open the app to main page
        navUri = "//MainPage";
       System.Diagnostics.Debug.WriteLine($"[MainActivity] ✓ Match intent detected (no eventId, opening MainPage)");
            }
        }

 if (!string.IsNullOrEmpty(navUri))
        {
         lock (_navigationLock)
       {
              _pendingNavigationUri = navUri;
      _hasPendingNavigation = true;
      }
      
   System.Diagnostics.Debug.WriteLine($"[MainActivity] ✓ Stored pending navigation: {navUri}");
           }
            else
                {
      System.Diagnostics.Debug.WriteLine($"[MainActivity] ✗ No valid notification data found in intent");
    }
 }
            catch (System.Exception ex)
            {
            System.Diagnostics.Debug.WriteLine($"[MainActivity] ProcessNotificationIntent error: {ex.Message}");
  }
        }

      private void TriggerNavigationInApp()
        {
        try
            {
       // Use MainThread to ensure we're on UI thread
    MainThread.BeginInvokeOnMainThread(async () =>
                {
              try
  {
await Task.Delay(500); // Small delay to ensure app is ready
             
            string? navUri;
     lock (_navigationLock)
            {
           if (!_hasPendingNavigation)
             {
      System.Diagnostics.Debug.WriteLine("[MainActivity] No pending navigation to trigger");
       return;
    }
      navUri = _pendingNavigationUri;
   }

    if (!string.IsNullOrEmpty(navUri) && Shell.Current != null)
              {
   System.Diagnostics.Debug.WriteLine($"[MainActivity] Executing navigation: {navUri}");
  await Shell.Current.GoToAsync(navUri);
       
       lock (_navigationLock)
        {
        _hasPendingNavigation = false;
           _pendingNavigationUri = null;
        }
  
        System.Diagnostics.Debug.WriteLine("[MainActivity] ✓ Navigation completed");
              }
    }
   catch (System.Exception ex)
 {
      System.Diagnostics.Debug.WriteLine($"[MainActivity] Navigation error: {ex.Message}");
          }
          });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] TriggerNavigationInApp error: {ex.Message}");
            }
        }

        // Public static method for App.xaml.cs to check and execute pending navigation
   public static bool HasPendingNavigation()
        {
 lock (_navigationLock)
       {
                return _hasPendingNavigation;
         }
     }

        public static string? GetPendingNavigationUri()
        {
            lock (_navigationLock)
   {
                return _pendingNavigationUri;
       }
        }

    public static void ClearPendingNavigation()
        {
            lock (_navigationLock)
        {
_hasPendingNavigation = false;
        _pendingNavigationUri = null;
            }
 System.Diagnostics.Debug.WriteLine("[MainActivity] Cleared pending navigation");
        }

        protected override void OnDestroy()
        {
            try
      {
      _isDestroying = true;
          System.Diagnostics.Debug.WriteLine("[MainActivity] OnDestroy called - activity is being destroyed");

    // Don't stop foreground service - it should continue running
             // The service lifecycle is separate from activity lifecycle

        base.OnDestroy();
          }
       catch (System.Exception ex)
     {
       System.Diagnostics.Debug.WriteLine($"[MainActivity] OnDestroy error (ignored): {ex.Message}");
      // Swallow exception during destruction to prevent crash
         }
      }

        protected override void OnPause()
        {
       try
      {
           System.Diagnostics.Debug.WriteLine("[MainActivity] OnPause - app going to background");
     base.OnPause();
            }
   catch (System.Exception ex)
        {
         System.Diagnostics.Debug.WriteLine($"[MainActivity] OnPause error: {ex.Message}");
     }
        }

      protected override void OnStop()
  {
       try
            {
                System.Diagnostics.Debug.WriteLine("[MainActivity] OnStop - app no longer visible");
            base.OnStop();
            }
            catch (System.Exception ex)
         {
      System.Diagnostics.Debug.WriteLine($"[MainActivity] OnStop error: {ex.Message}");
     }
        }

        private void RequestBatteryOptimizationExemption()
  {
         try
    {
          if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
         {
           var powerManager = (PowerManager?)GetSystemService(PowerService);
        if (powerManager != null)
    {
            var packageName = PackageName;
  if (!powerManager.IsIgnoringBatteryOptimizations(packageName))
    {
   System.Diagnostics.Debug.WriteLine("[MainActivity] Requesting battery optimization exemption");

       // Show dialog to user explaining why we need this
                var intent = new Intent();
      intent.SetAction(Settings.ActionRequestIgnoreBatteryOptimizations);
        intent.SetData(global::Android.Net.Uri.Parse("package:" + packageName));

        try
     {
     StartActivity(intent);
  }
       catch (System.Exception ex)
       {
   System.Diagnostics.Debug.WriteLine($"[MainActivity] Failed to start battery optimization settings: {ex.Message}");
           }
       }
         else
   {
           System.Diagnostics.Debug.WriteLine("[MainActivity] Battery optimization already disabled");
       }
   }
      }
  }
         catch (System.Exception ex)
            {
        System.Diagnostics.Debug.WriteLine($"[MainActivity] Battery optimization request failed: {ex.Message}");
          }
    }
    }
}
