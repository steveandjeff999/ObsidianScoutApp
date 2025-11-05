# Offline Mode Issue - Diagnostic Guide

## Problem
App is stuck in offline mode even though you're online with a server connection.

## Root Causes

### 1. **Offline Mode Setting Enabled**
- Check if offline mode toggle is ON in Settings
- Location: Settings Page ? Force Offline Mode switch
- This forces local Microcharts even when online

### 2. **Connectivity Service Not Detecting Connection**
- The `ConnectivityService` polls every 30 seconds
- May not have updated yet after network connection
- Initial state might be cached as "disconnected"

### 3. **SecureStorage Cached Value**
- Offline mode setting stored in `SecureStorage`
- Key: `"offline_mode"`
- May be stuck as `"true"` from previous session

## Diagnostic Steps

### Step 1: Check Debug Output
With the new debug logging, generate graphs and look for:

```
=== CONNECTIVITY CHECK ===
Offline Mode Setting: false  <-- Should be false
Connectivity Service IsConnected: true  <-- Should be true
MAUI Connectivity.NetworkAccess: Internet  <-- Should be Internet
Should try server: true  <-- Should be true
? Attempting to request server image
```

**If you see:**
```
? Skipping server request:
  - Offline mode is ENABLED
```
? Go to Settings and disable offline mode

**If you see:**
```
? Skipping server request:
  - Connectivity service says NOT connected
```
? Connectivity service issue (see fixes below)

### Step 2: Check Settings Page
1. Navigate to Settings
2. Look for "Force Offline Mode" toggle
3. **Make sure it's OFF (gray/disabled)**
4. If it's ON, toggle it OFF

### Step 3: Check MAUI Connectivity Directly
The debug log shows `MAUI Connectivity.NetworkAccess` - should be `Internet`.

If it shows:
- `None` - No network at all
- `Local` - Connected to WiFi but no internet
- `ConstrainedInternet` - Limited connectivity
- `Unknown` - Can't determine

### Step 4: Force Connectivity Refresh
Currently the app polls every 30 seconds. You can:
1. Wait 30 seconds after connecting to network
2. Restart the app to force re-check
3. Toggle WiFi off/on to trigger event

## Quick Fixes

### Fix 1: Clear Offline Mode Setting Manually
Add this temporary code to `App.xaml.cs` `OnStart()`:

```csharp
protected override async void OnStart()
{
    // Force clear offline mode
    await SecureStorage.SetAsync("offline_mode", "false");
    System.Diagnostics.Debug.WriteLine("? Forced offline mode to FALSE");
}
```

### Fix 2: Add Force Refresh Button
Add a button to GraphsPage that manually refreshes connectivity:

```xaml
<Button Text="?? Force Refresh Connection"
     Command="{Binding ForceRefreshConnectionCommand}"
        Style="{StaticResource OutlineGlassButton}" />
```

ViewModel:
```csharp
[RelayCommand]
private async Task ForceRefreshConnection()
{
    var isConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    System.Diagnostics.Debug.WriteLine($"Manual connectivity check: {isConnected}");
    StatusMessage = isConnected ? "? Connected to internet" : "? No internet connection";
}
```

### Fix 3: Make Connectivity Check Synchronous
The issue might be the async nature. Update `ConnectivityService`:

```csharp
public bool IsConnected
{
    get
    {
        try
  {
            // Always check live, don't cache
 return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
        catch
        {
            return false;
        }
    }
}
```

This removes the cached `_isConnected` field and checks live every time.

## Permanent Solution

I'll implement these fixes:

1. **Add debug logging** (? Done) to see exactly what's happening
2. **Add force refresh button** to manually check connectivity
3. **Make connectivity check live** instead of cached
4. **Add visual indicator** showing online/offline status
5. **Auto-detect** and show warning if offline mode is preventing server use

## Testing After Fixes

1. **Restart the app** completely (not just rebuild)
2. **Check debug output** when generating graphs
3. **Verify Settings page** shows offline mode OFF
4. **Try toggling offline mode** ON then OFF
5. **Generate graphs again** - should show server image

## Expected Behavior

**When Online (Correct):**
```
Offline Mode Setting: false
Connectivity Service IsConnected: true
Should try server: true
? Attempting to request server image
Server image request: GraphType=line, Mode=averages, DataView=averages
? Server image loaded successfully for averages view
```

**When Offline Mode Enabled (Correct):**
```
Offline Mode Setting: true
Connectivity Service IsConnected: true
Should try server: false
? Skipping server request:
  - Offline mode is ENABLED
=== GENERATING GRAPHS FROM SCOUTING DATA ===
```

**When No Internet (Correct):**
```
Offline Mode Setting: false
Connectivity Service IsConnected: false
Should try server: false
? Skipping server request:
  - Connectivity service says NOT connected
=== GENERATING GRAPHS FROM SCOUTING DATA ===
```

## What to Check Right Now

1. **Run the app with debugger attached**
2. **Navigate to Graphs page**
3. **Generate graphs**
4. **Check Debug Output window** for the connectivity check lines
5. **Copy/paste the output** and I'll tell you exactly what's wrong

The debug output will show us:
- Is offline mode accidentally enabled?
- Is connectivity service working?
- Is MAUI detecting your internet connection?
- Why server request is being skipped?

## Additional Notes

- The `ConnectivityService` uses a timer that polls every 30 seconds
- Initial state is set in constructor with `CheckNetworkAccess()`
- If app was started while offline, it might not have updated yet
- MAUI's `Connectivity.ConnectivityChanged` event should fire when network changes
- Windows might have different connectivity detection than Android/iOS
