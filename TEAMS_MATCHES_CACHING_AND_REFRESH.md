# Team & Match Caching with Auto-Refresh Implementation

## ? Successfully Implemented

I've added comprehensive caching for teams and matches with automatic refresh every 1 minute, plus automatic match loading on form startup.

---

## What Was Implemented

### 1. **Teams Caching** ??
- Teams are now cached to `SecureStorage`
- Fallback to cache when offline
- Refresh every 60 seconds in background
- Cache timestamp tracking

### 2. **Matches Caching** ??  
- Matches already had caching (now enhanced)
- Falls back to cache when offline
- Auto-refresh every 60 seconds
- Cache timestamp tracking

### 3. **Auto-Load Matches on Page Load** ??
- Matches automatically load when page appears
- Uses `InitialLoadAsync()` method
- Only loads if config and teams are available
- Silent loading (no UI disruption)

### 4. **Periodic Background Refresh** ?
- Refreshes teams & matches every 60 seconds
- Silent refresh (no status messages)
- Continues in background
- Updates `LastRefresh` timestamp

---

## File Changes

| File | Changes Made |
|------|--------------|
| `ScoutingViewModel.cs` | Added teams caching, periodic refresh timer, initial load method |
| `ScoutingPage.xaml.cs` | Added `InitialLoadAsync()` call in `OnAppearing()` |

---

## New Code Components

### Timer for Periodic Refresh

```csharp
private System.Threading.Timer? _refreshTimer;

private void StartPeriodicRefresh()
{
    // Refresh every 60 seconds
    _refreshTimer = new System.Threading.Timer(
        async _ => await RefreshDataInBackground(),
        null,
        TimeSpan.FromSeconds(60),  // Initial delay
        TimeSpan.FromSeconds(60)); // Repeat interval
}
```

### Background Refresh Method

```csharp
private async Task RefreshDataInBackground()
{
    try
    {
        System.Diagnostics.Debug.WriteLine("=== BACKGROUND REFRESH ===");
        
        // Refresh teams silently
        await LoadTeamsAsync(silent: true);
        
        // Refresh matches if we have an event
        if (GameConfig != null && !string.IsNullOrEmpty(GameConfig.CurrentEventCode))
        {
            await AutoLoadMatchesAsync(silent: true);
        }
        
        LastRefresh = DateTime.Now;
        System.Diagnostics.Debug.WriteLine($"? Background refresh completed at {LastRefresh:HH:mm:ss}");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Background refresh failed: {ex.Message}");
    }
}
```

### Initial Load on Page Appear

```csharp
public async Task InitialLoadAsync()
{
    // This method is called when the page appears
    System.Diagnostics.Debug.WriteLine("=== INITIAL LOAD ON PAGE APPEAR ===");
    
    // Load matches automatically if we have config and teams
    if (GameConfig != null && Teams.Any())
    {
        await AutoLoadMatchesAsync(silent: false);
    }
}
```

### Teams Caching

```csharp
private async Task CacheTeamsAsync(List<Team> teams)
{
    try
    {
        var json = JsonSerializer.Serialize(teams);
        await SecureStorage.SetAsync(CACHE_KEY_TEAMS, json);
        await SecureStorage.SetAsync(CACHE_KEY_TEAMS_TIMESTAMP, DateTime.UtcNow.ToString("O"));
        System.Diagnostics.Debug.WriteLine($"? Cached {teams.Count} teams");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Failed to cache teams: {ex.Message}");
    }
}

private async Task<List<Team>?> LoadCachedTeamsAsync()
{
    try
    {
        var json = await SecureStorage.GetAsync(CACHE_KEY_TEAMS);
        if (!string.IsNullOrEmpty(json))
        {
            var teams = JsonSerializer.Deserialize<List<Team>>(json);
            
            // Check cache age
            var timestampStr = await SecureStorage.GetAsync(CACHE_KEY_TEAMS_TIMESTAMP);
            if (!string.IsNullOrEmpty(timestampStr) && DateTime.TryParse(timestampStr, out var timestamp))
            {
                var age = DateTime.UtcNow - timestamp;
                System.Diagnostics.Debug.WriteLine($"Cached teams age: {age.TotalHours:F1} hours");
            }
            
            return teams;
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Failed to load cached teams: {ex.Message}");
    }
    return null;
}
```

### Silent Loading Parameter

Modified methods to accept `silent` parameter:

