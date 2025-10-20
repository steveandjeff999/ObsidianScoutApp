# Quick Reference: Auto-Load & Caching

## What Changed?

### 1. Matches Now Auto-Load ?
**When you select a team, matches load automatically in the background**
- No more clicking "Load Matches" button
- Happens silently (you won't even notice)
- Uses cached matches if offline

### 2. App Works Offline ??
**Config and matches are cached for offline use**
- First run: Downloads and caches
- Subsequent runs: Works even without internet
- Shows "?? offline mode" when using cache

---

## For Users

### Normal Workflow (Online):
1. Open app
2. Select team ? **Matches load automatically**
3. Select match
4. Fill form
5. Submit

### Offline Workflow:
1. Open app (sees cached config)
2. Select team ? **Matches load from cache**
3. Select match
4. Fill form
5. Generate QR code *(can't submit to server, but can scan QR later)*

---

## For Developers

### Cache Locations:
- `SecureStorage["cached_game_config"]` - Game configuration JSON
- `SecureStorage["cached_game_config_timestamp"]` - Last updated time
- `SecureStorage["cached_matches"]` - Matches JSON
- `SecureStorage["cached_matches_timestamp"]` - Last updated time

### Debug Commands:
```csharp
// Force reload from server (bypasses cache)
await LoadGameConfigAsync();

// Check if using cached data
var isOffline = IsOfflineMode; // true = using cache

// Clear cache (for testing)
await SecureStorage.SetAsync(CACHE_KEY_GAME_CONFIG, string.Empty);
await SecureStorage.SetAsync(CACHE_KEY_MATCHES, string.Empty);
```

### Watch Debug Output For:
```
? Game config loaded from server and cached
? Auto-loaded 45 matches from server
?? Using cached game config (offline mode)
? Auto-loaded 45 matches from cache (offline)
Cached config age: 2.3 hours
```

---

## Troubleshooting

### Matches Don't Load
**Check:**
1. Is `GameConfig` loaded? (Check `GameConfig != null`)
2. Does config have `CurrentEventCode`? (Check `GameConfig.CurrentEventCode`)
3. Look at Debug Output for errors

### Always Shows "Offline Mode"
**Possible Causes:**
- Server is actually unreachable
- Network error on your device
- Server URL is incorrect (check Settings)

**To Fix:**
- Verify server URL in Settings
- Check network connection
- Try manual refresh (pull to refresh)

### Cache Won't Update
**Solution:**
- Force close and reopen app
- Or call `LoadGameConfigAsync()` + `LoadMatchesAsync()` manually
- Cache updates whenever server data is successfully loaded

---

## Testing

### Test Offline Mode:
1. Use app online (loads and caches data)
2. Turn off WiFi/data
3. Close and reopen app
4. Should show "?? Using cached game config"
5. Matches should still load
6. Can generate QR codes

### Test Auto-Load:
1. Open app
2. Watch Debug Output
3. Select a team
4. Should see "=== AUTO-LOADING MATCHES ==="
5. Matches dropdown should populate

---

## Configuration

### Adjust Cache Behavior:
Currently no user-facing settings. To modify:

```csharp
// In ScoutingViewModel.cs

// Example: Clear cache on app start (for testing)
public ScoutingViewModel(...)
{
    // Clear cache for testing
    _ = SecureStorage.SetAsync(CACHE_KEY_GAME_CONFIG, string.Empty);
    _ = SecureStorage.SetAsync(CACHE_KEY_MATCHES, string.Empty);
    
    // Normal initialization
    LoadGameConfigAsync();
    LoadTeamsAsync();
}
```

---

## Known Limitations

1. **Cache Never Expires**: Cache persists until overwritten (future: add expiration)
2. **No Offline Submissions**: Must be online to submit data (use QR codes instead)
3. **Teams Not Cached**: Teams still require network (future enhancement)
4. **No Background Sync**: Doesn't auto-update cache in background

---

## Future Ideas

- Cache expiration (auto-refresh after X hours)
- Cache teams for full offline operation
- Offline submission queue (submit when back online)
- Background sync when app in background
- UI indicator showing cache age
- Manual cache clear button in settings

---

## Summary

**Auto-load** = Matches load automatically when you select a team

**Offline caching** = App works without internet after first use

**Result** = Faster, more reliable scouting experience ??
