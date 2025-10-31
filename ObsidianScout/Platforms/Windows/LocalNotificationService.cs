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
}
