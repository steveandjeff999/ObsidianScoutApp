using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using ObsidianScout.Services;

namespace ObsidianScout.Platforms.Windows;

public class LocalNotificationService : ILocalNotificationService
{
 private static INotificationNavigationService? _navigationService;
 private static bool _initialized = false;

 public LocalNotificationService()
 {
     InitializeNotificationHandling();
 }

 /// <summary>
 /// Initialize Windows notification handling with activation support.
 /// </summary>
 private void InitializeNotificationHandling()
 {
     if (_initialized) return;
      
     try
 {
         // Get the navigation service from DI
   _navigationService = IPlatformApplication.Current?.Services?.GetService<INotificationNavigationService>();
            
         // Register for notification activation
         AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
  
       // Register the app for notifications
         AppNotificationManager.Default.Register();
            
    _initialized = true;
   System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] Notification handling initialized");
     }
     catch (Exception ex)
     {
         System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Failed to initialize notification handling: {ex.Message}");
     }
 }

 /// <summary>
 /// Handle notification activation (tap).
 /// </summary>
 private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
 {
     try
     {
  System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] Notification tapped!");
      System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Arguments: {args.Argument}");

 // Parse the launch arguments
    var navData = ParseLaunchArguments(args.Argument);
            
         if (navData != null && navData.Count > 0)
         {
     System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Parsed {navData.Count} navigation parameters");
      
             // Navigate using the service
             if (_navigationService != null)
             {
  MainThread.BeginInvokeOnMainThread(async () =>
         {
          try
         {
  await _navigationService.NavigateFromNotificationAsync(navData);
System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] ? Navigation completed");
                }
           catch (Exception ex)
         {
              System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Navigation error: {ex.Message}");
    }
              });
   }
       else
 {
        System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] NavigationService not available");
    }
    }
     }
     catch (Exception ex)
     {
 System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] OnNotificationInvoked error: {ex.Message}");
     }
 }

 /// <summary>
 /// Parse launch arguments from notification activation.
 /// </summary>
 private static Dictionary<string, string>? ParseLaunchArguments(string arguments)
 {
   if (string.IsNullOrEmpty(arguments))
         return null;

     try
     {
   var result = new Dictionary<string, string>();
    
      // Arguments format: "type=chat&sourceType=dm&sourceId=user123"
    var pairs = arguments.Split('&', StringSplitOptions.RemoveEmptyEntries);
         foreach (var pair in pairs)
  {
           var keyValue = pair.Split('=', 2);
     if (keyValue.Length == 2)
             {
    var key = Uri.UnescapeDataString(keyValue[0]);
          var value = Uri.UnescapeDataString(keyValue[1]);
       result[key] = value;
   }
         }

    return result.Count > 0 ? result : null;
     }
     catch (Exception ex)
     {
       System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] ParseLaunchArguments error: {ex.Message}");
         return null;
     }
 }

 public Task ShowAsync(string title, string body, int id = 0)
 {
     try
     {
         // Validate inputs
         if (string.IsNullOrEmpty(title))
   title = "Notification";
         if (string.IsNullOrEmpty(body))
         body = "";

   var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
   if (toastXml == null)
       {
        System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] Failed to get toast template");
           return Task.CompletedTask;
         }

var stringElements = toastXml.GetElementsByTagName("text");
      if (stringElements == null || stringElements.Length < 2)
         {
             System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] Toast template missing text elements");
   return Task.CompletedTask;
      }

       stringElements[0].AppendChild(toastXml.CreateTextNode(title));
         stringElements[1].AppendChild(toastXml.CreateTextNode(body));

         var toast = new ToastNotification(toastXml);
   var notifier = ToastNotificationManager.CreateToastNotifier();
         if (notifier != null)
         {
         notifier.Show(toast);
}
       else
   {
      System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] Failed to create toast notifier");
     }
     }
     catch (System.Exception ex)
     {
         System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Toast failed: {ex.Message}");
   }

     return Task.CompletedTask;
 }

 public Task ShowWithDataAsync(string title, string message, int id, Dictionary<string, string> data)
 {
     try
     {
         // Validate inputs
 if (string.IsNullOrEmpty(title))
 title = "Notification";
     if (string.IsNullOrEmpty(message))
  message = "";

 System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Notification: {title} - {message}");
 
         if (data != null && data.Count > 0)
         {
       System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Data: {string.Join(", ", data.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
  }

         // Build launch arguments from data
   var launchArgs = BuildLaunchArguments(data);
         System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Launch args: {launchArgs}");

         // Use Windows App SDK notification builder for clickable notifications
         try
     {
      var builder = new AppNotificationBuilder()
     .AddArgument("notificationId", id.ToString())
       .AddText(title)
                 .AddText(message);

   // Add each data item as an argument
         if (data != null)
             {
     foreach (var kvp in data)
  {
        builder.AddArgument(kvp.Key, kvp.Value);
                 }
       }

    var notification = builder.BuildNotification();
       AppNotificationManager.Default.Show(notification);

 System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] ? AppNotification shown with click handler");
           return Task.CompletedTask;
   }
     catch (Exception ex)
         {
  System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] AppNotification failed: {ex.Message}, falling back to ToastNotification");
         }

      // Fallback: Use legacy ToastNotification with launch attribute
         ShowToastWithLaunchArgs(title, message, launchArgs);
     }
     catch (Exception ex)
 {
         System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] Error: {ex.Message}");
     }
        
  return Task.CompletedTask;
 }

 /// <summary>
 /// Build launch arguments string from notification data.
 /// </summary>
 private string BuildLaunchArguments(Dictionary<string, string>? data)
 {
     if (data == null || data.Count == 0)
     return string.Empty;

  var args = data.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
     return string.Join("&", args);
 }

 /// <summary>
 /// Show a toast notification with launch arguments (fallback method).
 /// </summary>
 private void ShowToastWithLaunchArgs(string title, string message, string launchArgs)
 {
 try
  {
         // Create custom XML with launch attribute
         var toastXmlString = $@"
<toast launch=""{System.Security.SecurityElement.Escape(launchArgs)}"">
    <visual>
        <binding template=""ToastText02"">
      <text id=""1"">{System.Security.SecurityElement.Escape(title)}</text>
     <text id=""2"">{System.Security.SecurityElement.Escape(message)}</text>
    </binding>
    </visual>
</toast>";

         var toastXml = new XmlDocument();
         toastXml.LoadXml(toastXmlString);

         var toast = new ToastNotification(toastXml);
  toast.Activated += (s, e) =>
       {
             System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] Legacy toast activated");
       // Handle activation for legacy toast
             if (e is ToastActivatedEventArgs args && !string.IsNullOrEmpty(args.Arguments))
  {
  var navData = ParseLaunchArguments(args.Arguments);
           if (navData != null && _navigationService != null)
       {
        MainThread.BeginInvokeOnMainThread(async () =>
         {
           await _navigationService.NavigateFromNotificationAsync(navData);
      });
  }
          }
         };

       var notifier = ToastNotificationManager.CreateToastNotifier();
         notifier?.Show(toast);

     System.Diagnostics.Debug.WriteLine("[LocalNotifications-Windows] ? Legacy toast shown with launch args");
     }
     catch (Exception ex)
     {
         System.Diagnostics.Debug.WriteLine($"[LocalNotifications-Windows] ShowToastWithLaunchArgs error: {ex.Message}");
         // Final fallback: show simple notification
    ShowAsync(title, message, 0);
     }
 }
}
