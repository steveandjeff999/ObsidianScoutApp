# ?? Background Notifications After Reboot - Diagnostic & Fix

## ?? The Problem

**After device reboot, background notifications don't work until you open the app.**

### Root Causes

1. **Service not starting on boot** - Boot receiver may not be triggering
2. **Service starting but not initializing** - Service starts but BackgroundNotificationService fails
3. **Battery optimization killing service** - Android kills service shortly after boot
4. **App launch flag not persisting** - Flag gets cleared on reboot

---

## ?? Diagnostic Steps

### Step 1: Check If Boot Receiver Triggers

```powershell
# Reboot and immediately check logs
adb reboot
Start-Sleep -Seconds 90
adb logcat -d | findstr "DeviceBootReceiver"
```

**Expected output:**
```
[DeviceBootReceiver] ===== BOOT RECEIVED =====
[DeviceBootReceiver] Action: android.intent.action.BOOT_COMPLETED
[DeviceBootReceiver] App launched once: true
[DeviceBootReceiver] ? App was launched before - proceeding with service start
[DeviceBootReceiver] Starting ForegroundNotificationService...
[DeviceBootReceiver] ? StartForegroundService called
```

**? If you see:**
```
[DeviceBootReceiver] App launched once: false
```
? **Problem:** App launch flag not persisting  
? **Fix:** See "Fix #1" below

**? If you see nothing:**
? **Problem:** Boot receiver not registered or not triggering  
? **Fix:** See "Fix #2" below

### Step 2: Check If Service Starts

```powershell
# Check if service is running after boot
adb shell dumpsys activity services | findstr "ForegroundNotificationService"
```

**Expected output:**
```
* ServiceRecord{...} u0 com.companyname.obsidianscout/.ForegroundNotificationService}
  app=ProcessRecord{...} pid=12345
  isForeground=true
```

**? If service not found:**
? **Problem:** Service failed to start  
? **Fix:** See "Fix #3" below

### Step 3: Check If BackgroundNotificationService Initializes

```powershell
# Check background service logs
adb logcat -d | findstr "BackgroundNotifications"
```

**Expected output:**
```
[ForegroundService] ? Background notification service started successfully
[BackgroundNotifications] Service started - polling every 60 seconds
[BackgroundNotifications] === POLL START ===
```

**? If you see:**
```
[ForegroundService] ? Failed to start background notification service
```
? **Problem:** Background service initialization failed  
? **Fix:** See "Fix #4" below

### Step 4: Check Battery Optimization

```powershell
# Check if app is optimized
adb shell dumpsys deviceidle whitelist | findstr "obsidianscout"
```

**Expected:** App should be in whitelist

**? If not found:**
? **Problem:** Battery optimization killing service  
? **Fix:** See "Fix #5" below

---

## ? Fixes

### Fix #1: App Launch Flag Not Persisting

**Problem:** `app_launched_once` flag cleared on reboot (Android 8-9)

**Solution: Use persistent storage**

Create file: `ObsidianScout/Platforms/Android/PersistentPreferences.cs`

```csharp
using Android.Content;
using Android.Preferences;

namespace ObsidianScout.Platforms.Android
{
    public static class PersistentPreferences
  {
      private const string PREFS_NAME = "obsidian_scout_persistent_prefs";
  private const string KEY_APP_LAUNCHED = "app_launched_once";
   
 public static void SetAppLaunched(Context context, bool launched)
 {
  try
  {
       // Use multiple storage methods for redundancy
         
        // Method 1: SharedPreferences (primary)
        var prefs = context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
      var editor = prefs.Edit();
       editor.PutBoolean(KEY_APP_LAUNCHED, launched);
           editor.Apply();
    editor.Commit(); // Force synchronous write
      
    // Method 2: PreferenceManager (backup)
      var defaultPrefs = PreferenceManager.GetDefaultSharedPreferences(context);
   var defaultEditor = defaultPrefs.Edit();
    defaultEditor.PutBoolean(KEY_APP_LAUNCHED, launched);
          defaultEditor.Apply();
            defaultEditor.Commit();
       
          // Method 3: File-based (tertiary backup)
    var flagFile = System.IO.Path.Combine(context.FilesDir.AbsolutePath, ".app_launched");
                System.IO.File.WriteAllText(flagFile, launched.ToString());
                
              System.Diagnostics.Debug.WriteLine($"[PersistentPreferences] App launched flag set to: {launched}");
      }
      catch (System.Exception ex)
            {
     System.Diagnostics.Debug.WriteLine($"[PersistentPreferences] Error setting flag: {ex.Message}");
            }
        }
        
        public static bool GetAppLaunched(Context context)
        {
            try
       {
         // Check Method 1: SharedPreferences
      var prefs = context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
     if (prefs.GetBoolean(KEY_APP_LAUNCHED, false))
            {
   System.Diagnostics.Debug.WriteLine("[PersistentPreferences] Found flag in SharedPreferences");
          return true;
       }

        // Check Method 2: PreferenceManager
                var defaultPrefs = PreferenceManager.GetDefaultSharedPreferences(context);
    if (defaultPrefs.GetBoolean(KEY_APP_LAUNCHED, false))
    {
         System.Diagnostics.Debug.WriteLine("[PersistentPreferences] Found flag in PreferenceManager");
   return true;
            }
         
             // Check Method 3: File-based
      var flagFile = System.IO.Path.Combine(context.FilesDir.AbsolutePath, ".app_launched");
           if (System.IO.File.Exists(flagFile))
         {
    var content = System.IO.File.ReadAllText(flagFile);
      if (bool.TryParse(content, out var result) && result)
        {
         System.Diagnostics.Debug.WriteLine("[PersistentPreferences] Found flag in file");
    return true;
    }
          }
     
   System.Diagnostics.Debug.WriteLine("[PersistentPreferences] No flag found in any storage");
           return false;
  }
        catch (System.Exception ex)
   {
            System.Diagnostics.Debug.WriteLine($"[PersistentPreferences] Error getting flag: {ex.Message}");
   return false;
         }
        }
    }
}
```

