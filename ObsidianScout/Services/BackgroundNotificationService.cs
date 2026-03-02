using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ObsidianScout.Models;

namespace ObsidianScout.Services;

public interface IBackgroundNotificationService
{
    Task StartAsync();
    void Stop();
    Task ForceCheckAsync();
    void OnAppBackground();
    void OnAppForeground();
}

public class BackgroundNotificationService : IBackgroundNotificationService, IDisposable
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settings_service;
    private readonly ILocalNotificationService? _localNotificationService;
  
    private Timer? _timer;
    private readonly object _timerLock = new object(); // Thread-safety for timer operations
    private TimeSpan _currentPollInterval = TimeSpan.FromSeconds(60); // Start with 60 seconds (1 min)
    private readonly TimeSpan _minPollInterval = TimeSpan.FromSeconds(60); // Minimum 1 minute
    private readonly TimeSpan _maxPollInterval = TimeSpan.FromSeconds(120); // Maximum 2 minutes
    private volatile bool _running; // volatile for thread-safe reads
    private volatile bool _isInBackground;
    private readonly SemaphoreSlim _pollLock = new SemaphoreSlim(1, 1);
    private int _consecutiveEmptyPolls = 0;
    private const int EMPTY_POLLS_BEFORE_SLOWDOWN = 3; // Slow down after 3 empty polls (instead of 5)
    private volatile bool _disposed = false;
    
    // Tracking data file path
    private readonly string _trackingFilePath;
    private NotificationTrackingData _trackingData = new();

    // Constants
    private const int CLEANUP_RETENTION_DAYS = 7; // Keep sent records for 7 days
    private const int NOTIFICATION_BUFFER_MINUTES = 5; // Show notifications 5 minutes before scheduled time

    public BackgroundNotificationService(
     IApiService apiService, 
        ISettingsService settingsService,
        ILocalNotificationService? localNotificationService = null)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _settings_service = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
     _localNotificationService = localNotificationService;
     
        // Set up tracking file path safely
        try
     {
        var appDataPath = FileSystem.AppDataDirectory;
            _trackingFilePath = Path.Combine(appDataPath, "notification_tracking.json");
        }
        catch (Exception ex)
        {
  System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Failed to get app data path: {ex.Message}");
         _trackingFilePath = "notification_tracking.json"; // Fallback
        }
        
        // Load existing tracking data - fire and forget safely
        _ = SafeLoadTrackingDataAsync();
    }

    private async Task SafeLoadTrackingDataAsync()
    {
        try
     {
   await LoadTrackingDataAsync();
        }
        catch (Exception ex)
        {
     System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] SafeLoadTrackingDataAsync error: {ex.Message}");
            _trackingData = new NotificationTrackingData
            {
         LastPollTime = DateTime.UtcNow,
  LastCleanupTime = DateTime.UtcNow
 };
    }
    }

    public async Task StartAsync()
    {
        if (_running || _disposed) return;

        // Check user setting: if notifications disabled, do not start polling
     try
 {
  var enabled = await _settings_service.GetNotificationsEnabledAsync();
      if (!enabled)
    {
     System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] Notifications disabled by user - service will not start");
      return;
    }
        }
     catch (Exception ex)
        {
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Failed to read notifications setting: {ex.Message}");
        }

        _running = true;
        System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] Starting background notification service");
        System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] Power optimization: Adaptive polling enabled");
 
        // Load tracking data
        await SafeLoadTrackingDataAsync();
   
        // Start timer - poll immediately then every interval
        lock (_timerLock)
        {
            if (_disposed) return; // Check disposed state under lock
            _timer = new Timer(async _ => await SafePollAsync(), null, TimeSpan.FromSeconds(5), _currentPollInterval); // Delay first poll by 5 seconds
        }
 
        System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Service started - polling every {_currentPollInterval.TotalSeconds} seconds");
    }

    private async Task SafePollAsync()
    {
        try
        {
            await PollAsync();
        }
     catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] SafePollAsync error: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (!_running) return;
      
        System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] Stopping background notification service");

        lock (_timerLock)
        {
            try
            {
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                _timer?.Dispose();
                _timer = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Error stopping timer: {ex.Message}");
            }
        }

        _running = false;
    }

    public async Task ForceCheckAsync()
    {
        if (_disposed) return;

        System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] Force check requested - resetting to fast polling");
        _consecutiveEmptyPolls = 0;
        _currentPollInterval = _minPollInterval;
        SafeUpdateTimerInterval();
        await SafePollAsync();
    }

    public void OnAppBackground()
    {
        _isInBackground = true;
        System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] App entered background - optimizing for battery");
    }

    public void OnAppForeground()
    {
        _isInBackground = false;
        System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] App entered foreground - restoring normal polling");
        
        // Reset to fast polling immediately when returning to foreground
        _consecutiveEmptyPolls = 0;
        if (_currentPollInterval > _minPollInterval)
        {
            _currentPollInterval = _minPollInterval;
            SafeUpdateTimerInterval();
        }
        
        // Trigger an immediate check
        Task.Run(async () => await SafePollAsync());
    }

    private void SafeUpdateTimerInterval()
    {
    try
        {
        UpdateTimerInterval();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] SafeUpdateTimerInterval error: {ex.Message}");
        }
    }

    private void UpdateTimerInterval()
    {
        lock (_timerLock)
        {
            if (_timer != null && !_disposed)
            {
                try
                {
                    _timer.Change(_currentPollInterval, _currentPollInterval);
                }
                catch (ObjectDisposedException)
                {
                    // Timer was disposed, ignore
                    System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] Timer already disposed during interval update");
                }
            }
        }
    }

    private void AdjustPollingInterval(int notificationsFound)
    {
        try
        {
            if (notificationsFound > 0)
            {
                _consecutiveEmptyPolls = 0;
                if (_currentPollInterval > _minPollInterval)
                {
                    _currentPollInterval = _minPollInterval;
                    SafeUpdateTimerInterval();
                    System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Activity detected - increased polling to {_currentPollInterval.TotalSeconds}s");
                }
            }
            else
            {
                _consecutiveEmptyPolls++;
     
                // If in background, allow slowing down more aggressively (up to 5 mins)
                var maxInterval = _isInBackground ? TimeSpan.FromMinutes(5) : _maxPollInterval;
                var slowdownThreshold = _isInBackground ? 1 : EMPTY_POLLS_BEFORE_SLOWDOWN;

                if (_consecutiveEmptyPolls >= slowdownThreshold)
                {
                    var newInterval = TimeSpan.FromSeconds(Math.Min(
                        _currentPollInterval.TotalSeconds * 1.5,
                        maxInterval.TotalSeconds
                    ));
      
                    if (newInterval != _currentPollInterval)
                    {
                        _currentPollInterval = newInterval;
                        SafeUpdateTimerInterval();
                        System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] No activity - reduced polling to {_currentPollInterval.TotalSeconds}s (saves battery)");
                    }
         
                    _consecutiveEmptyPolls = 0;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] AdjustPollingInterval error: {ex.Message}");
        }
    }

    private async Task PollAsync()
  {
        if (_disposed) return;

        // Before doing any work, check if notifications are enabled
        try
 {
    var enabled = await _settings_service.GetNotificationsEnabledAsync();
            if (!enabled)
    {
                System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] Skipping poll because notifications are disabled");
         return;
    }
        }
  catch (Exception ex)
        {
          System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Failed to read notifications setting during poll: {ex.Message}");
        }

 // Prevent concurrent polling with timeout
 if (!await _pollLock.WaitAsync(100))
        {
     System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] Poll already in progress, skipping");
    return;
   }

        try
        {
            System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] === POLL START ===");
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Current interval: {_currentPollInterval.TotalSeconds}s");
            var pollStartTime = DateTime.UtcNow;

            int notificationsFound = 0;

            // OPTIMIZED: Use single combined endpoint to fetch chat state and scheduled notifications
            // This minimizes battery usage by reducing radio active time to a single API call
            notificationsFound = await SafeCheckUnreadNotificationsAsync();
            
            // Step 4: Update last poll time
            _trackingData.LastPollTime = pollStartTime;
  
            // Step 5: Cleanup old records if needed (once per day)
            if ((pollStartTime - _trackingData.LastCleanupTime).TotalDays >= 1)
            {
                SafeCleanupOldRecords();
                _trackingData.LastCleanupTime = pollStartTime;
            }
 
            // Step 6: Save tracking data - run on background thread
            await SafeSaveTrackingDataAsync();
  
            // Step 7: Adaptive polling - adjust interval based on activity
            AdjustPollingInterval(notificationsFound);
   
            var pollDuration = (DateTime.UtcNow - pollStartTime).TotalSeconds;
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] === POLL END ({pollDuration:F1}s) - Found {notificationsFound} notifications ===");
        }
      catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Poll error: {ex.Message}");
        }
 finally
  {
            try
 {
      _pollLock.Release();
            }
   catch { }
 }
    }

    private async Task<int> SafeCheckUnreadNotificationsAsync()
  {
      try
      {
          return await Task.Run(async () => await CheckUnreadNotificationsAsync());
      }
      catch (Exception ex)
      {
          System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] SafeCheckUnreadNotificationsAsync error: {ex.Message}");
          return 0;
      }
  }

    private void SafeCleanupOldRecords()
    {
    try
        {
 CleanupOldRecords();
 }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] SafeCleanupOldRecords error: {ex.Message}");
        }
    }

    private async Task SafeSaveTrackingDataAsync()
    {
      try
   {
  await Task.Run(async () => await SaveTrackingDataAsync());
}
        catch (Exception ex)
        {
     System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] SafeSaveTrackingDataAsync error: {ex.Message}");
        }
    }

    private async Task<int> CheckUnreadNotificationsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] Checking unread notifications (combined endpoint)...");

            // Use combined endpoint - fetches both chat state and scheduled notifications in one API call
            var unreadResponse = await _apiService.GetUnreadNotificationsAsync();

            if (!unreadResponse.Success)
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Failed to fetch unread notifications: {unreadResponse.Error}");
                return 0;
            }

            int notificationsFound = 0;

            // Process scheduled notifications
            if (unreadResponse.Scheduled?.Notifications != null && unreadResponse.Scheduled.Notifications.Count > 0)
            {
                var now = DateTime.UtcNow;
                var bufferTime = now.AddMinutes(NOTIFICATION_BUFFER_MINUTES);

                System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Found {unreadResponse.Scheduled.Notifications.Count} scheduled notifications");

                var dueNotifications = unreadResponse.Scheduled.Notifications
                    .Where(n =>
                    {
                        // Check if push delivery is enabled (REQUIRED!)
                        if (n.DeliveryMethods == null || !n.DeliveryMethods.TryGetValue("push", out var pushEnabled) || !pushEnabled)
                        {
                            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Skipping notification {n.Id} - push not enabled");
                            return false;
                        }

                        // Check if notification is pending
                        if (n.Status != "pending")
                            return false;

                        // Check if already sent locally
                        if (WasNotificationSent(n.Id))
                            return false;

                        // Check if notification is due (within buffer time)
                        var scheduledUtc = n.ScheduledFor.Kind == DateTimeKind.Utc ? n.ScheduledFor : n.ScheduledFor.ToUniversalTime();
                        return scheduledUtc <= bufferTime;
                    })
                    .OrderBy(n => n.ScheduledFor) // Send in chronological order
                    .ToList();

                if (dueNotifications.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Found {dueNotifications.Count} DUE scheduled notifications to send now!");

                    foreach (var notification in dueNotifications)
                    {
                        await ShowNotificationAsync(notification, isCatchUp: false);
                        notificationsFound++;
                    }
                }
            }

            // Process chat messages
            if (unreadResponse.ChatState != null)
            {
                var chatState = unreadResponse.ChatState;
                var unreadCount = chatState.UnreadCount;
                System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Unread messages: {unreadCount}");

                if (unreadCount > 0)
                {
                    // Fetch actual unread messages to show individual notifications
                    var chatNotificationsSent = await FetchAndShowUnreadMessagesAsync(chatState);

                    if (chatNotificationsSent > 0)
                    {
                        // Update tracking
                        _trackingData.LastChatUnreadCount = unreadCount;
                        notificationsFound += chatNotificationsSent;
                    }
                }
                else
                {
                    // Reset tracking when no unread messages
                    _trackingData.LastChatUnreadCount = 0;
                }
            }

            return notificationsFound;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Error checking unread notifications: {ex.Message}");
            return 0;
        }
    }

    private async Task<int> FetchAndShowUnreadMessagesAsync(ChatState chatState)
    {
  try
    {
 var notificationsSent = 0;
     var lastSource = chatState.LastSource;

  if (lastSource == null)
     {
  System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] No last source - skipping generic notification");
     // REMOVED: No longer showing generic notification when no source
        return 0;
}

       // CRITICAL FIX: Check if chat state already includes unread messages
 if (chatState.UnreadMessages != null && chatState.UnreadMessages.Count > 0)
      {
     System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Using {chatState.UnreadMessages.Count} messages from chat state");
      
       // Get messages we haven't notified about yet
     var unnotifiedMessages = chatState.UnreadMessages
   .Where(m => !WasChatMessageNotified(m.Id))
     .OrderBy(m => m.Timestamp) // Show oldest first
.Take(10) // Limit to 10 notifications at once
        .ToList();

   System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Found {unnotifiedMessages.Count} unnotified messages from chat state");

              foreach (var message in unnotifiedMessages)
       {
     await ShowIndividualChatNotificationAsync(message, lastSource);
       RecordChatMessageNotification(message.Id);
    notificationsSent++;
        }

 if (notificationsSent > 0)
           {
       return notificationsSent;
     }
     }

            // Fallback: Try to fetch messages via API if not in chat state
          ChatMessagesResponse? messagesResponse = null;

  if (lastSource.Type == "dm" && !string.IsNullOrEmpty(lastSource.Id))
  {
     // Fetch direct messages with specific user
    System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Chat state didn't include messages, trying API fetch with user: {lastSource.Id}");
   
 // CRITICAL: Check if lastSource.Id looks like a team number (all digits)
        if (System.Text.RegularExpressions.Regex.IsMatch(lastSource.Id, @"^\d+$"))
  {
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] WARNING: lastSource.Id '{lastSource.Id}' appears to be a team number, not a username!");
      System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] This will likely cause API 404 error. Server should return actual username in chat state.");
}
    
    messagesResponse = await _apiService.GetChatMessagesAsync(
     type: "dm",
        user: lastSource.Id,
   limit: 50
   );
  
    // Check for API failure
