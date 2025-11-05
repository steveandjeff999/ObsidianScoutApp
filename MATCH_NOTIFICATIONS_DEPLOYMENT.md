# ?? Deploy Match Notification Improvements

## ? What's Being Deployed

1. **Accurate time display** - Shows actual time until match
2. **Auto-start on boot** - Service starts automatically when device reboots

---

## ?? Deployment Steps

### 1. Stop Debugging
```
Press Shift+F5 in Visual Studio
```

### 2. Uninstall Old App
```powershell
adb uninstall com.companyname.obsidianscout
```

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

### Test 1: Time Display

**Create a test notification:**
1. Server: Schedule notification for 15 minutes from now
2. Wait for notification to appear
3. **Expected:** Shows "Match starting in 15 minutes!"

**Verify logs:**
```powershell
adb logcat | findstr "BackgroundNotifications"
```

### Test 2: Auto-Start on Boot

**Reboot device:**
```powershell
adb reboot
```

**Wait 30 seconds, then check:**
```powershell
adb wait-for-device
adb logcat | findstr "BootReceiver"
```

**Expected output:**
```
[BootReceiver] Received broadcast: android.intent.action.BOOT_COMPLETED
[BootReceiver] Device booted - starting notification service
[BootReceiver] Notification service started successfully
```

### Test 3: No App Open Required

**After reboot:**
1. **DO NOT** open the app
2. Subscribe to match notification (server-side)
3. Wait for scheduled time
4. **Expected:** Notification appears without opening app ?

---

## ? Verification Checklist

- [ ] Build successful
- [ ] App deployed to device
- [ ] Notifications show accurate time ("5 min" not "20 min")
- [ ] Service starts on device boot
- [ ] BootReceiver logs appear
- [ ] Notifications work without opening app
- [ ] Battery optimization exemption requested

---

## ?? Debug Commands

### Check BootReceiver Registered
```powershell
adb shell dumpsys package com.companyname.obsidianscout | findstr "BootReceiver"
```

### Check Service Running
```powershell
adb shell dumpsys activity services | findstr "ForegroundNotificationService"
```

### Test Boot Manually
```powershell
adb shell am broadcast -a android.intent.action.BOOT_COMPLETED -p com.companyname.obsidianscout
```

---

## ?? Expected User Experience

### Scenario 1: Match Notification

```
User subscribes to match at 6:00 PM (match is at 6:20 PM)

5:55 PM - Notification appears
       "Match Reminder"
          "Match starting in 25 minutes!
           
            TXPLA2 - Match #5"

6:15 PM - Another notification
   "Match starting in 5 minutes!"

6:20 PM - Final notification
          "Match starting now!"
```

### Scenario 2: Device Reboot

```
User reboots device at 5:00 PM
5:02 PM - Device boots (user doesn't open app)
5:55 PM - Match notification appears anyway ?
```

---

## ?? Common Issues

### Issue: Time Still Shows "20 minutes"

**Cause:** Old code still running  
**Solution:** 
1. Uninstall app completely
2. Clean build
3. Deploy fresh

### Issue: Service Not Starting on Boot

**Cause:** BootReceiver not registered  
**Solution:** Check manifest:
```powershell
adb shell dumpsys package com.companyname.obsidianscout | findstr "BootReceiver"
```

If not found, rebuild completely.

### Issue: No Boot Logs

**Cause:** Logcat cleared or device took time to boot  
**Solution:** Test boot manually:
```powershell
adb shell am broadcast -a android.intent.action.BOOT_COMPLETED -p com.companyname.obsidianscout
```

---

## ?? Success Indicators

### 1. Accurate Time ?
```
[BackgroundNotifications]   Message: Match starting in 15 minutes!
```
(NOT "20 minutes")

### 2. Boot Auto-Start ?
```
[BootReceiver] Device booted - starting notification service
[ForegroundService] OnCreate called
[BackgroundNotifications] Service started
```

### 3. Works Without App Open ?
- Reboot device
- DON'T open app
- Notification still appears

---

## ?? Files Changed

| File | Status | Purpose |
|------|--------|---------|
| `BackgroundNotificationService.cs` | ? Modified | Calculate time |
| `BootReceiver.cs` | ? NEW | Auto-start |
| `AndroidManifest.xml` | ? Modified | Boot permission |

---

*Deployment Guide - January 2025*  
*Status: ? Ready to Deploy*  
*Build: ? Successful*