**Update MauiProgram.cs:**

```csharp
#if ANDROID
try
{
    var context = Android.App.Application.Context;
    
 // Use persistent preferences
    ObsidianScout.Platforms.Android.PersistentPreferences.SetAppLaunched(context, true);
    
    System.Diagnostics.Debug.WriteLine("[MauiProgram] App launched - starting ForegroundNotificationService");
    
var intent = new Intent(context, typeof(ObsidianScout.Platforms.Android.ForegroundNotificationService));
    context.StartForegroundService(intent);
    
    System.Diagnostics.Debug.WriteLine("[MauiProgram] ForegroundNotificationService started successfully");
}
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"Failed to start ForegroundNotificationService: {ex.Message}");
}
#endif
```

**Update DeviceBootReceiver.cs:**

```csharp
// In OnReceive method, replace:
var prefs = context.GetSharedPreferences("obsidian_scout_prefs", FileCreationMode.Private);
var appLaunchedOnce = prefs.GetBoolean("app_launched_once", false);

// With:
var appLaunchedOnce = PersistentPreferences.GetAppLaunched(context);
```

### Fix #2: Boot Receiver Not Triggering

**Add manifest query for boot receiver:**

In `AndroidManifest.xml`, add before `</application>`:

```xml
<!-- Ensure boot receiver is exported and enabled -->
<receiver
    android:name="crc64f3faeb7d35d8db75.DeviceBootReceiver"
    android:enabled="true"
    android:exported="true"
    android:directBootAware="true"
    android:permission="android.permission.RECEIVE_BOOT_COMPLETED">
    <intent-filter android:priority="1000">
        <action android:name="android.intent.action.BOOT_COMPLETED" />
   <action android:name="android.intent.action.LOCKED_BOOT_COMPLETED" />
        <action android:name="android.intent.action.QUICKBOOT_POWERON" />
        <action android:name="com.htc.intent.action.QUICKBOOT_POWERON" />
    <action android:name="android.intent.action.MY_PACKAGE_REPLACED" />
        <category android:name="android.intent.category.DEFAULT" />
    </intent-filter>
</receiver>
```

**Test boot receiver manually:**

```powershell
# Simulate boot
adb shell am broadcast -a android.intent.action.BOOT_COMPLETED -p com.companyname.obsidianscout
```

### Fix #3: Service Not Starting

**Add service restart on boot completion:**

Update `ForegroundNotificationService.cs` to handle cold starts better:

```csharp
public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
{
    System.Diagnostics.Debug.WriteLine("[ForegroundService] ===== OnStartCommand called =====");
    
 var startedFromBoot = intent?.GetBooleanExtra("started_from_boot", false) ?? false;
    System.Diagnostics.Debug.WriteLine($"[ForegroundService] Started from boot: {startedFromBoot}");
    
    // Always ensure foreground notification
    try
    {
     StartForeground(FOREGROUND_ID, BuildForegroundNotification("ObsidianScout notifications active"));
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[ForegroundService] Failed to start foreground: {ex.Message}");
    }
  
    // Initialize service if needed
  if (!_isInitialized || !_isRunning || startedFromBoot)
    {
   System.Diagnostics.Debug.WriteLine("[ForegroundService] Initializing background notification service...");
        InitializeBackgroundService();
    }
    
    return StartCommandResult.Sticky;
}
```

### Fix #4: Background Service Not Initializing

**Add retry logic with exponential backoff:**

