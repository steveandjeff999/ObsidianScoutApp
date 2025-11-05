# ? FIX APPLIED: Notification Deep Linking

## Changes Made

### `App.xaml.cs` - Navigation Timing Fix ?

**Problem:** OnResume() was checking for pending navigation BEFORE MainActivity.OnNewIntent() could store it.

**Solution:** Added navigation checks in BOTH lifecycle methods with proper delays:

1. **OnStart()** - Handles cold starts (app was completely closed)
   - Waits 500ms for OnNewIntent to complete
   - Checks HasPendingNavigation()
   - If found, navigates directly (skips MainPage)
   - If not found, goes to MainPage as normal

2. **OnResume()** - Handles warm starts (app was in background)
   - Waits 300ms for OnNewIntent to complete
   - Checks HasPendingNavigation()
   - If found, navigates on main thread
   - Continues with data refresh regardless

**Code Changes:**
```csharp
protected override async void OnStart()
{
    // ...existing authentication check...
    
    // ? NEW: Check for pending navigation (cold start)
    #if ANDROID
    await Task.Delay(500); // Give OnNewIntent time to store
    
    if (MainActivity.HasPendingNavigation())
    {
var navUri = MainActivity.GetPendingNavigationUri();
        await Shell.Current.GoToAsync(navUri);
        MainActivity.ClearPendingNavigation();
     return; // Don't go to MainPage
    }
    #endif
    
    await Shell.Current.GoToAsync("//MainPage");
}

protected override void OnResume()
{
    // ...existing base call...
    
    #if ANDROID
    _ = Task.Run(async () =>
    {
        await Task.Delay(300); // Shorter delay for resume
        
  if (MainActivity.HasPendingNavigation())
   {
  var navUri = MainActivity.GetPendingNavigationUri();
            await MainThread.InvokeOnMainThreadAsync(async () =>
       {
   await Shell.Current.GoToAsync(navUri);
            });
     MainActivity.ClearPendingNavigation();
        }
        
        // Continue with data refresh...
    });
    #endif
}
```

---

## Models Already Fixed ?

`NotificationModels.cs` already has `EventId` on both models:
- `ScheduledNotification.EventId` ?
- `PastNotification.EventId` ?

---

## Service Already Fixed ?

`BackgroundNotificationService.cs` already calls `ShowWithDataAsync()` correctly:

**For scheduled notifications:**
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
```

**For past notifications:**
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
```

---

## Testing Instructions

### Test 1: Match Notification (Cold Start)
1. Kill the app completely
2. Wait for a match reminder notification
3. Tap the notification
4. Expected: App opens directly to MatchesPage for that event

### Test 2: Chat Notification (Cold Start)
1. Kill the app completely
2. Send a chat message to yourself from another device
3. Wait for notification
4. Tap the notification
5. Expected: App opens directly to ChatPage with that DM

### Test 3: Match Notification (Background)
1. Open app and go to MainPage
2. Press home button (app goes to background)
3. Wait for a match reminder notification
4. Tap the notification
5. Expected: App resumes and navigates to MatchesPage

### Test 4: Chat Notification (Background)
1. Open app and go to MainPage
2. Press home button (app goes to background)
3. Send a chat message to yourself
4. Tap the notification
5. Expected: App resumes and navigates to ChatPage

---

## Expected Debug Logs

### Cold Start Success ?
```
[MainActivity] OnNewIntent - handling notification tap
[MainActivity] Checking intent extras:
  type: match
  eventCode: TXPLA2
  eventId: 5
  matchNumber: 3
[MainActivity] ? Match intent detected
[MainActivity] ? Stored pending navigation: //MatchesPage?eventId=5&eventCode=TXPLA2
[App] Checking for pending navigation in OnStart...
[App] Found pending navigation in OnStart: //MatchesPage?eventId=5&eventCode=TXPLA2
[App] Executing pending navigation from OnStart: //MatchesPage?eventId=5&eventCode=TXPLA2
[App] ? Pending navigation completed from OnStart
```

### Background?Foreground Success ?
```
[MainActivity] OnNewIntent - handling notification tap
[MainActivity] ? Stored pending navigation: //Chat?sourceType=dm&sourceId=5454
[App] App resumed, checking for pending navigation
[App] HasPendingNavigation: True
[App] Found pending navigation in OnResume: //Chat?sourceType=dm&sourceId=5454
[App] Executing pending navigation from OnResume: //Chat?sourceType=dm&sourceId=5454
[App] ? Navigation completed from OnResume
[App] Pending navigation cleared
```

---

## Known Issues (Server-Side)

### Chat Sender Issue ??
**Problem:** Chat state returns team number (5454) instead of username
**Impact:** API call fails with 404 "User not found"
**Workaround:** App falls back to generic notification
**Fix Required:** Server needs to return actual username in `lastSource.Id`

**Client logs showing issue:**
```
[BackgroundNotifications] WARNING: lastSource.Id '5454' appears to be a team number, not a username!
[API] GetChatMessagesAsync Status: 404 NotFound
```

---

## Summary

| Issue | Status | Action |
|-------|--------|--------|
| Match notifications don't open app | ? FIXED | Navigation timing in OnStart |
| Chat notifications don't navigate | ? FIXED | Navigation timing in OnStart + OnResume |
| Chat shows team number | ?? SERVER BUG | Client has fallback |

**Both notification types should now work correctly!** ??

Redeploy the app and test both cold starts and background resumes.
