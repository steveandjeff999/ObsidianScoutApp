# ?? Reboot Notifications Fix - Deploy & Test

## ? What Was Fixed

**Problem:** Notifications don't work after device reboot until you open the app

**Root Causes Fixed:**
1. App launch flag not persisting across reboots (Android 8-9)
2. Service not initializing properly after cold boot
3. Insufficient retry logic for boot scenarios

**Solutions Implemented:**
1. **Persistent storage in 3 locations** - SharedPreferences, PreferenceManager, File
2. **Boot-aware initialization** - Longer delays and more retries after boot
3. **Enhanced logging** - Better visibility into boot process

---

## ?? Files Changed

| File | Change |
|------|--------|
| `PersistentPreferences.cs` | **NEW** - Multi-location flag storage |
| `MauiProgram.cs` | Use PersistentPreferences |
| `DeviceBootReceiver.cs` | Use PersistentPreferences |
| `ForegroundNotificationService.cs` | Boot-aware retry logic |

---

## ?? Deploy Now

```powershell
# 1. Uninstall old version completely
adb uninstall com.companyname.obsidianscout

# 2. Clean and rebuild
dotnet clean
dotnet build -f net10.0-android -c Debug

# 3. Deploy
dotnet build -t:Run -f net10.0-android
```

---

## ?? Critical Tests

### Test 1: Open App and Set Flag

```powershell
# App should be open now after deploy
# Check that flag is set
adb logcat -d | findstr "PersistentPreferences"
```

**Expected:**
```
[PersistentPreferences] Setting app launched flag to: true
[PersistentPreferences] ? App launched flag saved to all storage locations
```

### Test 2: Verify Flag Persists

```powershell
# Close app completely
adb shell am force-stop com.companyname.obsidianscout

# Check if flag is still readable
adb shell am broadcast -a android.intent.action.BOOT_COMPLETED -p com.companyname.obsidianscout

# Check logs
adb logcat -d | findstr "PersistentPreferences\|DeviceBootReceiver"
```

**Expected:**
```
[PersistentPreferences] ? Found flag in SharedPreferences
[DeviceBootReceiver] App launched once: true
```

### Test 3: Actual Reboot Test

```powershell
# Reboot the device
adb reboot

# Wait for boot (2 minutes)
adb wait-for-device
Start-Sleep -Seconds 120

# Check if service started
adb logcat -d | findstr "DeviceBootReceiver\|ForegroundService\|BackgroundNotifications"
```

**Expected logs (in order):**
```
[DeviceBootReceiver] ===== BOOT RECEIVED =====
[DeviceBootReceiver] Action: android.intent.action.BOOT_COMPLETED
[PersistentPreferences] ? Found flag in SharedPreferences
[DeviceBootReceiver] App launched once: true
[DeviceBootReceiver] ? App was launched before - proceeding with service start
[DeviceBootReceiver] Starting ForegroundNotificationService...
[DeviceBootReceiver] ? StartForegroundService called (Android 8.0+)
[ForegroundService] ===== OnStartCommand called =====
[ForegroundService] Started from boot: true
[ForegroundService] Initializing background notification service...
[ForegroundService] Initialization attempt 1/5
[ForegroundService] Waiting 5000ms before attempt...
[ForegroundService] ? Background notification service started successfully
[BackgroundNotifications] Service started - polling every 60 seconds
[BackgroundNotifications] === POLL START ===
```

### Test 4: Verify Service Is Running

```powershell
# Check service status
adb shell dumpsys activity services | findstr "ForegroundNotificationService"
```

**Expected:**
```
* ServiceRecord{...} u0 com.companyname.obsidianscout/.ForegroundNotificationService}
  app=ProcessRecord{...} pid=12345
  isForeground=true
```

### Test 5: Verify Polling Continues

```powershell
# Wait 65 seconds and check for poll
Start-Sleep -Seconds 65
adb logcat -d | Select-String "POLL" | Select-Object -Last 5
```

