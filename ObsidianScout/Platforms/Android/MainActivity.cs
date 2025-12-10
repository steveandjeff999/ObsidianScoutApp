using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Android;
using Android.Content;
using Android.Provider;
using AndroidX.Core.View;
using Android.Views;
using Android.Runtime;
using ObsidianScout.Services;

namespace ObsidianScout
{
    [Activity(
        Theme = "@style/Maui.SplashTheme", 
  MainLauncher = true, 
        LaunchMode = LaunchMode.SingleTop, 
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        HardwareAccelerated = true,  // ← CRITICAL: Force hardware acceleration
        WindowSoftInputMode = SoftInput.AdjustResize)]  // ← Smooth keyboard handling
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable })]
    public class MainActivity : MauiAppCompatActivity
    {
    private bool _isDestroying = false;
        
 // Store pending navigation from notification intent
 private static string? _pendingNavigationUri = null;
    private static bool _hasPendingNavigation = false;
    private static readonly object _navigationLock = new object();

        protected override void OnCreate(Bundle? savedInstanceState)
        {
    // ========================================
     // CRITICAL: Performance optimizations BEFORE base.OnCreate
            // ========================================
        
  // Enable StrictMode in debug builds to catch performance issues
     #if DEBUG
        EnableStrictMode();
     #endif
 
     // Optimize window for performance (without edge-to-edge that causes TabBar overlap)
    OptimizeWindowPerformance();
    
     // Apply global Android optimizations
      Platforms.Android.AndroidPerformanceOptimizer.ApplyGlobalOptimizations(this);

    base.OnCreate(savedInstanceState);

      try
   {
   System.Diagnostics.Debug.WriteLine("[MainActivity] OnCreate called");
             
    // ========================================
        // Set status bar and navigation bar colors (NO edge-to-edge)
       // ========================================
     if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop && Window != null)
   {
  // IMPORTANT: Do NOT use SetDecorFitsSystemWindows(false) - it causes TabBar overlap
        // Keep the default behavior where app content fits within system windows
    
         var uiMode = Resources?.Configuration?.UiMode ?? 0;
   var isNightMode = (uiMode & global::Android.Content.Res.UiMode.NightMask) == global::Android.Content.Res.UiMode.NightYes;
     
      if (isNightMode)
        {
     // Dark mode colors
   Window.SetStatusBarColor(new Android.Graphics.Color(0x1E, 0x1E, 0x1E)); // #1E1E1E
   Window.SetNavigationBarColor(new Android.Graphics.Color(0x2D, 0x2D, 0x30)); // #2D2D30
  }
else
          {
  // Light mode colors
    Window.SetStatusBarColor(new Android.Graphics.Color(0x63, 0x66, 0xF1)); // #6366F1
          Window.SetNavigationBarColor(new Android.Graphics.Color(0xFF, 0xFF, 0xFF)); // #FFFFFF
  }
       
        // Set light/dark status bar icons based on theme
if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
          {
   var windowInsetsController = Window.InsetsController;
      if (windowInsetsController != null)
{
              if (isNightMode)
      {
      // Dark mode - use light status bar icons
  windowInsetsController.SetSystemBarsAppearance(0, (int)WindowInsetsControllerAppearance.LightStatusBars);
        }
  else
   {
     // Light mode - use dark status bar icons
  windowInsetsController.SetSystemBarsAppearance(
        (int)WindowInsetsControllerAppearance.LightStatusBars,
    (int)WindowInsetsControllerAppearance.LightStatusBars);
        }
   }
 }
}

        // ========================================
     // CRITICAL: Initialize heavy operations asynchronously
    // ========================================
   InitializeAsync();
        }
      catch (System.Exception ex)
  {
          System.Diagnostics.Debug.WriteLine($"[MainActivity] OnCreate error: {ex.Message}");
            }
        }

        /// <summary>
        /// Optimize window for maximum performance (WITHOUT edge-to-edge mode)
        /// </summary>
        private void OptimizeWindowPerformance()
        {
            try
{
  if (Window == null) return;

     // ========================================
    // HARDWARE ACCELERATION ONLY - NO EDGE-TO-EDGE
// ========================================
  
  // Enable hardware acceleration at window level
       Window.SetFlags(WindowManagerFlags.HardwareAccelerated, WindowManagerFlags.HardwareAccelerated);
  
         // Set pixel format for best performance
   Window.SetFormat(global::Android.Graphics.Format.Rgba8888);
         
  // DO NOT use these flags - they cause TabBar to go under system navigation:
       // - WindowManagerFlags.LayoutNoLimits
  // - WindowManagerFlags.TranslucentNavigation
 // - SetDecorFitsSystemWindows(false)

      System.Diagnostics.Debug.WriteLine("[MainActivity] Window performance optimizations applied (no edge-to-edge)");
          }
   catch (System.Exception ex)
       {
      System.Diagnostics.Debug.WriteLine($"[MainActivity] Window optimization error: {ex.Message}");
   }
     }

        /// <summary>
        /// Enable StrictMode to catch performance issues in debug builds
     /// </summary>
     [System.Diagnostics.Conditional("DEBUG")]
  private void EnableStrictMode()
        {
       try
      {
   // Thread policy - detect slow operations on main thread
      StrictMode.SetThreadPolicy(new StrictMode.ThreadPolicy.Builder()
    .DetectAll()
          .PenaltyLog()
   .Build());

 // VM policy - detect memory leaks and other issues
     StrictMode.SetVmPolicy(new StrictMode.VmPolicy.Builder()
      .DetectAll()
       .PenaltyLog()
      .Build());

     System.Diagnostics.Debug.WriteLine("[MainActivity] StrictMode enabled for performance monitoring");
}
       catch (System.Exception ex)
        {
    System.Diagnostics.Debug.WriteLine($"[MainActivity] StrictMode setup error: {ex.Message}");
   }
    }

        /// <summary>
    /// Initialize heavy operations asynchronously to avoid blocking UI
        /// </summary>
 private async void InitializeAsync()
      {
       try
         {
          // Run heavy initialization on background thread
       await Task.Run(() =>
    {
      try
  {
        // Android13+ requires runtime permission for notifications
  if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
       {
 MainThread.BeginInvokeOnMainThread(() =>
   {
    if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.PostNotifications) != (int)Permission.Granted)
  {
ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.PostNotifications }, 1001);
 }
    });
 }

        // Create notification channel (IO operation - do async)
     CreateNotificationChannel();

       // Request battery optimization exemption (async)
      MainThread.BeginInvokeOnMainThread(() => RequestBatteryOptimizationExemption());
           }
 catch (System.Exception ex)
  {
          System.Diagnostics.Debug.WriteLine($"[MainActivity] Background init error: {ex.Message}");
           }
          });

         // Process notification intent (lightweight operation)
    ProcessNotificationIntent(Intent);
    }
      catch (System.Exception ex)
 {
        System.Diagnostics.Debug.WriteLine($"[MainActivity] InitializeAsync error: {ex.Message}");
    }
 }

        /// <summary>
        /// Create notification channel (moved to separate method for async execution)
   /// </summary>
     private void CreateNotificationChannel()
        {
 try
  {
       var context = global::Android.App.Application.Context;
       if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
       {
    var channelId = "obsidian_scout_channel";
        var channelName = "ObsidianScout Notifications";
          var channelDesc = "General notifications from ObsidianScout";

var nm = (global::Android.App.NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
   if (nm != null)
   {
        var existing = nm.GetNotificationChannel(channelId);
     if (existing == null)
        {
       var channel = new global::Android.App.NotificationChannel(channelId, channelName, global::Android.App.NotificationImportance.High)
   {
   Description = channelDesc
       };
      nm.CreateNotificationChannel(channel);
    }
          }
     }
     }
        catch (System.Exception ex)
   {
      System.Diagnostics.Debug.WriteLine($"[MainActivity] Notification channel error: {ex.Message}");
  }
        }

        protected override void OnNewIntent(Intent? intent)
   {
      base.OnNewIntent(intent);
            
 // CRITICAL: Update the Intent property so it's available to the activity
         Intent = intent;

      try
     {
  System.Diagnostics.Debug.WriteLine("[MainActivity] OnNewIntent - handling notification tap while app is running");
   
    // Process intent and get navigation data
      var navData = ExtractNavigationData(intent);
      
       if (navData != null && navData.Count > 0)
            {
 // App is running - navigate immediately using the NotificationNavigationService
        System.Diagnostics.Debug.WriteLine("[MainActivity] App is in foreground, triggering immediate navigation via service");
                TriggerImmediateNavigation(navData);
            }
  }
       catch (System.Exception ex)
  {
          System.Diagnostics.Debug.WriteLine($"[MainActivity] OnNewIntent error: {ex.Message}");
      }
        }

        /// <summary>
        /// Extract navigation data from an intent.
        /// </summary>
   private Dictionary<string, string>? ExtractNavigationData(Intent? intent)
        {
            if (intent == null)
       {
    System.Diagnostics.Debug.WriteLine("[MainActivity] Intent is null, skipping");
      return null;
  }

        try
   {
 var type = intent.GetStringExtra("type");
 var sourceType = intent.GetStringExtra("sourceType");
                var sourceId = intent.GetStringExtra("sourceId");
           var messageId = intent.GetStringExtra("messageId");
   var eventCode = intent.GetStringExtra("eventCode");
          var eventId = intent.GetStringExtra("eventId");
      var matchNumber = intent.GetStringExtra("matchNumber");

            System.Diagnostics.Debug.WriteLine($"[MainActivity] Processing intent extras:");
    System.Diagnostics.Debug.WriteLine($"  type: {type}");
       System.Diagnostics.Debug.WriteLine($"  sourceType: {sourceType}");
 System.Diagnostics.Debug.WriteLine($"  sourceId: {sourceId}");
                System.Diagnostics.Debug.WriteLine($"  eventCode: {eventCode}");
    System.Diagnostics.Debug.WriteLine($"  eventId: {eventId}");
         System.Diagnostics.Debug.WriteLine($"  matchNumber: {matchNumber}");

       // If no type, this isn't a notification intent
       if (string.IsNullOrEmpty(type))
       {
     System.Diagnostics.Debug.WriteLine("[MainActivity] No notification type found in intent");
               return null;
        }

         var navData = new Dictionary<string, string> { { "type", type } };

     if (!string.IsNullOrEmpty(sourceType))
   navData["sourceType"] = sourceType;
         if (!string.IsNullOrEmpty(sourceId))
           navData["sourceId"] = sourceId;
          if (!string.IsNullOrEmpty(messageId))
      navData["messageId"] = messageId;
                if (!string.IsNullOrEmpty(eventCode))
             navData["eventCode"] = eventCode;
   if (!string.IsNullOrEmpty(eventId))
         navData["eventId"] = eventId;
     if (!string.IsNullOrEmpty(matchNumber))
         navData["matchNumber"] = matchNumber;

         System.Diagnostics.Debug.WriteLine($"[MainActivity] ✓ Extracted {navData.Count} navigation parameters");
       return navData;
            }
    catch (System.Exception ex)
      {
   System.Diagnostics.Debug.WriteLine($"[MainActivity] ExtractNavigationData error: {ex.Message}");
                return null;
            }
        }

   private void ProcessNotificationIntent(Intent? intent)
      {
 var navData = ExtractNavigationData(intent);
            
       if (navData != null && navData.Count > 0)
   {
                // Store for later processing (cold start scenario)
       var navUri = BuildNavigationUri(navData);
           if (!string.IsNullOrEmpty(navUri))
       {
        lock (_navigationLock)
   {
      _pendingNavigationUri = navUri;
             _hasPendingNavigation = true;
           }

              // Also store in the service for centralized handling
          try
         {
 var services = IPlatformApplication.Current?.Services;
         var navService = services?.GetService<INotificationNavigationService>();
    navService?.SetPendingNavigation(navData);
            }
              catch (System.Exception ex)
  {
          System.Diagnostics.Debug.WriteLine($"[MainActivity] Error setting pending navigation in service: {ex.Message}");
     }

        System.Diagnostics.Debug.WriteLine($"[MainActivity] ✓ Stored pending navigation: {navUri}");
            }
    }
     else
 {
      System.Diagnostics.Debug.WriteLine($"[MainActivity] ✗ No valid notification data found in intent");
    }
 }

   /// <summary>
        /// Build a navigation URI from notification data.
        /// </summary>
        private string? BuildNavigationUri(Dictionary<string, string> data)
        {
if (!data.TryGetValue("type", out var type))
       return null;

       if (type == "chat")
  {
      var queryParams = new List<string>();
        if (data.TryGetValue("sourceType", out var sourceType) && !string.IsNullOrEmpty(sourceType))
          queryParams.Add($"sourceType={System.Uri.EscapeDataString(sourceType)}");
                if (data.TryGetValue("sourceId", out var sourceId) && !string.IsNullOrEmpty(sourceId))
         queryParams.Add($"sourceId={System.Uri.EscapeDataString(sourceId)}");

         var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                return $"//ChatPage{query}";
}
            else if (type == "match")
  {
             if (data.TryGetValue("eventId", out var eventId) && !string.IsNullOrEmpty(eventId))
  {
           var queryParams = new List<string> { $"eventId={eventId}" };
   if (data.TryGetValue("eventCode", out var eventCode) && !string.IsNullOrEmpty(eventCode))
         queryParams.Add($"eventCode={System.Uri.EscapeDataString(eventCode)}");
              if (data.TryGetValue("matchNumber", out var matchNumber) && !string.IsNullOrEmpty(matchNumber))
    queryParams.Add($"matchNumber={matchNumber}");

        return $"//MatchPredictionPage?{string.Join("&", queryParams)}";
 }
      return "//MainPage";
            }

return "//MainPage";
    }

/// <summary>
  /// Trigger immediate navigation when app is already running.
  /// </summary>
        private void TriggerImmediateNavigation(Dictionary<string, string> navData)
   {
      try
            {
                MainThread.BeginInvokeOnMainThread(async () =>
{
      try
    {
        // Small delay to ensure app is ready
           await Task.Delay(300);

           // Try to use the navigation service first
 var services = IPlatformApplication.Current?.Services;
    var navService = services?.GetService<INotificationNavigationService>();

       if (navService != null)
 {
            System.Diagnostics.Debug.WriteLine("[MainActivity] Using NotificationNavigationService for immediate navigation");
  await navService.NavigateFromNotificationAsync(navData);
 }
   else
         {
     // Fallback: direct navigation
            System.Diagnostics.Debug.WriteLine("[MainActivity] NotificationNavigationService not available, using direct navigation");
           var navUri = BuildNavigationUri(navData);
  if (!string.IsNullOrEmpty(navUri) && Shell.Current != null)
             {
       await Shell.Current.GoToAsync(navUri);
           }
   }

    System.Diagnostics.Debug.WriteLine("[MainActivity] ✓ Immediate navigation completed");
          }
  catch (System.Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"[MainActivity] Immediate navigation error: {ex.Message}");
             }
       });
            }
catch (System.Exception ex)
     {
   System.Diagnostics.Debug.WriteLine($"[MainActivity] TriggerImmediateNavigation error: {ex.Message}");
  }
        }

        // Public static method for App.xaml.cs to check and execute pending navigation
        public static bool HasPendingNavigation()
   {
        lock (_navigationLock)
  {
     return _hasPendingNavigation;
        }
        }

