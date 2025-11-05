# ?? Service Restart Fix - Deploy & Test

## ? What's Fixed

**Problem:** Service doesn't restart after reboot or when killed by Android  
**Solution:** Moved initialization to `OnStartCommand()` for reliable restarts

---

## ?? Deploy Now

```powershell
# 1. Stop debugging
# Press Shift+F5

# 2. Uninstall old version
adb uninstall com.companyname.obsidianscout

# 3. Rebuild and deploy
dotnet clean
dotnet build -f net10.0-android -c Debug
dotnet build -t:Run -f net10.0-android
```

---

## ?? Test Immediately

### Quick Test 1: Does It Start?

```powershell
# Open app and check logs
adb logcat | findstr "ForegroundService"
```

**Expected within 5 seconds:**
```
[ForegroundService] ===== OnCreate called =====
[ForegroundService] ===== OnStartCommand called =====
[ForegroundService] Initializing background notification service...
[ForegroundService] ? Background notification service started successfully
```

**? SUCCESS:** Shows "? Background notification service started successfully"  
**? FAIL:** Shows error or nothing appears

### Quick Test 2: Does It Survive Restart?

```powershell
# Force stop the service
adb shell am force-stop com.companyname.obsidianscout

# Wait 35 seconds
Start-Sleep -Seconds 35

# Check if it restarted
adb logcat -d | Select-String "ForegroundService.*started successfully"
```

**? SUCCESS:** Shows service restarted  
**? FAIL:** No restart message

### Quick Test 3: Does It Work After Reboot?

```powershell
# Reboot device
adb reboot

# Wait for boot
adb wait-for-device
Start-Sleep -Seconds 60

# Check logs
adb logcat -d | findstr "DeviceBootReceiver\|ForegroundService"
```

**? SUCCESS:** Shows boot receiver ? service starts  
**? FAIL:** No boot receiver or service start

---

## ? Success Checklist

- [ ] Service starts when app opens
- [ ] Logs show "? Background notification service started successfully"
- [ ] Service restarts after force stop (within 30-60s)
- [ ] Service starts on device reboot
- [ ] Notifications poll every 60 seconds

---

## ?? If Tests Fail

### Service Doesn't Start on App Open

**Check:**
```powershell
adb logcat | findstr "ForegroundService.*Exception"
```

**Common issues:**
- Network not available
- Notification permission not granted

### Service Doesn't Restart After Force Stop

**Grant battery optimization exemption:**
```powershell
adb shell dumpsys deviceidle whitelist +com.companyname.obsidianscout
```

### Service Doesn't Start on Reboot

**Check if app was launched:**
```powershell
adb shell "run-as com.companyname.obsidianscout cat /data/data/com.companyname.obsidianscout/shared_prefs/obsidian_scout_prefs.xml"
```

**Should show:**
```xml
<boolean name="app_launched_once" value="true" />
```

If not, open the app once, then reboot again.

---

## ?? All Tests Pass?

**You're done!** The service now:
- ? Restarts automatically
- ? Survives device reboots
- ? Continues when app closes
- ? Polls notifications reliably

---

*Quick Deploy & Test Guide - January 2025*  
*Status: ? Ready*
