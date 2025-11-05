# ?? Match Notifications - Quick Reference

## ? What's New

### 1. **Accurate Time Display** ?
Shows **actual time** until match instead of hardcoded "20 minutes"

### 2. **Auto-Start on Boot** ??
Service starts **automatically** when device reboots - no app open required!

---

## ?? Changes Summary

| File | Change | Purpose |
|------|--------|---------|
| `BackgroundNotificationService.cs` | Calculate actual time | Shows "5 minutes" vs "20 minutes" |
| `BootReceiver.cs` | NEW FILE | Starts service on device boot |
| `AndroidManifest.xml` | Add RECEIVE_BOOT_COMPLETED | Allows boot detection |

---

## ?? Time Display Examples

| Time Until Match | Display |
|-----------------|---------|
| < 1 minute | "Match starting now!" |
| 5 minutes | "Match starting in 5 minutes!" |
| 45 minutes | "Match starting in 45 minutes!" |
| 90 minutes | "Match starting in 1h 30m!" |
| 3 hours | "Match starting in 3 hours!" |
| 2 days | "Match in 2 days" |

---

## ?? Quick Test

### Test Auto-Start:
```powershell
# 1. Deploy app
dotnet build -t:Run -f net10.0-android

# 2. Reboot device
adb reboot

# 3. Wait 30 seconds, then check logs
adb wait-for-device
adb logcat | findstr "BootReceiver"
```

**Expected output:**
```
[BootReceiver] Received broadcast: android.intent.action.BOOT_COMPLETED
[BootReceiver] Device booted - starting notification service
[BootReceiver] Notification service started successfully
```

### Test Time Display:
```powershell
# Watch notification messages
adb logcat | findstr "BackgroundNotifications"
```

**Expected:**
```
[BackgroundNotifications] Showing notification: Match Reminder
[BackgroundNotifications]   Message: Match starting in 15 minutes!

         TXPLA2 - Match #5
```

---

## ? Success Indicators

- [ ] Notifications show actual time countdown
- [ ] Service starts automatically after reboot
- [ ] No need to open app after boot
- [ ] Logs show BootReceiver activation

---

## ?? Troubleshooting

### Service Not Starting on Boot

**Check receiver registered:**
```powershell
adb shell dumpsys package com.companyname.obsidianscout | findstr "BootReceiver"
```

**Manually trigger boot:**
```powershell
adb shell am broadcast -a android.intent.action.BOOT_COMPLETED -p com.companyname.obsidianscout
```

### Time Not Accurate

**Check server time:**
- Server must send correct `scheduled_for` timestamp
- Service calculates: `scheduledFor - now = timeUntilMatch`

---

## ?? Result

**Before:**
- ? "20 minutes" (always)
- ? Must open app after reboot

**After:**
- ? **Accurate countdown** ("5 min", "2h 30m")
- ? **Auto-starts on boot** - truly background!

---

*Quick Reference - January 2025*  
*Status: ? Ready to Test*
