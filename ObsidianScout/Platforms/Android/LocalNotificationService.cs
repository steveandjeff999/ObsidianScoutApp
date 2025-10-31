using System.Threading.Tasks;
using ObsidianScout.Services;

namespace ObsidianScout.Platforms.Android;

public class LocalNotificationService : ILocalNotificationService
{
 const string CHANNEL_ID = "obsidian_scout_channel";
 const string CHANNEL_NAME = "ObsidianScout Notifications";
 const string CHANNEL_DESC = "General notifications from ObsidianScout";

 public Task ShowAsync(string title, string body, int id =0)
 {
 var context = global::Android.App.Application.Context;
 var notificationManager = global::AndroidX.Core.App.NotificationManagerCompat.From(context);

 // Create channel for Android8.0+
 if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
 {
 var channel = new global::Android.App.NotificationChannel(CHANNEL_ID, CHANNEL_NAME, global::Android.App.NotificationImportance.High)
 {
 Description = CHANNEL_DESC
 };

 var nm = (global::Android.App.NotificationManager)context.GetSystemService(global::Android.Content.Context.NotificationService);
 nm.CreateNotificationChannel(channel);
 }

 var builder = new global::AndroidX.Core.App.NotificationCompat.Builder(context, CHANNEL_ID)
 .SetContentTitle(title)
 .SetContentText(body)
 .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
 .SetPriority((int)global::Android.App.NotificationPriority.High)
 .SetAutoCancel(true);

 var notification = builder.Build();
 notificationManager.Notify(id ==0 ?1000 : id, notification);

 return Task.CompletedTask;
 }
}