if (messagesResponse != null && !messagesResponse.Success)
   {
  System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] API call failed for user: {lastSource.Id}");
    System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] This usually means:");
    System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   1. lastSource.Id is a team number instead of username");
     System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   2. Server's chat state is returning incorrect data");
   System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   3. The other user doesn't exist or isn't on the same team");
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Check server logs for 404 'User not found' error");
  }
       }
  else if (lastSource.Type == "group" && !string.IsNullOrEmpty(lastSource.Id))
{
     // Fetch group messages
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Fetching group messages: {lastSource.Id}");
   messagesResponse = await _apiService.GetChatMessagesAsync(
  type: null, // Let server infer from group param
  group: lastSource.Id,
  limit: 50
      );
   }

  if (messagesResponse?.Success == true && messagesResponse.Messages != null && messagesResponse.Messages.Count > 0)
 {
 // Get messages we haven't notified about yet
  var unnotifiedMessages = messagesResponse.Messages
   .Where(m => !WasChatMessageNotified(m.Id))
       .OrderBy(m => m.Timestamp) // Show oldest first
    .Take(10) // Limit to 10 notifications at once
    .ToList();

  System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Found {unnotifiedMessages.Count} unnotified messages from API");

     foreach (var message in unnotifiedMessages)
   {
        await ShowIndividualChatNotificationAsync(message, lastSource);
 RecordChatMessageNotification(message.Id);
   notificationsSent++;
        }

  return notificationsSent;
   }
       else
    {
    // REMOVED: No longer showing generic fallback notification
  System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Could not fetch messages - no notification shown");
System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] messagesResponse Success: {messagesResponse?.Success}");
   System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Message count: {messagesResponse?.Messages?.Count ?? 0}");
    
     return 0;
}
   }
   catch (Exception ex)
      {
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Error fetching unread messages: {ex.Message}");
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Stack trace: {ex.StackTrace}");
 // REMOVED: No longer showing generic fallback notification on error
     return 0;
     }
 }

    private bool WasChatMessageNotified(string messageId)
    {
      return _trackingData.NotifiedChatMessageIds.Contains(messageId);
    }

    private void RecordChatMessageNotification(string messageId)
    {
        if (!_trackingData.NotifiedChatMessageIds.Contains(messageId))
 {
            _trackingData.NotifiedChatMessageIds.Add(messageId);
      System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Recorded chat message notification: {messageId}");
      }

        // Keep only last 100 message IDs to prevent unbounded growth
if (_trackingData.NotifiedChatMessageIds.Count > 100)
        {
   var toRemove = _trackingData.NotifiedChatMessageIds.Count - 100;
        _trackingData.NotifiedChatMessageIds.RemoveRange(0, toRemove);
      System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Trimmed {toRemove} old message IDs");
  }
    }

    private async Task ShowIndividualChatNotificationAsync(ChatMessage message, ChatMessageSource source)
    {
        try
   {
            // Format title based on source
            var title = source.Type switch
       {
   "dm" => message.Sender,
      "group" => $"{message.Sender} in {source.Id}",
        _ => message.Sender
      };

      // Use message text as notification body
        var messageText = message.Text;
      if (string.IsNullOrWhiteSpace(messageText))
         {
        messageText = "New message";
         }
     else if (messageText.Length > 100)
      {
messageText = messageText.Substring(0, 97) + "...";
    }

       System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Showing individual chat notification:");
  System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   Title: {title}");
    System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   Message: {messageText}");
   System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   Source: {source.Type}/{source.Id}");
    System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   MessageId: {message.Id}");

    // CRITICAL FIX: Generate TRULY UNIQUE notification ID using timestamp
  // This prevents Android from grouping/replacing notifications
  // Use message timestamp to ensure uniqueness
    var timestampTicks = message.Timestamp.Ticks;
    // Take last 8 digits to keep ID reasonable size but unique per message
    var notificationId = 9000 + (int)(Math.Abs(timestampTicks) % 999999);
    
    System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   Notification ID: {notificationId}");

     if (_localNotificationService != null)
          {
     // Create deep link data
      var deepLinkData = new Dictionary<string, string>
     {
          { "type", "chat" },
        { "sourceType", source.Type },
     { "sourceId", source.Id ?? "" },
      { "messageId", message.Id }
  };

    await _localNotificationService.ShowWithDataAsync(
         title, 
     messageText, 
      notificationId,
  deepLinkData
   );
       }
    else
      {
 // Fallback to UI alert if notification service not available
MainThread.BeginInvokeOnMainThread(async () =>
       {
     try
    {
        await Shell.Current.DisplayAlert(title, messageText, "OK");
     }
  catch { }
 });
     }

System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] ? Individual chat notification shown (ID: {notificationId})");
   }
        catch (Exception ex)
        {
       System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Error showing individual chat notification: {ex.Message}");
   }
    }

    private async Task ShowNotificationAsync(ScheduledNotification notification, bool isCatchUp)
{
        try
        {
    var title = FormatNotificationTitle(notification, isCatchUp);
var message = FormatNotificationMessage(notification);
   
   // Try to append match teams if available
   try
   {
   int? eventIdForMatch = notification.EventId;
   if (!eventIdForMatch.HasValue && !string.IsNullOrEmpty(notification.EventCode))
   {
   eventIdForMatch = await TryGetEventIdFromCodeAsync(notification.EventCode);
   }
   
   if (notification.MatchNumber.HasValue && eventIdForMatch.HasValue)
 {
 var matchesResp = await _apiService.GetMatchesAsync(eventIdForMatch.Value);
 if (matchesResp != null && matchesResp.Success && matchesResp.Matches != null)
 {
 var match = matchesResp.Matches.FirstOrDefault(m => m.MatchNumber == notification.MatchNumber.Value);
 if (match != null)
 {
 // Extract only numeric team IDs from whatever format the API returned
 var red = ExtractTeamList(match.RedAlliance);
 var blue = ExtractTeamList(match.BlueAlliance);

 var teamsText = $"\n\nRed: {red}\nBlue: {blue}";
 message += teamsText;
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Appended teams to message for match {notification.MatchNumber}: Red='{red}' Blue='{blue}'");
 }
 else
 {
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Match not found for number {notification.MatchNumber} in event {eventIdForMatch}");
 }
 }
 else
 {
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Failed to load matches for event {eventIdForMatch}");
 }
 }
   }
   catch (Exception ex)
   {
   System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Error fetching match teams: {ex.Message}");
   }
   
   System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Showing notification: {title}");
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Message: {message}");
     System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   Catch-up: {isCatchUp}");
     System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   EventCode: {notification.EventCode}");
        System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   EventId: {notification.EventId}");
        System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   MatchNumber: {notification.MatchNumber}");
       
        // Show notification using platform service
    if (_localNotificationService != null)
        {
  // CRITICAL: Always add deep link data for match notifications
    // Even if eventCode/eventId is missing, we still want tap to open the app
     var deepLinkData = new Dictionary<string, string>
            {
        { "type", "match" }
            };
  
  if (!string.IsNullOrEmpty(notification.EventCode))
 {
    deepLinkData["eventCode"] = notification.EventCode;
            }

            // Try to add eventId - if missing, try to look up
  int? eventId = notification.EventId;
    if (!eventId.HasValue && !string.IsNullOrEmpty(notification.EventCode))
      {
    // Try to look up eventId from eventCode
           eventId = await TryGetEventIdFromCodeAsync(notification.EventCode);
          if (eventId.HasValue)
            {
        System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   ? Looked up eventId {eventId} from eventCode {notification.EventCode}");
        }
          }

            if (eventId.HasValue)
 {
                deepLinkData["eventId"] = eventId.Value.ToString();
      }

     if (notification.MatchNumber.HasValue)
  {
     deepLinkData["matchNumber"] = notification.MatchNumber.ToString();
            }
   
    System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Adding deep link data to match notification:");
            foreach (var kvp in deepLinkData)
            {
         System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications]   {kvp.Key} = {kvp.Value}");
     }
      
       // Always use ShowWithDataAsync so notification is tappable
        await _localNotificationService.ShowWithDataAsync(title, message, notification.Id, deepLinkData);
        }
        else
   {
            // Fallback to UI alert if notification service not available
 MainThread.BeginInvokeOnMainThread(async () =>
 {
            try
{
        await Shell.Current.DisplayAlert(title, message, "OK");
        }
        catch { }
         });
        }
  
        // Record that we sent this notification
        RecordSentNotification(notification, isCatchUp);

   System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] ✓ Notification shown and recorded");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Error showing notification {notification.Id}: {ex.Message}");
    }
}

