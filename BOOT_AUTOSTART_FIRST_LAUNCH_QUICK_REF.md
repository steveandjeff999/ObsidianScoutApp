# ?? Boot Auto-Start Fix - Quick Reference

## ? What's Fixed

**Problem:** Service doesn't start on boot after fresh install/update  
**Root Cause:** Android 8.0+ blocks broadcast receivers for never-launched apps  
**Solution:** Multi-layer boot handling with reminder notification

---

## ?? Changes Made

| File | Change |
|------|--------|
| `MauiProgram.cs` | Save "app_launched_once" flag |
| `BootReceiver.cs` | Check flag before starting |
| `DeviceBootReceiver.cs` | **NEW** - Enhanced boot handling |
| `AndroidManifest.xml` | Add receivers + SCHEDULE_EXACT_ALARM |

---

## ?? How It Works

### First Boot After Install (App Not Opened):
1. Device reboots
2. Boot receiver checks: App launched? **NO**
3. Shows notification: "Tap to enable match notifications"
4. User taps ? App opens
5. Flag saved: `app_launched_once = true`
6. Service starts

### All Subsequent Boots:
1. Device reboots
2. Boot receiver checks: App launched? **YES**
3. Service starts automatically ?
4. Notifications work WITHOUT opening app ?

---

## ?? Quick Test

```powershell
# 1. Fresh install
adb uninstall com.companyname.obsidianscout
dotnet build -t:Run -f net10.0-android

# 2. DON'T open app - reboot immediately
adb reboot

# 3. Check for reminder notification
adb wait-for-device
adb logcat | findstr "DeviceBootReceiver"
```

**Expected:**
```
[DeviceBootReceiver] App launched once: false
[DeviceBootReceiver] ?? App never launched
[DeviceBootReceiver] ? Reminder notification shown
```

```powershell
# 4. Open app (tap notification or manual)
# Flag is saved automatically

# 5. Reboot again
adb reboot

# 6. Check service starts
adb wait-for-device
adb logcat | findstr "DeviceBootReceiver\|ForegroundService"
```

**Expected:**
```
[DeviceBootReceiver] App launched once: true
[DeviceBootReceiver] ? App was launched before
[DeviceBootReceiver] ? StartForegroundService called
[ForegroundService] OnCreate called
```

---

## ?? User Experience

### After Install/Update:

**First Reboot:**
- Notification: "Tap to enable match notifications"
- User taps ? App opens
- Service starts

**All Future Reboots:**
- Service starts automatically ?
- No app open required ?

---

## ?? Debug Commands

### Check Launch Flag:
```powershell
adb shell "run-as com.companyname.obsidianscout cat /data/data/com.companyname.obsidianscout/shared_prefs/obsidian_scout_prefs.xml"
```

**Should show:**
```xml
<boolean name="app_launched_once" value="true" />
```

### Monitor Boot:
```powershell
adb logcat | findstr "DeviceBootReceiver\|BootReceiver\|ForegroundService\|BackgroundNotifications"
```

---

## ?? Important

### ONE-TIME Setup Required:
After install/update, user must open app **once** before reboot for auto-start to work.

### Android Restriction:
This is a standard Android security requirement for ALL apps (not a bug).

### Reminder Notification:
Automatically guides users through the one-time setup.

---

## ? Success Indicators

- [ ] Reminder notification shows after first reboot (app not opened)
- [ ] Service starts after opening app
- [ ] Service auto-starts on second reboot (no app open)
- [ ] Notifications work without opening app

---

*Quick Reference - January 2025*  
*Android First Launch: ? Handled*  
*Status: ? Fixed*