```csharp
private int _initRetryCount = 0;
private const int MAX_INIT_RETRIES = 5;

private void InitializeBackgroundService()
{
    try
    {
    _isRunning = true;
        
        // ... existing wake lock code ...
        
  // Initialize background notification service with retry
        Task.Run(async () =>
        {
      for (int attempt = 0; attempt < MAX_INIT_RETRIES; attempt++)
            {
                try
    {
         System.Diagnostics.Debug.WriteLine($"[ForegroundService] Initialization attempt {attempt + 1}/{MAX_INIT_RETRIES}");
     
           // Wait longer after boot to let system settle
        var delay = attempt == 0 ? 5000 : (int)Math.Pow(2, attempt) * 2000;
       await Task.Delay(delay);
 
               var apiService = new ApiService(_http!, new SettingsService(), new CacheService(), new ConnectivityService());
         var settingsService = new SettingsService();
       var localNotificationService = new LocalNotificationService();

         _backgroundNotificationService = new BackgroundNotificationService(apiService, settingsService, localNotificationService);
        await _backgroundNotificationService.StartAsync();
  
    _isInitialized = true;
               _initRetryCount = 0;
  
        System.Diagnostics.Debug.WriteLine("[ForegroundService] ? Background notification service started successfully");
      UpdateForegroundNotification("ObsidianScout - Notifications active");
          
  return; // Success!
   }
              catch (Exception ex)
                {
            System.Diagnostics.Debug.WriteLine($"[ForegroundService] Attempt {attempt + 1} failed: {ex.Message}");
             
  if (attempt == MAX_INIT_RETRIES - 1)
         {
        System.Diagnostics.Debug.WriteLine("[ForegroundService] ? All initialization attempts failed");
      UpdateForegroundNotification("ObsidianScout - Initialization failed");
        }
          }
       }
  });
    }
    catch (Exception ex)
    {
System.Diagnostics.Debug.WriteLine($"[ForegroundService] InitializeBackgroundService exception: {ex.Message}");
    }
}
```

### Fix #5: Battery Optimization

**Add to MainActivity.cs OnCreate:**

```csharp
protected override void OnCreate(Bundle? savedInstanceState)
{
    base.OnCreate(savedInstanceState);
    
    // Request battery optimization exemption
  if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
    {
        var intent = new Intent();
    var packageName = PackageName;
        var pm = (PowerManager?)GetSystemService(PowerService);
        
        if (pm != null && !pm.IsIgnoringBatteryOptimizations(packageName))
        {
       intent.SetAction(global::Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
            intent.SetData(global::Android.Net.Uri.Parse("package:" + packageName));
    
            try
            {
      StartActivity(intent);
            }
  catch (Exception ex)
   {
    System.Diagnostics.Debug.WriteLine($"Failed to request battery optimization exemption: {ex.Message}");
  }
        }
    }
}
```

---

## ?? Complete Test Procedure

### Test 1: Fresh Reboot

```powershell
# 1. Deploy app
dotnet build -t:Run -f net10.0-android

# 2. Open app once (sets flag)
# Let it run for 1 minute to verify notifications work

# 3. Reboot WITHOUT opening app again
adb reboot

# 4. Wait 2 minutes for boot to complete
Start-Sleep -Seconds 120

# 5. Check service status
adb shell dumpsys activity services | findstr "ForegroundNotificationService"

# 6. Check logs
adb logcat -d | findstr "DeviceBootReceiver\|ForegroundService\|BackgroundNotifications"
```

**Expected logs:**
```
[DeviceBootReceiver] ===== BOOT RECEIVED =====
[DeviceBootReceiver] App launched once: true
[DeviceBootReceiver] Starting ForegroundNotificationService...
[ForegroundService] ===== OnStartCommand called =====
[ForegroundService] Started from boot: true
[ForegroundService] Initializing background notification service...
[ForegroundService] ? Background notification service started successfully
[BackgroundNotifications] Service started - polling every 60 seconds
```

### Test 2: Multiple Reboots

```powershell
# Test persistence across multiple reboots
for ($i=1; $i -le 3; $i++) {
    Write-Host "`n===== REBOOT TEST $i ====="
    adb reboot
 Start-Sleep -Seconds 120
    
    $serviceRunning = adb shell dumpsys activity services | Select-String "ForegroundNotificationService"
    if ($serviceRunning) {
        Write-Host "? PASS: Service running after reboot $i"
    } else {
        Write-Host "? FAIL: Service NOT running after reboot $i"
    }
    
    Start-Sleep -Seconds 60
}
```

### Test 3: Long-term Stability

```powershell
# Check if service stays running 30 minutes after boot
adb reboot
Start-Sleep -Seconds 120

Write-Host "Service started. Waiting 30 minutes..."
Start-Sleep -Seconds 1800

$serviceRunning = adb shell dumpsys activity services | Select-String "ForegroundNotificationService"
if ($serviceRunning) {
    Write-Host "? Service still running after 30 minutes"
} else {
    Write-Host "? Service died after 30 minutes"
}
```

---

## ? Success Indicators

After applying all fixes:

- [ ] Boot receiver triggers on every reboot
- [ ] App launch flag persists across reboots
- [ ] Service starts automatically after reboot
- [ ] BackgroundNotificationService initializes successfully
- [ ] Notifications poll every 60 seconds
- [ ] Service stays running for > 30 minutes after boot
- [ ] Works across multiple reboots
- [ ] No "app never launched" messages in logs

---

## ?? Before vs After

| Event | Before | After |
|-------|--------|-------|
| Reboot device | ? No notifications | ? Notifications work |
| Don't open app | ? Service doesn't start | ? Service starts automatically |
| Multiple reboots | ? Unreliable | ? Consistent |
| 30 min after boot | ? Service killed | ? Service running |

---

*Background Notifications Reboot Fix - January 2025*  
*Status: ? Comprehensive Solution*