```csharp
private async Task LoadTeamsAsync(bool silent = false)
{
    if (!silent)
    {
        StatusMessage = "Loading teams...";
    }
    
    // ... load logic ...
    
    if (!silent)
    {
        StatusMessage = $"Loaded {Teams.Count} teams";
    }
}

private async Task AutoLoadMatchesAsync(bool silent = false)
{
    // Silent mode skips status messages
    // Used for background refresh
}
```

---

## Cache Keys

```csharp
private const string CACHE_KEY_TEAMS = "cached_teams";
private const string CACHE_KEY_TEAMS_TIMESTAMP = "cached_teams_timestamp";
private const string CACHE_KEY_MATCHES = "cached_matches";
private const string CACHE_KEY_MATCHES_TIMESTAMP = "cached_matches_timestamp";
```

---

## Data Flow

### On App Start:
```
1. ScoutingViewModel constructor runs
   ?
2. LoadGameConfigAsync() - Load config (or use cache)
   ?
3. LoadTeamsAsync() - Load teams (or use cache)
   ?
4. LoadScoutNameAsync() - Auto-fill scout name
   ?
5. StartPeriodicRefresh() - Start 60s refresh timer
```

### On Page Appear:
```
1. OnAppearing() called
   ?
2. InitialLoadAsync() called
   ?
3. Check if GameConfig exists and Teams loaded
   ?
4. AutoLoadMatchesAsync(silent: false) - Load matches
   ?? Try server first
   ?? Cache results
   ?? Fallback to cache if offline
```

### Every 60 Seconds:
```
1. Timer triggers RefreshDataInBackground()
   ?
2. LoadTeamsAsync(silent: true) - Refresh teams
   ?? Try server first
   ?? Cache results
   ?? Fallback to cache if offline
   ?
3. AutoLoadMatchesAsync(silent: true) - Refresh matches
   ?? Try server first
   ?? Cache results
   ?? Fallback to cache if offline
   ?
4. Update LastRefresh timestamp
```

---

## Offline Support

### Teams Load Flow:
```
Try Server
  ?? SUCCESS ? Cache teams ? Display ? ?
  ?? FAIL ? Try Cache
      ?? Cache Found ? Display ? ?? "offline mode"
      ?? No Cache ? Show error
```

### Matches Load Flow:
```
Try Server
  ?? SUCCESS ? Cache matches ? Display ? ?
  ?? FAIL ? Try Cache
      ?? Cache Found ? Display ? ?? "offline mode"
      ?? No Cache ? Show error
```

---

## Debug Output

### Successful Background Refresh:
```
=== BACKGROUND REFRESH ===
? Loaded 150 teams from server
? Cached 150 teams
? Auto-loaded 45 matches from server
? Cached 45 matches
? Background refresh completed at 14:23:15
=== END AUTO-LOAD MATCHES ===
```

### Offline Background Refresh:
```
=== BACKGROUND REFRESH ===
Error loading teams from server: No such host is known
? Loaded 150 teams from cache
Cached teams age: 0.5 hours
=== AUTO-LOADING MATCHES ===
Server error during auto-load: No such host is known
? Auto-loaded 45 matches from cache (offline)
Cached matches age: 0.5 hours
? Background refresh completed at 14:24:15
=== END AUTO-LOAD MATCHES ===
```

### Initial Load on Page Appear:
```
=== INITIAL LOAD ON PAGE APPEAR ===
=== AUTO-LOADING MATCHES ===
Looking for event: 2024moks
? Auto-loaded 45 matches from server
? Cached 45 matches
=== END AUTO-LOAD MATCHES ===
```

---

## Benefits

### For Users:
- ? **Faster**: Matches load automatically on page open
- ?? **Works Offline**: Teams and matches cached locally
- ?? **Always Fresh**: Data updates every minute
- ?? **Seamless**: No manual refresh needed

### For Scouts:
- ?? **Ready to Scout**: Form is ready when you open it
- ?? **Reliable**: Works even with poor connectivity
- ?? **Fast Response**: Local cache means instant load
- ? **Auto-Updates**: New teams/matches appear automatically

### For Developers:
- ?? **Easy Debugging**: Comprehensive logging
- ??? **Robust**: Graceful fallbacks everywhere
- ?? **Maintainable**: Clear separation of concerns
- ?? **Observable**: LastRefresh timestamp for monitoring

---

## Timing Details

| Action | Timing | Notes |
|--------|--------|-------|
| Initial Refresh Timer Start | 60 seconds | First refresh after app start |
| Refresh Interval | 60 seconds | Continuous updates |
| Cache Check | Immediate | No delay when offline |
| Page Appear Load | Immediate | Matches load on navigation |

