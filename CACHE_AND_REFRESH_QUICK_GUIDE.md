# Quick Reference: Teams & Matches Caching + Auto-Refresh

## What's New?

### ? Teams Now Cached
- Teams are saved locally
- Works offline after first load
- Refreshes every 1 minute

### ? Matches Auto-Load
- Matches load automatically when you open the scouting page
- No need to click "Load Matches" anymore
- Still works if you select a team

### ? Background Refresh
- Teams and matches update every 60 seconds
- Happens in background (you won't notice)
- Keeps data fresh automatically

---

## How It Works

### When You Open Scouting Page:
```
1. Page opens
2. Teams are already there (from cache or server)
3. Matches automatically load
4. Form is ready to use! ?
```

### Every 60 Seconds:
```
1. App refreshes teams from server (or uses cache if offline)
2. App refreshes matches from server (or uses cache if offline)
3. Your data stays fresh automatically
```

### When Offline:
```
1. Teams load from cache ??
2. Matches load from cache ??
3. You can scout using cached data
4. Generate QR codes works
5. Submit doesn't work (need internet)
```

---

## Visual Indicators

### Online Mode:
- No special indicators
- Data loads normally
- "Loaded X teams" briefly appears

### Offline Mode (Using Cache):
- "?? Loaded X cached teams (offline)"
- "?? Loaded X cached matches (offline mode)"
- Can still scout and generate QR codes
- Cannot submit directly

---

## User Experience

### Before This Change:
```
1. Open Scouting page
2. Select team
3. Click "Load Matches" ? Manual step
4. Wait for matches
5. Select match
6. Scout
```

### After This Change:
```
1. Open Scouting page
2. Matches already loading! ? Automatic
3. Select team (refreshes matches)
4. Select match
5. Scout
```

---

## Cache Details

### What's Cached:
- ? Teams list
- ? Matches list
- ? Game config
- ? Scout name (username)

### What's NOT Cached:
- ? Scouting submissions (need server)
- ? Events list (loaded on demand)

### Cache Storage:
- **Android**: Encrypted storage
- **iOS**: Keychain
- **Windows**: Secure storage
- **Persists**: Until app uninstalled or overwritten

---

## Refresh Timing

| Action | When | Silent? |
|--------|------|---------|
| Initial Load | App startup | No (shows status) |
| Page Appear Load | Open scouting page | No (shows status) |
| Background Refresh | Every 60 seconds | Yes (silent) |
| Team Selection | When you select team | No (shows status) |
| Manual Refresh | Click refresh button | No (shows status) |

---

## Debug Output

You'll see these in debug logs:

### On Page Open:
```
=== INITIAL LOAD ON PAGE APPEAR ===
=== AUTO-LOADING MATCHES ===
? Auto-loaded 45 matches from server
```

### Every 60 Seconds:
```
=== BACKGROUND REFRESH ===
? Loaded 150 teams from server
? Cached 150 teams
? Background refresh completed at 14:23:15
```

### When Offline:
```
? Loaded 150 teams from cache
Cached teams age: 2.3 hours
? Auto-loaded 45 matches from cache (offline)
Cached matches age: 2.3 hours
```

---

## Frequently Asked Questions

### Q: Will matches load when I open the page?
**A:** Yes! They load automatically if you have teams and config loaded.

### Q: Do I still need to click "Load Matches"?
**A:** No, but you can if you want to manually refresh.

### Q: What if I'm offline?
**A:** The app will use cached teams and matches from your last online session.

### Q: How often does it refresh?
**A:** Every 60 seconds in the background.

### Q: Will it slow down my device?
**A:** No, the background refresh is lightweight and silent.

### Q: Can I disable auto-refresh?
**A:** Yes, but you'd need to modify the code (see full documentation).

### Q: Does it use a lot of data?
**A:** No, it only downloads teams/matches when they change.

### Q: What happens when I select a team?
**A:** Matches are refreshed for that specific team (as before).

---

## Benefits

### For Scouts:
- ? Faster - Form ready instantly
- ?? Works offline - Scout anywhere
- ?? Always fresh - Auto-updates
- ?? Mobile-friendly - Handles poor connectivity

### For Scouting Leads:
- ?? More scouts - Easier to use
- ?? Reliable - Works in stadiums
- ?? Accurate - Fresh team/match data
- ?? Productive - Less time waiting

---

## Summary

**The scouting form is now faster, smarter, and works offline!**

- Teams & matches are cached locally
- Auto-loads when you open the page
- Refreshes every minute in background
- Falls back to cache when offline

You can now scout even with poor or no internet connection! ??
