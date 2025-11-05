# Chat Notification Navigation Fix

## Problem
When tapping chat message notifications, the app was navigating to the home page instead of opening the chat page.

### Error Message
```
[App] Navigation error in OnResume: unable to figure out route for: //Chat?sourceType=dm&sourceId=5454
```

## Root Cause
The notification system was creating navigation URIs with **absolute routing** (`//Chat`) when it should use **relative routing** (`Chat`) because "Chat" is registered as a relative route in `AppShell.xaml.cs`.

### Why This Matters
- **Absolute routes** (`//PageName`) navigate to top-level Shell pages defined directly in AppShell.xaml
- **Relative routes** (`PageName`) navigate to registered routes using `Routing.RegisterRoute()`
- Chat is registered as: `Routing.RegisterRoute("Chat", typeof(ChatPage));`

## Changes Made

### 1. MainActivity.cs - Fixed Route Generation
**File**: `ObsidianScout/Platforms/Android/MainActivity.cs`

Changed from:
```csharp
// WRONG: Absolute route
navUri = $"//Chat?sourceType={sourceType}&sourceId={sourceId}";
```

To:
```csharp
// CORRECT: Relative route
navUri = $"Chat?sourceType={sourceType}&sourceId={System.Uri.EscapeDataString(sourceId)}";
```

### 2. App.xaml.cs - Added URI Cleaning
**File**: `ObsidianScout/App.xaml.cs`

Added fallback logic to handle both absolute and relative routes:

```csharp
private async Task ExecutePendingNavigationAsync(string navUri)
{
    // Fix: Remove leading "//" for relative navigation
    string cleanUri = navUri;
    if (cleanUri.StartsWith("//"))
    {
        cleanUri = cleanUri.Substring(2); // Remove leading "//"
        System.Diagnostics.Debug.WriteLine($"[App] Cleaned navigation URI: {cleanUri}");
    }
    
    // Navigate to MainPage first to ensure proper initialization
    if (Shell.Current?.CurrentState?.Location?.OriginalString != "//MainPage")
    {
        await Shell.Current.GoToAsync("//MainPage");
        await Task.Delay(500);
    }

    // Now navigate to the target page
    await Shell.Current.GoToAsync(cleanUri);
}
```

## How It Works Now

### Chat Notification Flow
1. **User receives chat notification** from dm/5454
2. **User taps notification**
3. **LocalNotificationService** creates PendingIntent with extras:
   ```
   type: chat
   sourceType: dm
   sourceId: 5454
   ```
4. **MainActivity.OnNewIntent** reads extras and creates URI:
   ```
   Chat?sourceType=dm&sourceId=5454
   ```
5. **MainActivity** stores the pending navigation
6. **App.OnResume** retrieves and executes navigation:
   - Navigates to MainPage (if not already there)
   - Waits 500ms for initialization
   - Navigates to `Chat?sourceType=dm&sourceId=5454`
7. **ChatPage** receives parameters and opens the correct conversation

## Testing
1. ? Build successful
2. ? No compilation errors
3. ? Async/await properly handled
4. ? Conditional compilation for Android-specific code

## Expected Behavior
When you tap a chat notification:
1. App opens/resumes
2. Navigates to MainPage briefly (if needed)
3. Immediately navigates to ChatPage
4. Opens the specific conversation (dm with user 5454)

## Additional Notes
- Match notifications still use absolute routing (`//MatchesPage`) which is correct for them
- The URI cleaning fallback ensures backward compatibility if absolute routes are used accidentally
- 500ms delay allows Shell and pages to fully initialize before navigation
- Navigation errors are caught and logged for debugging

## Related Files
- `ObsidianScout/Platforms/Android/MainActivity.cs` - Intent handling and URI generation
- `ObsidianScout/App.xaml.cs` - Navigation execution
- `ObsidianScout/Platforms/Android/LocalNotificationService.cs` - Notification creation
- `ObsidianScout/AppShell.xaml.cs` - Route registration
