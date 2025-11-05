using System.Threading.Tasks;
using ObsidianScout.Services;
using Android.App;
using Android.OS;
using Android.Content;
using AndroidX.Core.App;
using static Android.Provider.Settings;
using System.Text.RegularExpressions;

namespace ObsidianScout.Platforms.Android;

public class LocalNotificationService : ILocalNotificationService
{
 const string CHANNEL_ID = "obsidian_scout_channel";
 const string CHANNEL_NAME = "ObsidianScout Notifications";
 const string CHANNEL_DESC = "Match reminders and chat messages";

 // IMPORTANT: Use different group keys for different notification types to prevent grouping
 const string GROUP_KEY_CHAT = "chat_messages";
 const string GROUP_KEY_MATCH = "match_notifications";

 private readonly NotificationManager? _notificationManager;

 public LocalNotificationService()
 {
 var context = Platform.CurrentActivity ?? Platform.AppContext;
 _notificationManager = context.GetSystemService(global::Android.Content.Context.NotificationService) as NotificationManager;
 }

 public async Task ShowAsync(string title, string message, int id =0)
 {
 try
 {
 if (_notificationManager == null)
 {
 System.Diagnostics.Debug.WriteLine("[LocalNotifications] NotificationManager is null");
 return;
 }

 // Create notification channel for Android8.0+
 if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
 {
 var existingChannel = _notificationManager.GetNotificationChannel(CHANNEL_ID);
 if (existingChannel == null)
 {
 var channel = new NotificationChannel(
 CHANNEL_ID,
 CHANNEL_NAME,
 NotificationImportance.High)
 {
 Description = CHANNEL_DESC,
 LockscreenVisibility = NotificationVisibility.Public
 };

 channel.EnableVibration(true);
 channel.SetVibrationPattern(new long[] {0,250,250,250 });

 _notificationManager.CreateNotificationChannel(channel);
 System.Diagnostics.Debug.WriteLine("[LocalNotifications] Created HIGH priority channel with sound and vibration");
 }
 }

 var notification = new NotificationCompat.Builder(Platform.CurrentActivity ?? Platform.AppContext, CHANNEL_ID)
 .SetContentTitle(title)
 .SetContentText(message)
 .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
 .SetAutoCancel(true)
 .SetPriority((int)NotificationPriority.High)
 .SetCategory(NotificationCompat.CategoryMessage)
 .SetDefaults((int)(global::Android.App.NotificationDefaults.Sound | global::Android.App.NotificationDefaults.Vibrate))
 .SetVibrate(new long[] {0,250,250,250 })
 .SetGroup(GROUP_KEY_MATCH)
 .Build();

 _notificationManager.Notify(id, notification);

 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] ? Notification shown - ID: {id}, Title: {title}");
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Priority: HIGH, Sound: ENABLED, Vibration: ENABLED");

 await Task.CompletedTask;
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Error showing notification: {ex.Message}");
 }
 }

 public async Task ShowWithDataAsync(string title, string message, int id, Dictionary<string, string> data)
 {
 try
 {
 if (_notificationManager == null)
 {
 System.Diagnostics.Debug.WriteLine("[LocalNotifications] NotificationManager is null");
 return;
 }

 // Create notification channel if needed
 if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
 {
 var existingChannel = _notificationManager.GetNotificationChannel(CHANNEL_ID);
 if (existingChannel == null)
 {
 var channel = new NotificationChannel(
 CHANNEL_ID,
 CHANNEL_NAME,
 NotificationImportance.High)
 {
 Description = CHANNEL_DESC,
 LockscreenVisibility = NotificationVisibility.Public
 };

 channel.EnableVibration(true);
 channel.SetVibrationPattern(new long[] {0,250,250,250 });

 _notificationManager.CreateNotificationChannel(channel);
 }
 }

 // Create intent to launch MainActivity
 var context = Platform.CurrentActivity ?? Platform.AppContext;
 var intent = new Intent(context, typeof(MainActivity));
 
// CRITICAL: Set proper flags to ensure app launches correctly
 // - FLAG_ACTIVITY_SINGLE_TOP: Use existing MainActivity if it exists (prevents duplicates)
 // - FLAG_ACTIVITY_CLEAR_TOP: Clear activities above MainActivity
 intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
 
 // CRITICAL: Set action to ensure intent is unique and processed
 intent.SetAction($"obsidian_scout_notification_{id}_{DateTime.UtcNow.Ticks}");
 
 // Add deep link data
 System.Diagnostics.Debug.WriteLine("[LocalNotifications] Adding intent extras:");
 foreach (var kvp in data)
 {
 intent.PutExtra(kvp.Key, kvp.Value);
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] {kvp.Key} = {kvp.Value}");
 }

 // Determine group key based on notification type
 string groupKey = GROUP_KEY_CHAT; // default
 if (data != null && data.TryGetValue("type", out var typeVal) && string.Equals(typeVal, "match", StringComparison.OrdinalIgnoreCase))
 {
 groupKey = GROUP_KEY_MATCH;
 }

 // CRITICAL: Create pending intent with proper flags
 // - UpdateCurrent: Update the intent if one already exists with same request code
 // - Immutable (Android12+): Required for security, intent extras won't change
 var requestCode = id; // Use notification ID as request code for uniqueness
 var pendingIntentFlags = Build.VERSION.SdkInt >= BuildVersionCodes.S
 ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
 : PendingIntentFlags.UpdateCurrent;

 var pendingIntent = PendingIntent.GetActivity(
 context,
 requestCode, // UNIQUE request code per notification
 intent,
 pendingIntentFlags);
 
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Created PendingIntent:");
 System.Diagnostics.Debug.WriteLine($" RequestCode: {requestCode}");
 System.Diagnostics.Debug.WriteLine($" Flags: {pendingIntentFlags}");
 System.Diagnostics.Debug.WriteLine($" Action: {intent.Action}");

 // Build a short, single-line summary that includes both alliances (if present)
 string summaryText = CreateAllianceSummary(message);
 if (string.IsNullOrWhiteSpace(summaryText))
 {
 // fallback to a trimmed version of the message
 summaryText = message?.Replace("\n", " ") ?? string.Empty;
 if (summaryText.Length >120) summaryText = summaryText.Substring(0,117) + "...";
 }

 // For expanded view, remove the appended Red/Blue block so teams only appear in the collapsed preview
 string expandedText = RemoveAllianceBlock(message) ?? message;

 // Build notification with tap action
 var builder = new NotificationCompat.Builder(context, CHANNEL_ID)
 .SetContentTitle(title)
 .SetContentText(summaryText) // collapsed view shows the concise summary (includes both alliances)
 .SetStyle(new NotificationCompat.BigTextStyle().BigText(expandedText).SetBigContentTitle(title)) // full message when expanded (without duplicated teams)
 .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
 .SetAutoCancel(true)
 .SetPriority((int)NotificationPriority.High)
 .SetCategory(NotificationCompat.CategoryMessage)
 .SetDefaults((int)(global::Android.App.NotificationDefaults.Sound | global::Android.App.NotificationDefaults.Vibrate))
 .SetVibrate(new long[] {0,250,250,250 })
 .SetContentIntent(pendingIntent) // Set tap action
 .SetOnlyAlertOnce(false) // Always alert for each message
 .SetGroup(groupKey)
 .SetGroupAlertBehavior(NotificationCompat.GroupAlertAll)
 .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
 .SetShowWhen(true);

 // Build final notification
 var notificationFinal = builder.Build();

 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Built notification:");
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications]ID: {id}");
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Title: {title}");
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Message: {message}");
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Group: {groupKey}");
 
 // Show notification
 _notificationManager.Notify(id, notificationFinal);

 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] ? Chat/match notification with data shown - ID: {id}");

 await Task.CompletedTask;
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Error showing notification with data: {ex.Message}");
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Stack trace: {ex.StackTrace}");
 }
 }

 private string CreateAllianceSummary(string message)
 {
 if (string.IsNullOrWhiteSpace(message)) return string.Empty;

 // Try regex-based parsing to handle multiple formats such as:
 // "Red:5454,5568,9988"
 // "red(5454,5568,9988)"
 // "Blue:"
 string red = string.Empty;
 string blue = string.Empty;

 try
 {
 var regex = new Regex(@"(?i)\b(?<side>red|blue)\b\s*[:(]?\s*(?<teams>[0-9,\s]+)?\)?");
 var matches = regex.Matches(message);
 foreach (Match m in matches)
 {
 if (!m.Success) continue;
 var side = m.Groups["side"].Value?.Trim();
 var teams = m.Groups["teams"].Value?.Trim();
 if (string.IsNullOrEmpty(teams)) teams = string.Empty;

 if (side.Equals("red", StringComparison.OrdinalIgnoreCase))
 red = teams;
 else if (side.Equals("blue", StringComparison.OrdinalIgnoreCase))
 blue = teams;
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications] Alliance regex parse failed: {ex.Message}");
 }

 // Fallback: existing line-based parsing if regex didn't find anything
 if (string.IsNullOrEmpty(red) && string.IsNullOrEmpty(blue))
 {
 var lines = message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
 foreach (var line in lines)
 {
 var trimmed = line.Trim();
 if (trimmed.StartsWith("Red:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("red(", StringComparison.OrdinalIgnoreCase))
 {
 var val = trimmed.IndexOf(':') >=0 ? trimmed.Substring(trimmed.IndexOf(':') +1).Trim() : trimmed;
 // remove trailing/leading parentheses
 val = val.Trim('(', ')', ' ');
 red = val;
 }
 else if (trimmed.StartsWith("Blue:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("blue(", StringComparison.OrdinalIgnoreCase))
 {
 var val = trimmed.IndexOf(':') >=0 ? trimmed.Substring(trimmed.IndexOf(':') +1).Trim() : trimmed;
 val = val.Trim('(', ')', ' ');
 blue = val;
 }
 }
 }

 // Normalize comma spacing
 if (!string.IsNullOrEmpty(red)) red = string.Join(", ", red.Split(new[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries));
 if (!string.IsNullOrEmpty(blue)) blue = string.Join(", ", blue.Split(new[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries));

 if (!string.IsNullOrEmpty(red) && !string.IsNullOrEmpty(blue))
 {
 var summary = $"Red: {red} • Blue: {blue}";
 if (summary.Length >120) summary = summary.Substring(0,117) + "...";
 return summary;
 }

 if (!string.IsNullOrEmpty(red))
 {
 var summary = $"Red: {red}";
 if (summary.Length >120) summary = summary.Substring(0,117) + "...";
 return summary;
 }

 if (!string.IsNullOrEmpty(blue))
 {
 var summary = $"Blue: {blue}";
 if (summary.Length >120) summary = summary.Substring(0,117) + "...";
 return summary;
 }

 return string.Empty;
 }

 // Remove trailing "Red:/Blue:" block added by BackgroundNotificationService so expanded text doesn't duplicate teams
 private string RemoveAllianceBlock(string message)
 {
 if (string.IsNullOrWhiteSpace(message))
 return message;

 try
 {
 // Match optional whitespace/newlines, then Red: ... then newline then Blue: ... at end of string
 var regex = new Regex(@"(\r?\n)*\s*Red:.*(\r?\n)+\s*Blue:.*\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
 var result = regex.Replace(message, string.Empty).TrimEnd();
 return string.IsNullOrEmpty(result) ? message : result;
 }
 catch
 {
 return message;
 }
 }
}
