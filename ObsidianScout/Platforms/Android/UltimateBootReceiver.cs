using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;

namespace ObsidianScout.Platforms.Android
{
    /// <summary>
    /// ULTIMATE Boot Receiver - Uses WorkManager for maximum reliability
    /// </summary>
    [BroadcastReceiver(
        Enabled = true,
    Exported = true,
        DirectBootAware = true,
        Name = "com.companyname.obsidianscout.UltimateBootReceiver")]
    [IntentFilter(
        new[] { 
     Intent.ActionBootCompleted,
 Intent.ActionLockedBootCompleted  
        },
        Priority = 999)]
public class UltimateBootReceiver : BroadcastReceiver
    {
      private const string TAG = "UltimateBootReceiver";

        public override void OnReceive(Context? context, Intent? intent)
        {
  Log.Info(TAG, "========== ULTIMATE BOOT RECEIVER ==========");
     Log.Info(TAG, $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log.Info(TAG, $"Action: {intent?.Action}");
            
            if (context == null)
{
      Log.Error(TAG, "Context is null!");
   return;
   }

            try
            {
    // Check app launch flag
    var appLaunched = PersistentPreferences.GetAppLaunched(context);
           Log.Info(TAG, $"App launched before: {appLaunched}");

      if (!appLaunched)
       {
                    Log.Warn(TAG, "App never launched - showing reminder");
        ShowReminder(context);
         return;
          }

        Log.Info(TAG, "Starting service...");
        
         // Method 1: Direct service start
   StartServiceDirectly(context);
            
                // Method 2: Schedule with AlarmManager (5 seconds delay)
       ScheduleDelayedStart(context, 5000);
      
        // Method 3: Schedule with AlarmManager (30 seconds delay as backup)
      ScheduleDelayedStart(context, 30000);
    
                Log.Info(TAG, "========== BOOT HANDLING COMPLETE ==========");
}
            catch (System.Exception ex)
{
                Log.Error(TAG, $"Error: {ex.Message}");
   Log.Error(TAG, $"Stack: {ex.StackTrace}");
     }
        }

        private void StartServiceDirectly(Context context)
        {
            try
       {
       Log.Info(TAG, "[Method 1] Direct service start");
     
     var serviceIntent = new Intent(context, typeof(ForegroundNotificationService));
     serviceIntent.PutExtra("started_from_boot", true);
      serviceIntent.PutExtra("boot_method", "direct");
      
         if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
               context.StartForegroundService(serviceIntent);
    Log.Info(TAG, "[Method 1] StartForegroundService called");
       }
       else
      {
  context.StartService(serviceIntent);
         Log.Info(TAG, "[Method 1] StartService called");
  }
 }
            catch (System.Exception ex)
      {
     Log.Error(TAG, $"[Method 1] Failed: {ex.Message}");
      }
        }

        private void ScheduleDelayedStart(Context context, int delayMs)
        {
     try
       {
          Log.Info(TAG, $"[Method 2] Scheduling delayed start ({delayMs}ms)");
     
    var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
 if (alarmManager == null)
             {
        Log.Warn(TAG, "[Method 2] AlarmManager not available");
            return;
           }

        var intent = new Intent(context, typeof(DelayedBootReceiver));
       intent.PutExtra("delay_ms", delayMs);
                
       var pendingIntent = PendingIntent.GetBroadcast(
 context,
          delayMs, // Use delay as request code to make them unique
  intent,
        Build.VERSION.SdkInt >= BuildVersionCodes.S
               ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent);

         var triggerTime = SystemClock.ElapsedRealtime() + delayMs;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
    {
                if (alarmManager.CanScheduleExactAlarms())
 {
  alarmManager.SetExact(AlarmType.ElapsedRealtimeWakeup, triggerTime, pendingIntent);
            Log.Info(TAG, $"[Method 2] Exact alarm scheduled ({delayMs}ms)");
      }
        else
        {
         alarmManager.Set(AlarmType.ElapsedRealtimeWakeup, triggerTime, pendingIntent);
     Log.Info(TAG, $"[Method 2] Inexact alarm scheduled ({delayMs}ms)");
        }
          }
      else
        {
         alarmManager.SetExact(AlarmType.ElapsedRealtimeWakeup, triggerTime, pendingIntent);
          Log.Info(TAG, $"[Method 2] Exact alarm scheduled ({delayMs}ms)");
 }
            }
     catch (System.Exception ex)
            {
      Log.Error(TAG, $"[Method 2] Failed to schedule ({delayMs}ms): {ex.Message}");
 }
   }

  private void ShowReminder(Context context)
        {
            try
         {
     var nm = (NotificationManager?)context.GetSystemService(Context.NotificationService);
          if (nm == null) return;

      const string channelId = "obsidian_scout_channel";

           if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
          {
    var channel = new NotificationChannel(
                 channelId,
            "ObsidianScout",
  NotificationImportance.High);
        nm.CreateNotificationChannel(channel);
        }

         var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
                launchIntent?.AddFlags(ActivityFlags.NewTask);

  var pendingIntent = PendingIntent.GetActivity(
          context,
         0,
         launchIntent,
          Build.VERSION.SdkInt >= BuildVersionCodes.S
            ? PendingIntentFlags.Immutable
     : PendingIntentFlags.UpdateCurrent);

   var notification = new AndroidX.Core.App.NotificationCompat.Builder(context, channelId)
    .SetContentTitle("ObsidianScout")
  .SetContentText("Tap to enable notifications after restart")
     .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
             .SetContentIntent(pendingIntent)
        .SetAutoCancel(true)
      .SetPriority((int)NotificationPriority.High)
        .Build();

       nm.Notify(1000, notification);
    Log.Info(TAG, "Reminder notification shown");
       }
            catch (System.Exception ex)
            {
     Log.Error(TAG, $"Failed to show reminder: {ex.Message}");
        }
        }
    }

    /// <summary>
 /// Receives delayed boot triggers
    /// </summary>
    [BroadcastReceiver(
        Enabled = true,
        Exported = false,
        Name = "com.companyname.obsidianscout.DelayedBootReceiver")]
    public class DelayedBootReceiver : BroadcastReceiver
    {
        private const string TAG = "DelayedBootReceiver";

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context == null) return;

            var delayMs = intent?.GetIntExtra("delay_ms", 0) ?? 0;
         Log.Info(TAG, $"========== DELAYED START ({delayMs}ms) ==========");

  try
            {
       var serviceIntent = new Intent(context, typeof(ForegroundNotificationService));
     serviceIntent.PutExtra("started_from_boot", true);
  serviceIntent.PutExtra("boot_method", $"delayed_{delayMs}ms");

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
    context.StartForegroundService(serviceIntent);
        }
            else
        {
      context.StartService(serviceIntent);
        }

 Log.Info(TAG, $"Service started from delayed trigger ({delayMs}ms)");
   }
            catch (System.Exception ex)
            {
         Log.Error(TAG, $"Failed to start service: {ex.Message}");
            }
   }
    }
}
