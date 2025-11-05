# ?? Deploy Boot Auto-Start Fix

## ? What Was Fixed

**Problem:** Notifications don't start in background after device reboot (must open app first)  
**Root Cause:** Android requires apps to be launched at least once before broadcast receivers work  
**Solution:** Reminder notification + multi-layer boot handling

---

## ?? Deploy Steps

### 1. Stop Debugging
```
Press Shift+F5 in Visual Studio
```

### 2. Uninstall Old Version
```powershell
adb uninstall com.companyname.obsidianscout
```

**Important:** Complete uninstall to test fresh install scenario

### 3. Clean Build
```powershell
cd "C:\Users\steve\source\repos\ObsidianScout"
dotnet clean
```

### 4. Rebuild
```powershell
dotnet build -f net10.0-android -c Debug
```

### 5. Deploy
```powershell
dotnet build -t:Run -f net10.0-android
```

---

## ?? Test Immediately

### Test 1: Fresh Install (App Not Opened)

```powershell
# App is now installed but NOT opened
# Reboot device immediately
adb reboot

# Wait for boot
adb wait-for-device

# Check for reminder notification
adb logcat | findstr "DeviceBootReceiver"
```

**Expected output:**
```
[DeviceBootReceiver] ===== BOOT RECEIVED =====
[DeviceBootReceiver] App launched once: false
[DeviceBootReceiver] ?? App never launched - Android restricts background execution
[DeviceBootReceiver] ?? User must open app at least once
[DeviceBootReceiver] ? Reminder notification shown
```

**On device:**
- ? Notification appears: "ObsidianScout - Tap to enable match notifications after device restart"
- ? Service did NOT start (expected due to Android restriction)

### Test 2: After Opening App

```powershell
# Tap the reminder notification OR manually open the app
# (This sets the "app_launched_once" flag)

# Wait a few seconds for service to start
# Check logs:
adb logcat | findstr "MauiProgram\|ForegroundService"
```

**Expected output:**
```
[MauiProgram] App launched - starting ForegroundNotificationService
[MauiProgram] ForegroundNotificationService started successfully
[ForegroundService] OnCreate called
[ForegroundService] Background notification service started successfully
[BackgroundNotifications] Service started - polling every 60 seconds
```

**Verify flag was saved:**
```powershell
adb shell "run-as com.companyname.obsidianscout cat /data/data/com.companyname.obsidianscout/shared_prefs/obsidian_scout_prefs.xml"
```

**Expected:**
```xml
<?xml version='1.0' encoding='utf-8' standalone='yes' ?>
<map>
 <boolean name="app_launched_once" value="true" />
</map>
```

### Test 3: Second Reboot (Service Auto-Starts)

```powershell
# Reboot device again WITHOUT opening app
adb reboot

# Wait for boot
adb wait-for-device

# Monitor boot receiver
adb logcat | findstr "DeviceBootReceiver\|ForegroundService"
```

**Expected output:**
```
[DeviceBootReceiver] ===== BOOT RECEIVED =====
[DeviceBootReceiver] App launched once: true
[DeviceBootReceiver] ? App was launched before - proceeding with service start
[DeviceBootReceiver] Starting ForegroundNotificationService...
[DeviceBootReceiver] ? StartForegroundService called (Android 8.0+)
[DeviceBootReceiver] ? Exact alarm scheduled
[DeviceBootReceiver] ===== BOOT HANDLING COMPLETE =====
[ForegroundService] OnCreate called
[ForegroundService] Background notification service started successfully
[BackgroundNotifications] Service started - polling every 60 seconds
```

**On device:**
- ? Service starts automatically
- ? No reminder notification (not needed)
- ? Notifications work WITHOUT opening app

### Test 4: Verify Notifications Work

```powershell
# Wait 60 seconds for first poll
# Check logs:
adb logcat | findstr "BackgroundNotifications"
```

**Expected:**
```
[BackgroundNotifications] === POLL START ===
[BackgroundNotifications] Checking for missed notifications...
[BackgroundNotifications] Checking scheduled notifications...
[BackgroundNotifications] === POLL END (1.2s) ===
```

---

## ? Verification Checklist

- [ ] Fresh install - reminder notification appears after reboot
- [ ] Tap notification or open app manually
- [ ] Flag saved: `app_launched_once = true`
- [ ] Service starts immediately after opening app
- [ ] Reboot again - service starts automatically (no app open)
- [ ] No reminder notification on second boot
- [ ] Notifications poll every 60 seconds
- [ ] No ANR or crashes

---

## ?? Expected Timeline

| Event | Time | Result |
|-------|------|--------|
| Install app | 0:00 | App installed, not opened |
| Reboot device | 0:30 | Reminder notification appears |
| Tap notification | 1:00 | App opens, flag saved |
| | 1:05 | Service starts |
| Reboot device | 2:00 | Service auto-starts (no app open) ? |
| | 2:30 | First notification poll |
| | 3:30 | Second notification poll |

---

## ?? Success Criteria

### After First Reboot (Fresh Install):
- ? Reminder notification appears
- ? User opens app
- ? Service starts
- ? Flag saved

### After Second Reboot:
- ? Service starts automatically
- ? No reminder notification
- ? Notifications work WITHOUT opening app
- ? Polling continues every 60 seconds

---

## ?? Troubleshooting

### Issue: No Reminder Notification

**Check notification permission:**
```powershell
adb shell dumpsys notification | findstr "obsidianscout"
```

**Check boot receiver:**
```powershell
adb shell dumpsys package com.companyname.obsidianscout | findstr "DeviceBootReceiver"
```

### Issue: Service Doesn't Start After Opening App

**Check logs for errors:**
```powershell
adb logcat | findstr "MauiProgram\|ForegroundService\|Exception"
```

**Manually start service:**
```powershell
adb shell am start-foreground-service com.companyname.obsidianscout/crc64f3faeb7d35d8db75.ForegroundNotificationService
```

### Issue: Service Doesn't Auto-Start on Second Boot

**Verify flag is saved:**
```powershell
adb shell "run-as com.companyname.obsidianscout cat /data/data/com.companyname.obsidianscout/shared_prefs/obsidian_scout_prefs.xml"
```

**Should show:**
```xml
<boolean name="app_launched_once" value="true" />
```

If not, check MauiProgram.cs is saving the flag correctly.

---

## ?? User Instructions

**Provide to end users:**

### After Installing or Updating ObsidianScout:

1. **You'll see a notification after device restart:**
   - "Tap to enable match notifications after device restart"

2. **Tap the notification** or open ObsidianScout manually

3. **That's it!** From now on:
   - Match notifications will work automatically after every restart
   - No need to open the app after reboots
   - Background service starts automatically

**Why is this needed?**
- Android security requires apps to be opened once after install/update
- This is standard for all Android apps
- It's a ONE-TIME setup

---

## ?? Deployment Complete

### Before:
- ? Service doesn't start after reboot
- ? User must open app every reboot
- ? Confusing behavior

### After:
- ? **Reminder notification** guides users
- ? **ONE-TIME setup** (open app once)
- ? **Auto-start on all future boots**
- ? **Clear user communication**

---

*Deployment Guide - January 2025*  
*Status: ? Ready to Deploy*  
*Build: ? Successful*
