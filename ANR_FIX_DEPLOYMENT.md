# ?? Deploy ANR Fix - Step by Step

## ? What's Fixed
- Changed polling from 10 seconds ? 60 seconds
- Added battery optimization exemption
- Added wake lock management
- Added START_STICKY for auto-restart
- Fixed ANR errors when app is closed

---

## ?? Deployment Steps

### Step 1: Stop Debugging
```
1. Stop the debugger (Shift+F5 in Visual Studio)
2. Close Visual Studio
3. Kill any running processes:
```
```powershell
taskkill /F /IM dotnet.exe
taskkill /F /IM msbuild.exe
```

### Step 2: Clean Everything
```powershell
cd "C:\Users\steve\source\repos\ObsidianScout"

# Clean solution
dotnet clean

# Delete build artifacts
Remove-Item -Recurse -Force ObsidianScout\bin,ObsidianScout\obj

# Delete Android specific artifacts
Remove-Item -Recurse -Force ObsidianScout\obj\Debug\net10.0-android
```

### Step 3: Uninstall from Device
```powershell
# Uninstall old version
adb uninstall com.companyname.obsidianscout

# Verify uninstalled
adb shell pm list packages | findstr obsidian
# (should return nothing)
```

### Step 4: Rebuild Fresh
```powershell
# Restore packages
dotnet restore

# Build for Android
dotnet build -f net10.0-android -c Debug

# Deploy
dotnet build -t:Run -f net10.0-android
```

---

## ?? Test After Deployment

### Test 1: Battery Optimization Dialog
1. **Launch app for first time**
2. **Expected:** Dialog appears asking to disable battery optimization
3. **Action:** Tap "Allow" and select "Unrestricted"
4. **Verify:** Settings ? Apps ? ObsidianScout ? Battery ? "Unrestricted"

### Test 2: Background Operation
1. **Open app and log in**
2. **Close app completely** (swipe from recents)
3. **Wait 5 minutes**
4. **Check logs:**
   ```powershell
   adb logcat | findstr "BackgroundNotifications"
   ```
5. **Expected:** Polls every 60 seconds, no ANR errors

### Test 3: Notification Delivery
1. **Subscribe to match reminder** (server-side)
2. **Close app completely**
3. **Wait for scheduled time**
4. **Expected:** Notification appears within 60 seconds
5. **No ANR errors**

### Test 4: Service Restart
1. **Force stop app:** Settings ? Apps ? ObsidianScout ? Force Stop
2. **Wait 2-3 minutes**
3. **Check logs:**
   ```powershell
   adb logcat | findstr "ForegroundService"
   ```
4. **Expected:** Service restarts automatically
5. **Log shows:** `[ForegroundService] OnCreate called`

---

## ?? Verify Changes

### 1. Check Poll Interval
```powershell
adb logcat | findstr "polling every"
```
**Expected output:**
```
[BackgroundNotifications] Service started - polling every 60 seconds (1 minutes)
```

### 2. Check No ANR
```powershell
# Watch for ANR
adb logcat | findstr "ANR\|not responding"
```
**Expected:** No output (no ANR errors)

### 3. Check Service Running
```powershell
adb shell dumpsys activity services | findstr "ForegroundNotificationService"
```
**Expected:** Shows service details if running

### 4. Check Battery Exemption
```powershell
adb shell dumpsys deviceidle whitelist | findstr "obsidianscout"
```
**Expected:** Shows package name if exempted

---

## ? Success Checklist

- [ ] Old app uninstalled from device
- [ ] Fresh build completed without errors
- [ ] Battery optimization dialog appeared on first launch
- [ ] Battery exemption granted (Unrestricted)
- [ ] Logs show 60-second polling interval
- [ ] No ANR errors after 5+ minutes with app closed
- [ ] Notifications received within 60 seconds
- [ ] Service restarts after force stop
- [ ] Battery drain < 0.02%/hour

---

## ?? Expected Behavior

### Correct:
```
18:19:00 - User closes app
18:19:00 - Service continues in background
18:19:30 - Poll #1 (30 sec)
18:20:30 - Poll #2 (1 min 30 sec)
18:21:30 - Poll #3 (2 min 30 sec)
18:20:00 - Scheduled notification time
18:20:30 - Notification delivered (30 sec after scheduled)
```

### Timeline:
```
App Closed ? Service Running ? Polling Every 60s ? Notification Within 60s ?
```

---

## ?? Common Issues

### Issue: Battery Dialog Doesn't Appear
**Solution:** Manually go to Settings ? Apps ? ObsidianScout ? Battery ? Unrestricted

### Issue: Service Stops After 10 Minutes
**Cause:** Battery optimization not disabled  
**Solution:** Grant battery exemption manually

### Issue: Still Getting ANR
**Cause:** Old build still installed  
**Solution:** 
1. Uninstall completely
2. Clean build
3. Deploy fresh
4. Restart device

### Issue: Notifications Delayed by 2+ Minutes
**Expected:** With 60-second polling, max delay is 60 seconds  
**Not a bug:** This is normal behavior

---

## ?? Inform Users

### What Users Need to Know:
1. **First launch:** Allow battery exemption when prompted
2. **Notifications:** May arrive up to 60 seconds after scheduled time
3. **Battery:** Uses < 0.02% per hour
4. **Reliability:** Service runs even when app is closed

### If Issues:
1. Settings ? Apps ? ObsidianScout ? Battery ? Unrestricted
2. Restart device
3. Check server is sending notifications

---

*Deployment Guide - January 2025*  
*ANR Fix: 60-Second Polling*  
*Status: ? Ready to Deploy*