private async Task<int?> TryGetEventIdFromCodeAsync(string eventCode)
{
 try
 {
       System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Trying to look up eventId for code: {eventCode}");
       
       // Try to get events from API
  var eventsResponse = await _apiService.GetEventsAsync();
            
   if (eventsResponse.Success && eventsResponse.Events != null)
            {
        // Try to get season from game config for proper event code matching
        var configResponse = await _apiService.GetGameConfigAsync();
        string seasonCode = eventCode;
        
        if (configResponse.Success && configResponse.Config != null && configResponse.Config.Season > 0)
        {
            var seasonStr = configResponse.Config.Season.ToString();
            // If the event code already starts with the season, use it as-is
            if (!eventCode.StartsWith(seasonStr, StringComparison.OrdinalIgnoreCase))
            {
                seasonCode = $"{configResponse.Config.Season}{eventCode}";
            }
        }

        // Try exact match with season+code (primary), then raw code only (fallback)
        // No suffix matching to avoid wrong year events (e.g., 2026arli instead of 2025arli)
        var matchingEvent = eventsResponse.Events.FirstOrDefault(e => 
            string.Equals(e.Code, seasonCode, StringComparison.OrdinalIgnoreCase))
            ?? eventsResponse.Events.FirstOrDefault(e => 
            string.Equals(e.Code, eventCode, StringComparison.OrdinalIgnoreCase));
    
        if (matchingEvent != null)
       {
       System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] ✓ Found event: {matchingEvent.Name} (ID: {matchingEvent.Id})");
     return matchingEvent.Id;
       }
    else
       {
         System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] ✗ No event found with code: {eventCode}");
         }
       }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] ✗ Failed to get events from API");
  }
     }
        catch (Exception ex)
     {
 System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Error looking up eventId: {ex.Message}");
        }
        
        return null;
    }

    private string FormatNotificationTitle(ScheduledNotification notification, bool isCatchUp)
    {
        if (!string.IsNullOrEmpty(notification.NotificationType))
      {
     return FormatNotificationType(notification.NotificationType);
   }
        
        return "Notification";
    }

    private string FormatNotificationMessage(ScheduledNotification notification)
    {
        // Always show the scheduled match time (local) to avoid "starting in0 minutes" confusion
        var now = DateTime.UtcNow;
        var scheduledUtc = notification.ScheduledFor.Kind == DateTimeKind.Utc
       ? notification.ScheduledFor
       : notification.ScheduledFor.ToUniversalTime();
        
        var scheduledLocal = scheduledUtc.ToLocalTime();
        var scheduledText = scheduledLocal.ToString("g"); // short date/time
        
        var timeUntilMatch = scheduledUtc - now;
        
        // Build a friendly relative line but always include the scheduled time
        string timeText;
        if (timeUntilMatch.TotalMinutes >=1)
        {
         if (timeUntilMatch.TotalMinutes <60)
         {
           var minutes = (int)Math.Ceiling(timeUntilMatch.TotalMinutes);
    timeText = $"Match starting in {minutes} minute{(minutes !=1 ? "s" : "")}.";
         }
         else if (timeUntilMatch.TotalHours <24)
         {
           var hours = (int)Math.Floor(timeUntilMatch.TotalHours);
  var minutes = (int)Math.Round(timeUntilMatch.TotalMinutes %60);
 timeText = minutes >0 ? $"Match starting in {hours}h {minutes}m." : $"Match starting in {hours} hour{(hours !=1 ? "s" : "")}.";
         }
         else
         {
           var days = (int)Math.Floor(timeUntilMatch.TotalDays);
    timeText = $"Match in {days} day{(days !=1 ? "s" : "")}.";
         }
        }
        else if (timeUntilMatch.TotalMinutes >= -1)
        {
         // Around start time
         timeText = "Match starting now.";
        }
        else
        {
         // Started in the past - show when it started
         var minutesAgo = (int)Math.Round(-timeUntilMatch.TotalMinutes);
         if (minutesAgo <60)
         {
           timeText = $"Match started {minutesAgo} minute{(minutesAgo !=1 ? "s" : "")} ago.";
         }
         else if (-timeUntilMatch.TotalHours <24)
         {
           var hoursAgo = (int)Math.Round(-timeUntilMatch.TotalHours);
           timeText = $"Match started {hoursAgo} hour{(hoursAgo !=1 ? "s" : "")} ago.";
         }
         else
         {
           var daysAgo = (int)Math.Floor(-timeUntilMatch.TotalDays);
           timeText = $"Match started {daysAgo} day{(daysAgo !=1 ? "s" : "")} ago.";
         }
        }
        
        // Always include scheduled time
        var scheduledLine = $"Scheduled: {scheduledText}";
        
        // Build full message with match details
  if (notification.MatchNumber.HasValue && !string.IsNullOrEmpty(notification.EventCode))
     {
       return $"{timeText}\n{scheduledLine}\n\n{notification.EventCode} - Match #{notification.MatchNumber}";
     }
        
        if (notification.MatchNumber.HasValue)
        {
        return $"{timeText}\n{scheduledLine}\n\nMatch #{notification.MatchNumber}";
        }
        
        return $"{timeText}\n{scheduledLine}";
    }

    private string FormatNotificationType(string notificationType)
    {
  return notificationType switch
        {
 "match_reminder" => "Match Reminder",
  "event_update" => "Event Update",
    "schedule_change" => "Schedule Change",
    _ => notificationType
        };
    }

    private bool WasNotificationSent(int notificationId)
    {
        return _trackingData.SentNotifications.Any(r => r.NotificationId == notificationId);
    }

    private void RecordSentNotification(ScheduledNotification notification, bool isCatchUp)
    {
        var record = new SentNotificationRecord
   {
         NotificationId = notification.Id,
         SentAt = DateTime.UtcNow,
 ScheduledFor = notification.ScheduledFor,
  NotificationType = notification.NotificationType,
            MatchNumber = notification.MatchNumber,
            EventCode = notification.EventCode,
     WasMissed = isCatchUp
        };
   
        _trackingData.SentNotifications.Add(record);
      
        System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Recorded sent notification {notification.Id} (missed: {isCatchUp})");
    }

    private void CleanupOldRecords()
    {
      var cutoffDate = DateTime.UtcNow.AddDays(-CLEANUP_RETENTION_DAYS);
   var originalCount = _trackingData.SentNotifications.Count;
        
        _trackingData.SentNotifications.RemoveAll(r => r.SentAt < cutoffDate);
      
 var removedCount = originalCount - _trackingData.SentNotifications.Count;
        if (removedCount > 0)
        {
      System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Cleaned up {removedCount} old notification records (older than {CLEANUP_RETENTION_DAYS} days)");
        }
    }

    private async Task LoadTrackingDataAsync()
 {
     try
        {
    if (!File.Exists(_trackingFilePath))
            {
   System.Diagnostics.Debug.WriteLine("[BackgroundNotifications] No tracking file found, starting fresh");
        _trackingData = new NotificationTrackingData
            {
   LastPollTime = DateTime.UtcNow,
      LastCleanupTime = DateTime.UtcNow
       };
         return;
  }
     
     var json = await File.ReadAllTextAsync(_trackingFilePath);
          var data = JsonSerializer.Deserialize<NotificationTrackingData>(json);
     
  if (data != null)
            {
    _trackingData = data;
         System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Loaded tracking data: {_trackingData.SentNotifications.Count} sent records, last poll: {_trackingData.LastPollTime:yyyy-MM-dd HH:mm:ss}");
       }
        }
        catch (Exception ex)
        {
     System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Error loading tracking data: {ex.Message}");
    _trackingData = new NotificationTrackingData
            {
                LastPollTime = DateTime.UtcNow,
   LastCleanupTime = DateTime.UtcNow
         };
    }
    }

    private async Task SaveTrackingDataAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_trackingData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_trackingFilePath, json);
            
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Saved tracking data: {_trackingData.SentNotifications.Count} sent records");
        }
      catch (Exception ex)
        {
     System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Error saving tracking data: {ex.Message}");
   }
 }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
  
        try
        {
            _pollLock?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Dispose error: {ex.Message}");
        }
    }

    // Helper: extract numeric team IDs from arbitrary alliance string (handles formats like "red(5454,5568)", "[5454,5568]", "5454,5568", etc.)
    private static string ExtractTeamList(string? raw)
    {
    if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
    try
    {
      var matches = System.Text.RegularExpressions.Regex.Matches(raw, "\\d+");
      var nums = matches.Select(m => m.Value).ToArray();
      return nums.Length >0 ? string.Join(", ", nums) : string.Empty;
    }
    catch
    {
      // Fallback: remove non-digits and non-comma characters
      var cleaned = new string(raw.Where(c => char.IsDigit(c) || c == ',').ToArray());
      var parts = cleaned.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
      return parts.Length >0 ? string.Join(", ", parts) : string.Empty;
    }
    }
}