---

## Cache Storage

### Platform-Specific Locations:
- **Android**: Encrypted SharedPreferences
- **iOS**: Keychain
- **Windows**: Data Protection API
- **macOS**: Keychain

### Cache Format:
- **Data**: JSON serialized
- **Timestamp**: ISO 8601 format (DateTime.UtcNow.ToString("O"))
- **Persistence**: Indefinite (until overwritten or app uninstalled)

---

## User Experience Scenarios

### Scenario 1: Online User
```
1. Open app ? Teams load from server
2. Navigate to Scouting ? Matches auto-load from server
3. Start scouting
4. After 1 minute ? Teams & matches refresh in background
5. Continue scouting with fresh data
```

### Scenario 2: Offline User (Previously Used App)
```
1. Open app (no internet) ? Teams load from cache ??
2. Navigate to Scouting ? Matches auto-load from cache ??
3. Start scouting
4. After 1 minute ? Background refresh attempts, falls back to cache
5. Can scout using cached data, generate QR codes
```

### Scenario 3: Mixed Connectivity
```
1. Open app (online) ? Teams & matches load from server ?
2. Internet drops
3. After 1 minute ? Background refresh fails, uses cache
4. Can continue scouting
5. Internet returns
6. After 1 minute ? Background refresh succeeds, updates cache ?
```

---

## Configuration Options

### Change Refresh Interval

To change from 60 seconds to another interval:

```csharp
// In StartPeriodicRefresh() method
_refreshTimer = new System.Threading.Timer(
    async _ => await RefreshDataInBackground(),
    null,
    TimeSpan.FromSeconds(120),  // 2 minutes initial delay
    TimeSpan.FromSeconds(120)); // 2 minutes repeat interval
```

### Disable Auto-Load

To disable automatic match loading on page appear:

```csharp
// In ScoutingPage.xaml.cs OnAppearing()
protected override void OnAppearing()
{
    base.OnAppearing();
    
    // Comment out this line:
    // _ = _viewModel.InitialLoadAsync();
    
    if (_viewModel.GameConfig != null && _mainScrollView == null)
    {
        BuildDynamicForm();
    }
}
```

### Disable Background Refresh

To disable periodic background refresh:

```csharp
// In ScoutingViewModel constructor
public ScoutingViewModel(...)
{
    // ...existing code...
    
    // Comment out this line:
    // StartPeriodicRefresh();
}
```

---

## Known Limitations

1. **No Cache Expiration**: Cache persists indefinitely (until overwritten)
2. **Timer Runs Continuously**: Even when app in background
3. **No Offline Queue**: Submissions still require network
4. **Memory Usage**: Cached data stays in memory after load

### Future Enhancements:
- Add cache expiration (e.g., 24 hours)
- Pause timer when app in background
- Implement offline submission queue
- Add cache size limits
- Background sync using WorkManager/BackgroundTasks

---

## Troubleshooting

### Matches Not Auto-Loading

**Check:**
1. Debug Output shows "INITIAL LOAD ON PAGE APPEAR"?
2. GameConfig is loaded?
3. Teams collection has items?
4. Event code is set in GameConfig?

**Solutions:**
- Ensure you're logged in
- Check game config has CurrentEventCode
- Verify teams loaded successfully
- Look at Debug Output for error messages

### Background Refresh Not Working

**Check:**
1. Timer started? (Look for "BACKGROUND REFRESH" in debug output)
2. App in foreground or background?
3. Any exceptions in refresh method?

**Solutions:**
- Restart app to restart timer
- Check network connectivity
- Look for exceptions in Debug Output

### Cache Not Working Offline

**Check:**
1. Was app used online first? (Need initial data to cache)
2. Cache files exist? (Check SecureStorage)
3. JSON deserialization errors?

**Solutions:**
- Use app online first to populate cache
- Clear app data and restart if corrupted
- Check Debug Output for deserialization errors

---

## Summary

? **Teams now cached** - Works offline
? **Matches now cached** - Works offline  
? **Auto-refresh every 60s** - Always fresh data
? **Auto-load on page appear** - Instant readiness
? **Graceful offline fallback** - Uses cache when needed
? **Silent background updates** - No UI disruption
? **Comprehensive logging** - Easy to debug

The scouting form now provides a **seamless offline-first experience** with automatic data loading and continuous background updates! ??
