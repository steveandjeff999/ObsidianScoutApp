using System.Threading.Tasks;
using Windows.UI.Notifications;
using ObsidianScout.Services;

namespace ObsidianScout.Platforms.Windows;

public class LocalNotificationService : ILocalNotificationService
{
 public Task ShowAsync(string title, string body, int id =0)
 {
 try
 {
 var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
 var stringElements = toastXml.GetElementsByTagName("text");
 stringElements[0].AppendChild(toastXml.CreateTextNode(title));
 stringElements[1].AppendChild(toastXml.CreateTextNode(body));

 var toast = new ToastNotification(toastXml);
 ToastNotificationManager.CreateToastNotifier().Show(toast);
 }
 catch (System.Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"Windows toast failed: {ex.Message}");
 }

 return Task.CompletedTask;
 }

 public Task ShowWithDataAsync(string title, string message, int id, Dictionary<string, string> data)
 {
  try
     {
   // For Windows, use the regular ShowAsync method
   // Windows toast notifications with deep linking require UWP-specific XML and protocol handlers
     // For now, just show the notification - deep linking would require additional setup
        System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Notification: {title} - {message}");
   System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Data: {string.Join(", ", data.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
  
   // Show notification using standard method
            return ShowAsync(title, message, id);
     }
        catch (Exception ex)
    {
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Error: {ex.Message}");
  return Task.CompletedTask;
        }
 }
}