**Expected:**
```
[BackgroundNotifications] === POLL START ===
[BackgroundNotifications] Checking for missed notifications...
[BackgroundNotifications] Checking scheduled notifications...
[BackgroundNotifications] === POLL END (1.2s) ===
```

### Test 6: Multiple Reboots

```powershell
# Test stability across multiple reboots
for ($i=1; $i -le 3; $i++) {
Write-Host "`n===== REBOOT TEST $i/3 ====="
    adb reboot
 adb wait-for-device
    Start-Sleep -Seconds 120
    
    $service = adb shell dumpsys activity services | Select-String "ForegroundNotificationService"
    $polling = adb logcat -d | Select-String "POLL START"
  
    if ($service -and $polling) {
        Write-Host "? PASS: Service running and polling after reboot $i"
  } else {
        Write-Host "? FAIL: Service issues after reboot $i"
 if (!$service) { Write-Host "  - Service not running" }
  if (!$polling) { Write-Host "  - No polling detected" }
    }
}
```

---

## ?? Troubleshooting

### Issue: "No flag found in any storage"

**Check:**
```powershell
# Verify app was opened
adb logcat -d | findstr "MauiProgram.*APP LAUNCHED"
```

**If not found:** App wasn't fully opened after install. Open it manually.

### Issue: Service Starts But Doesn't Initialize

**Check initialization logs:**
```powershell
adb logcat -d | findstr "ForegroundService.*Initialization attempt"
```

**Common causes:**
- Network not available yet
- Services throw exceptions during creation

**Check for exceptions:**
```powershell
adb logcat -d | findstr "Exception"
```

### Issue: Service Dies After Boot

**Check battery optimization:**
```powershell
adb shell dumpsys deviceidle whitelist | findstr "obsidianscout"
```

**If not whitelisted:**
```powershell
# Grant exemption
adb shell dumpsys deviceidle whitelist +com.companyname.obsidianscout
```

### Issue: Boot Receiver Not Triggering

**Test manually:**
```powershell
adb shell am broadcast -a android.intent.action.BOOT_COMPLETED -p com.companyname.obsidianscout
```

**Check receiver registration:**
```powershell
adb shell dumpsys package com.companyname.obsidianscout | findstr "DeviceBootReceiver"
```

---

## ? Success Checklist

After completing all tests:

- [ ] Flag set when app opens
- [ ] Flag persists after force stop
- [ ] Boot receiver triggers on reboot
- [ ] Service starts automatically after reboot
- [ ] BackgroundNotificationService initializes
- [ ] Polling starts within 2 minutes of boot
- [ ] Service stays running for > 30 minutes
- [ ] Works across multiple reboots (3+)
- [ ] No "app never launched" in logs
- [ ] No initialization failures

---

## ?? Expected Timeline After Reboot

| Time | Event |
|------|-------|
| 0:00 | Device starts booting |
| 1:00 | Android system fully booted |
| 1:05 | Boot receiver triggers |
| 1:05 | ForegroundNotificationService starts |
| 1:10 | First initialization attempt (5s delay) |
| 1:15 | BackgroundNotificationService starts |
| 1:15 | Service updates notification to "active" |
| 2:15 | First notification poll (60s after start) |
| 3:15 | Second notification poll |
| ... | Continues every 60 seconds |

---

## ?? What You Should See

### After Deploy:
```
[MauiProgram] ===== APP LAUNCHED =====
[PersistentPreferences] ? App launched flag saved to all storage locations
[ForegroundService] ? Background notification service started successfully
```

### After Reboot (Without Opening App):
```
[DeviceBootReceiver] ? App was launched before - proceeding with service start
[ForegroundService] Started from boot: true
[ForegroundService] ? Background notification service started successfully
[BackgroundNotifications] === POLL START ===
```

### 30 Minutes Later:
```
[BackgroundNotifications] === POLL START ===
# (repeating every 60 seconds)
```

---

*Reboot Notifications Fix - Deploy & Test Guide*  
*Status: ? Ready*  
*Build: ? Successful*
