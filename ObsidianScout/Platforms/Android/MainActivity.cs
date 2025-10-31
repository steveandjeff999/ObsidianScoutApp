using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Android;

namespace ObsidianScout
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            try
            {
                // Android13+ requires runtime permission for notifications
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.PostNotifications) != (int)Permission.Granted)
                    {
                        ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.PostNotifications },1001);
                    }
                }

                // Create a default notification channel to ensure notifications are delivered
                var context = global::Android.App.Application.Context;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    var channelId = "obsidian_scout_channel";
                    var channelName = "ObsidianScout Notifications";
                    var channelDesc = "General notifications from ObsidianScout";

                    var nm = (global::Android.App.NotificationManager)context.GetSystemService(global::Android.Content.Context.NotificationService);
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
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] Notification init failed: {ex.Message}");
            }
        }
    }
}
