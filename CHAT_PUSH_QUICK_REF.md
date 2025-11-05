# ?? Chat Notifications + Push-Only - Quick Reference

## ?? Deploy

```powershell
# Stop app if running
# Then:
dotnet clean
dotnet build -f net10.0-android
dotnet build -t:Run -f net10.0-android
```

---

## ? What's Fixed

1. **Chat notifications now work** ??
   - Checks `/api/mobile/chat/state`
   - Shows notifications for unread messages
   - Deduplicates automatically

2. **Match notifications: Push-only** ??
   - Only sends if `push: true` in subscription
   - Skips when push disabled
   - Logs skip reason

---

## ?? Notification Types

| Type | ID Range | Trigger | Example |
|------|----------|---------|---------|
| Match | 1-1999 | Scheduled + push enabled | "Match in 5 min" |
| Chat | 9000+ | Unread messages | "2 New Messages" |

---

## ?? Quick Tests

### Test Chat
```powershell
# Send yourself a message
# Wait 60-120s
# Should see notification

adb logcat | findstr "unread chat"
```

### Test Push-Only
```powershell
# Check if any skipped
adb logcat | findstr "push not enabled"
```

---

## ?? Enable Push (if skipped)

1. Log into web UI
2. Notifications settings
3. Find subscription
4. Enable "Push Notifications" toggle
5. Save

---

## ? Success Indicators

- [ ] Chat notifications appear
- [ ] Match notifications appear (when push enabled)
- [ ] Logs show skip reason when push disabled
- [ ] Sound & vibration work for both

---

**Build:** ? Successful  
**Ready:** Deploy now! ??