public static string? GetPendingNavigationUri()
        {
 lock (_navigationLock)
   {
   return _pendingNavigationUri;
       }
     }

        public static void ClearPendingNavigation()
        {
     lock (_navigationLock)
       {
       _hasPendingNavigation = false;
     _pendingNavigationUri = null;
            }
       System.Diagnostics.Debug.WriteLine("[MainActivity] Cleared pending navigation");
  }

        protected override void OnDestroy()
        {
    try
        {
     _isDestroying = true;
    System.Diagnostics.Debug.WriteLine("[MainActivity] OnDestroy called - activity is being destroyed");

        // Don't stop foreground service - it should continue running
    // The service lifecycle is separate from activity lifecycle

    base.OnDestroy();
   }
         catch (System.Exception ex)
   {
  System.Diagnostics.Debug.WriteLine($"[MainActivity] OnDestroy error (ignored): {ex.Message}");
         // Swallow exception during destruction to prevent crash
            }
        }

      protected override void OnPause()
    {
     try
            {
     System.Diagnostics.Debug.WriteLine("[MainActivity] OnPause - app going to background");
      base.OnPause();
  }
        catch (System.Exception ex)
 {
    System.Diagnostics.Debug.WriteLine($"[MainActivity] OnPause error: {ex.Message}");
    }
}

        protected override void OnStop()
        {
            try
  {
   System.Diagnostics.Debug.WriteLine("[MainActivity] OnStop - app no longer visible");
                base.OnStop();
            }
    catch (System.Exception ex)
        {
       System.Diagnostics.Debug.WriteLine($"[MainActivity] OnStop error: {ex.Message}");
      }
  }

        /// <summary>
        /// Override to handle low memory situations gracefully
        /// </summary>
   public override void OnLowMemory()
      {
 base.OnLowMemory();
            
         try
            {
     System.Diagnostics.Debug.WriteLine("[MainActivity] Low memory warning - triggering GC");
     
        // Force garbage collection to free memory
     GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
      }
   catch (System.Exception ex)
 {
    System.Diagnostics.Debug.WriteLine($"[MainActivity] OnLowMemory error: {ex.Message}");
      }
        }

   /// <summary>
 /// Handle memory trim requests from system
   /// </summary>
        public override void OnTrimMemory([GeneratedEnum] TrimMemory level)
    {
    base.OnTrimMemory(level);
            
  try
   {
   System.Diagnostics.Debug.WriteLine($"[MainActivity] Memory trim requested: {level}");
    
             // Aggressive memory cleanup based on trim level
      if (level >= TrimMemory.RunningCritical)
          {
      // Critical memory pressure - aggressive cleanup
         GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
         GC.WaitForPendingFinalizers();
         GC.Collect();
         }
  else if (level >= TrimMemory.RunningLow)
             {
       // Moderate memory pressure - normal cleanup
    GC.Collect();
      }
   }
     catch (System.Exception ex)
          {
         System.Diagnostics.Debug.WriteLine($"[MainActivity] OnTrimMemory error: {ex.Message}");
            }
        }

  private void RequestBatteryOptimizationExemption()
        {
  try
  {
    if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
     {
    var powerManager = (PowerManager?)GetSystemService(PowerService);
    if (powerManager != null)
             {
     var packageName = PackageName;
      if (!powerManager.IsIgnoringBatteryOptimizations(packageName))
        {
 System.Diagnostics.Debug.WriteLine("[MainActivity] Requesting battery optimization exemption");

     // Show dialog to user explaining why we need this
  var intent = new Intent();
           intent.SetAction(Settings.ActionRequestIgnoreBatteryOptimizations);
  intent.SetData(global::Android.Net.Uri.Parse("package:" + packageName));

     try
           {
 StartActivity(intent);
     }
 catch (System.Exception ex)
{
 System.Diagnostics.Debug.WriteLine($"[MainActivity] Failed to start battery optimization settings: {ex.Message}");
       }
   }
       else
 {
      System.Diagnostics.Debug.WriteLine("[MainActivity] Battery optimization already disabled");
               }
   }
          }
       }
      catch (System.Exception ex)
 {
    System.Diagnostics.Debug.WriteLine($"[MainActivity] Battery optimization request failed: {ex.Message}");
 }
        }
    }
}
