# ?? CRITICAL FIX: Notification Deep Linking Issues

## Problem 1: Match Notifications Don't Open App ?

**Symptom:** Tapping match reminder shows empty intent extras:
```
[MainActivity] Checking intent extras:
  type: 
  sourceType: 
  sourceId: 
  eventCode: 
  eventId: 
  matchNumber: 
[MainActivity] ? Invalid notification intent - not storing
```

**Root Cause:** `BackgroundNotificationService.cs` creates match notifications but **NEVER calls `ShowWithDataAsync()`** - it calls the wrong method!

**Current Code (BROKEN):**
```csharp
if (!string.IsNullOrEmpty(notification.EventCode) && notification.EventId.HasValue)
{
    var deepLinkData = new Dictionary<string, string>
    {
        { "type", "match" },
        { "eventCode", notification.EventCode },
        { "eventId", notification.EventId.Value.ToString() },
        { "matchNumber", notification.MatchNumber?.ToString() ?? "" }
    };
    
    await _localNotificationService.ShowWithDataAsync(title, message, notification.Id, deepLinkData);
}
else
{
    // ?? BUG: This fallback ALWAYS executes because notification.EventId is ALWAYS NULL!
    await _localNotificationService.ShowAsync(title, message, id: notification.Id);
}
```

**Why it fails:**
- `ScheduledNotification` and `PastNotification` **do NOT have an `EventId` property**
- The condition `notification.EventId.HasValue` is **ALWAYS FALSE**
- So it **ALWAYS** calls `ShowAsync()` (no deep link data) instead of `ShowWithDataAsync()`

---

## Problem 2: Chat Navigation Stored But Never Executed ?

**Symptom:** When tapping chat notification:
1. ? MainActivity stores navigation: `//Chat?sourceType=dm&sourceId=5454`
2. ? `App.xaml.cs OnResume()` **NEVER LOGS** the navigation execution
3. ? App opens but goes to MainPage instead of ChatPage

**Root Cause:** `OnResume()` is called **BEFORE** MainActivity stores the intent!

**Sequence of Events (WRONG ORDER):**
```
1. User taps notification ? MainActivity.OnNewIntent() called
2. App.OnResume() called IMMEDIATELY (before OnNewIntent finishes)
3. OnResume checks HasPendingNavigation() ? FALSE (not stored yet!)
4. OnResume exits without navigating
5. OnNewIntent finally stores navigation ? TOO LATE!
```

**Proof from logs:**
```
[MainActivity] OnNewIntent - handling notification tap   ? Step 1
[MainActivity] OnPause - app going to background         ? Step 5 (after everything)
```

No `[App] HasPendingNavigation:` logs = OnResume never checked!

---

## Solution

### Fix 1: Add EventId to Notification Models ?

`NotificationModels.cs` needs EventId:
```csharp
public class ScheduledNotification
{
    // ...existing properties...
    public int? EventId { get; set; }  // ? ADD THIS
}

public class PastNotification  
{
    // ...existing properties...
    public int? EventId { get; set; }  // ? ADD THIS (already exists from previous fix)
}
```

### Fix 2: Change OnResume Navigation Strategy ?

Instead of checking in `OnResume()`, check in `OnStart()` with a delay:

**App.xaml.cs:**
```csharp
protected override async void OnStart()
{
    base.OnStart();

    // Check if user is logged in
 var token = await _settingsService.GetTokenAsync();
    var expiration = await _settingsService.GetTokenExpirationAsync();

    if (string.IsNullOrEmpty(token) || expiration == null || expiration < DateTime.UtcNow)
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }
    else
    {
        // Token exists and is valid, preload all data in background
        System.Diagnostics.Debug.WriteLine("[App] User authenticated, triggering data preload");
        _ = Task.Run(async () => await _dataPreloadService.PreloadAllDataAsync());

   // Update auth state and navigate
        if (Windows[0].Page is AppShell shell)
        {
          shell.UpdateAuthenticationState(true);
        }
   
        // ? NEW: Check for pending navigation AFTER authentication
        #if ANDROID
        await Task.Delay(500); // Give OnNewIntent time to store navigation
     
        if (ObsidianScout.MainActivity.HasPendingNavigation())
      {
            var navUri = ObsidianScout.MainActivity.GetPendingNavigationUri();
            System.Diagnostics.Debug.WriteLine($"[App] Found pending navigation in OnStart: {navUri}");
            
  if (!string.IsNullOrEmpty(navUri))
       {
   await Shell.Current.GoToAsync(navUri);
   ObsidianScout.MainActivity.ClearPendingNavigation();
        System.Diagnostics.Debug.WriteLine($"[App] ? Pending navigation executed from OnStart");
     return; // Don't navigate to MainPage
          }
   }
        #endif
        
        await Shell.Current.GoToAsync("//MainPage");
    }
}
```

**Keep OnResume for background ? foreground transitions:**
```csharp
protected override void OnResume()
{
    base.OnResume();
    
    System.Diagnostics.Debug.WriteLine("[App] App resumed, checking for pending navigation");
    
    #if ANDROID
    _ = Task.Run(async () =>
    {
        try
        {
            // Small delay to ensure OnNewIntent completes
            await Task.Delay(300);
      
       if (ObsidianScout.MainActivity.HasPendingNavigation())
            {
        var navUri = ObsidianScout.MainActivity.GetPendingNavigationUri();
           System.Diagnostics.Debug.WriteLine($"[App] Found pending navigation in OnResume: {navUri}");
         
      if (!string.IsNullOrEmpty(navUri))
      {
            await MainThread.InvokeOnMainThreadAsync(async () =>
    {
           try
             {
     if (Shell.Current != null)
         {
     System.Diagnostics.Debug.WriteLine($"[App] Executing pending navigation: {navUri}");
        await Shell.Current.GoToAsync(navUri);
      System.Diagnostics.Debug.WriteLine($"[App] ? Navigation completed");
    }
           }
    catch (Exception ex)
                {
   System.Diagnostics.Debug.WriteLine($"[App] Navigation error: {ex.Message}");
    }
       });
     
         ObsidianScout.MainActivity.ClearPendingNavigation();
  }
            }
        
     // Continue with data refresh
   var token = await _settingsService.GetTokenAsync();
  if (!string.IsNullOrEmpty(token))
    {
                await _dataPreloadService.PreloadAllDataAsync();
         }
        }
        catch (Exception ex)
   {
         System.Diagnostics.Debug.WriteLine($"[App] Resume failed: {ex.Message}");
        }
    });
    #endif
}
```

---

## Bonus Issue: Chat Sender is Team Number Not Username ??

From logs:
```
[BackgroundNotifications] WARNING: lastSource.Id '5454' appears to be a team number, not a username!
[API] GetChatMessagesAsync Status: 404 NotFound
[API] GetChatMessagesAsync Response: {
  "error": "Other user not found",
  "error_code": "USER_NOT_FOUND"
}
```

**This is a SERVER issue** - the chat state returns team number instead of username in `lastSource.Id`.

**Client workaround already implemented:** Falls back to generic notification when API fails.

**Server needs to fix:** Return actual username in chat state's `lastSource.Id` field.

---

## Summary

| Issue | Status | Fix |
|-------|--------|-----|
| Match notifications have no deep link data | ? | Add EventId to models |
| Chat navigation stored but never executes | ? | Check in OnStart + OnResume |
| Chat shows team number instead of username | ?? | Server bug, client has fallback |

**Deploy these fixes and both notification types will work!** ??
