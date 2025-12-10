using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ObsidianScout.Services;

/// <summary>
/// Implementation of INotificationNavigationService that handles navigation
/// from notification taps across all app states (foreground, background, cold start).
/// </summary>
public class NotificationNavigationService : INotificationNavigationService
{
    private readonly object _lock = new();
    private bool _hasPendingNavigation;
    private string? _pendingNavigationUri;
    private Dictionary<string, string>? _pendingNavigationData;

    public bool HasPendingNavigation
    {
        get
   {
            lock (_lock)
          {
 return _hasPendingNavigation;
    }
        }
    }

    public string? PendingNavigationUri
    {
        get
    {
 lock (_lock)
      {
        return _pendingNavigationUri;
            }
      }
    }

    public Dictionary<string, string>? PendingNavigationData
    {
        get
    {
   lock (_lock)
        {
        return _pendingNavigationData != null 
    ? new Dictionary<string, string>(_pendingNavigationData) 
 : null;
         }
        }
    }

    public event EventHandler<NotificationNavigationEventArgs>? NavigationRequested;

  public void SetPendingNavigation(Dictionary<string, string> data)
    {
        if (data == null || data.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[NotificationNavigation] SetPendingNavigation called with empty data");
         return;
      }

        var navUri = BuildNavigationUri(data);
        if (string.IsNullOrEmpty(navUri))
        {
          System.Diagnostics.Debug.WriteLine("[NotificationNavigation] Could not build navigation URI from data");
  return;
        }

        lock (_lock)
      {
      _hasPendingNavigation = true;
  _pendingNavigationUri = navUri;
     _pendingNavigationData = new Dictionary<string, string>(data);
    }

    System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] Stored pending navigation: {navUri}");
    }

    public void ClearPendingNavigation()
    {
        lock (_lock)
        {
            _hasPendingNavigation = false;
        _pendingNavigationUri = null;
 _pendingNavigationData = null;
        }

        System.Diagnostics.Debug.WriteLine("[NotificationNavigation] Cleared pending navigation");
    }

    public async Task<bool> TryExecutePendingNavigationAsync()
    {
      string? navUri;
        Dictionary<string, string>? navData;

        lock (_lock)
        {
   if (!_hasPendingNavigation || string.IsNullOrEmpty(_pendingNavigationUri))
   {
        return false;
    }

            navUri = _pendingNavigationUri;
   navData = _pendingNavigationData != null 
  ? new Dictionary<string, string>(_pendingNavigationData) 
      : new Dictionary<string, string>();
      }

 System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] Executing pending navigation: {navUri}");

        try
        {
        var success = await ExecuteNavigationAsync(navUri, navData);
      
            if (success)
    {
     ClearPendingNavigation();
          System.Diagnostics.Debug.WriteLine("[NotificationNavigation] ? Pending navigation executed successfully");
    }

            return success;
        }
        catch (Exception ex)
        {
    System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] Error executing pending navigation: {ex.Message}");
            ClearPendingNavigation(); // Clear to prevent retry loops
      return false;
        }
    }

    public async Task NavigateFromNotificationAsync(Dictionary<string, string> data)
    {
        if (data == null || data.Count == 0)
   {
       System.Diagnostics.Debug.WriteLine("[NotificationNavigation] NavigateFromNotificationAsync called with empty data");
            return;
    }

        var navUri = BuildNavigationUri(data);
   if (string.IsNullOrEmpty(navUri))
        {
            System.Diagnostics.Debug.WriteLine("[NotificationNavigation] Could not build navigation URI from data");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] Immediate navigation requested: {navUri}");

        try
        {
            await ExecuteNavigationAsync(navUri, data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] Error in immediate navigation: {ex.Message}");
        }
    }

    private string? BuildNavigationUri(Dictionary<string, string> data)
    {
     if (!data.TryGetValue("type", out var type))
        {
          System.Diagnostics.Debug.WriteLine("[NotificationNavigation] No 'type' in notification data");
   return null;
      }

      string navUri;

        switch (type.ToLowerInvariant())
        {
            case "chat":
         navUri = BuildChatNavigationUri(data);
         break;

          case "match":
    navUri = BuildMatchNavigationUri(data);
     break;

            default:
              System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] Unknown notification type: {type}");
                navUri = "//MainPage";
           break;
        }

        return navUri;
  }

    private string BuildChatNavigationUri(Dictionary<string, string> data)
    {
        // Build chat navigation URI
  // Format: //ChatPage?sourceType=dm&sourceId=username OR //ChatPage?sourceType=group&sourceId=groupname
      
        var queryParams = new List<string>();

  if (data.TryGetValue("sourceType", out var sourceType) && !string.IsNullOrEmpty(sourceType))
        {
   queryParams.Add($"sourceType={Uri.EscapeDataString(sourceType)}");
        }

        if (data.TryGetValue("sourceId", out var sourceId) && !string.IsNullOrEmpty(sourceId))
        {
       queryParams.Add($"sourceId={Uri.EscapeDataString(sourceId)}");
      }

      if (data.TryGetValue("messageId", out var messageId) && !string.IsNullOrEmpty(messageId))
   {
      queryParams.Add($"messageId={Uri.EscapeDataString(messageId)}");
        }

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return $"//ChatPage{query}";
    }

    private string BuildMatchNavigationUri(Dictionary<string, string> data)
    {
        // Build match navigation URI
        // Format: //MatchPredictionPage?eventId=123&eventCode=ABC&matchNumber=1

 if (!data.TryGetValue("eventId", out var eventId) || string.IsNullOrEmpty(eventId))
        {
  // No event ID - just go to main page
 System.Diagnostics.Debug.WriteLine("[NotificationNavigation] Match notification without eventId, navigating to MainPage");
         return "//MainPage";
 }

        var queryParams = new List<string>
        {
   $"eventId={Uri.EscapeDataString(eventId)}"
        };

        if (data.TryGetValue("eventCode", out var eventCode) && !string.IsNullOrEmpty(eventCode))
        {
  queryParams.Add($"eventCode={Uri.EscapeDataString(eventCode)}");
   }

        if (data.TryGetValue("matchNumber", out var matchNumber) && !string.IsNullOrEmpty(matchNumber))
        {
            queryParams.Add($"matchNumber={Uri.EscapeDataString(matchNumber)}");
    }

        var query = "?" + string.Join("&", queryParams);
        return $"//MatchPredictionPage{query}";
    }

    private async Task<bool> ExecuteNavigationAsync(string navUri, Dictionary<string, string> data)
    {
        // Ensure we're on the main thread
        if (!MainThread.IsMainThread)
        {
            return await MainThread.InvokeOnMainThreadAsync(async () => await ExecuteNavigationAsync(navUri, data));
        }

        try
        {
    // Wait for Shell to be available
     var shellCurrent = Shell.Current;
            if (shellCurrent == null)
         {
                System.Diagnostics.Debug.WriteLine("[NotificationNavigation] Shell.Current is null, waiting...");
            
          // Wait up to 3 seconds for Shell to become available
          for (int i = 0; i < 30; i++)
   {
           await Task.Delay(100);
   shellCurrent = Shell.Current;
if (shellCurrent != null) break;
   }

       if (shellCurrent == null)
                {
            System.Diagnostics.Debug.WriteLine("[NotificationNavigation] Shell.Current still null after waiting");
        return false;
                }
            }

          // Raise navigation event before navigating
 var notificationType = data.TryGetValue("type", out var t) ? t : "unknown";
       NavigationRequested?.Invoke(this, new NotificationNavigationEventArgs(notificationType, data, navUri));

   // Execute navigation
         System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] Navigating to: {navUri}");
            await shellCurrent.GoToAsync(navUri);

       System.Diagnostics.Debug.WriteLine("[NotificationNavigation] ? Navigation completed");
     return true;
        }
    catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] Navigation failed: {ex.Message}");
            return false;
        }
    }
}
