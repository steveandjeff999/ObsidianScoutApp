using ObsidianScout.Services;
using ObsidianScout.Views.Controls;

namespace ObsidianScout
{
    public partial class App : Application
    {
        private readonly ISettingsService _settingsService;
   private readonly IDataPreloadService _dataPreloadService;
 private readonly INotificationPollingService? _notificationPollingService;
        private readonly INotificationNavigationService? _notificationNavigationService;
        private readonly IServiceProvider _services;

        // Global banner overlay instance
     private ConnectionBannerView? _bannerOverlay;

    public App(IServiceProvider services)
   {
  _services = services ?? throw new ArgumentNullException(nameof(services));

            // Get services from the provider
       _settingsService = services.GetRequiredService<ISettingsService>();
     _dataPreloadService = services.GetRequiredService<IDataPreloadService>();
        _notificationPollingService = services.GetService<INotificationPollingService>();
        _notificationNavigationService = services.GetService<INotificationNavigationService>();

InitializeComponent();

  MainPage = new AppShell(
        services.GetRequiredService<ISettingsService>(),
   services.GetRequiredService<IApiService>()
       );
   }

     protected override Window CreateWindow(IActivationState? activationState)
     {
    var window = base.CreateWindow(activationState);

      // Handle data preload and background services - fire and forget safely
  _ = SafeInitializeThemeAsync();

        return window;
 }

        // Public method to show/hide banner - called by AppShell
        public void UpdateBannerState(bool showBanner, bool isOfflineMode, bool showConnectionProblem, string message)
        {
            if (!MainThread.IsMainThread)
            {
         MainThread.BeginInvokeOnMainThread(() => UpdateBannerState(showBanner, isOfflineMode, showConnectionProblem, message));
         return;
            }

 try
   {
    EnsureBannerOverlayExists();
 if (_bannerOverlay != null)
     {
  _bannerOverlay.ShowBanner = showBanner;
    _bannerOverlay.ShowOfflineBanner = isOfflineMode;
 _bannerOverlay.ShowConnectionProblem = showConnectionProblem && !isOfflineMode;
    _bannerOverlay.Message = message ?? string.Empty;
            }
        }
catch (Exception ex)
   {
 System.Diagnostics.Debug.WriteLine($"[App] UpdateBannerState error: {ex.Message}");
            }
        }

  private void EnsureBannerOverlayExists()
        {
  if (_bannerOverlay != null) return;

    try
            {
         // Create the banner overlay
        _bannerOverlay = new ConnectionBannerView
          {
     ShowBanner = false,
      ShowOfflineBanner = false,
         ShowConnectionProblem = false,
            Message = string.Empty
        };

            // Wire up events
    _bannerOverlay.YesClicked += OnBannerYesClicked;
                _bannerOverlay.NoClicked += OnBannerNoClicked;

                // Add to the current page's layout if possible
       InjectBannerIntoCurrentPage();

    // Subscribe to page changes to re-inject banner
       if (MainPage is Shell shell)
            {
     shell.Navigated += OnShellNavigated;
    }
            }
  catch (Exception ex)
            {
     System.Diagnostics.Debug.WriteLine($"[App] EnsureBannerOverlayExists error: {ex.Message}");
            }
   }

      private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
 {
            // Re-inject banner into the new page after navigation
     MainThread.BeginInvokeOnMainThread(() =>
    {
     try
        {
    InjectBannerIntoCurrentPage();
     }
    catch (Exception ex)
                {
    System.Diagnostics.Debug.WriteLine($"[App] OnShellNavigated banner inject error: {ex.Message}");
             }
            });
     }

private void InjectBannerIntoCurrentPage()
        {
if (_bannerOverlay == null) return;

            try
   {
    // Get the current page from Shell - add null checks
  var shellCurrent = Shell.Current;
  if (shellCurrent == null) return;

      var currentPage = shellCurrent.CurrentPage;
           if (currentPage == null) return;

          // Skip banner injection on LoginPage during startup - it will be added when navigating to proper pages
          var pageTypeName = currentPage.GetType().Name;
          if (pageTypeName == "LoginPage" && !_bannerOverlay.ShowBanner)
          {
              System.Diagnostics.Debug.WriteLine($"[App] Skipping banner injection on LoginPage (not visible yet)");
              return;
          }

          // Remove banner from previous parent if any
    if (_bannerOverlay.Parent is Layout oldLayout)
          {
            try
    {
             oldLayout.Children.Remove(_bannerOverlay);
         }
               catch (Exception ex)
    {
       System.Diagnostics.Debug.WriteLine($"[App] Error removing banner from old layout: {ex.Message}");
   }
                }

           // Only ContentPage has Content property
          if (currentPage is ContentPage contentPage)
   {
              var content = contentPage.Content;
          if (content == null) return;

  if (content is Layout layout && layout is not Grid)
  {
            // Check if banner is already in this layout
            if (!layout.Children.Contains(_bannerOverlay))
      {
      // Insert at position 0 so it appears at top
       layout.Children.Insert(0, _bannerOverlay);
          System.Diagnostics.Debug.WriteLine($"[App] Banner injected into {pageTypeName}");
         }
  }
     else if (content is Grid grid)
             {
    if (!grid.Children.Contains(_bannerOverlay))
     {
          // Add to grid row 0 with high ZIndex
             Grid.SetRow(_bannerOverlay, 0);
    Grid.SetColumnSpan(_bannerOverlay, 99);
    _bannerOverlay.ZIndex = 9999;
 grid.Children.Add(_bannerOverlay);
        System.Diagnostics.Debug.WriteLine($"[App] Banner injected into Grid on {pageTypeName}");
              }
     }
          else if (content is ScrollView scrollView && scrollView.Content is Layout scrollLayout)
        {
     // For ScrollView, we need to wrap content
      if (!scrollLayout.Children.Contains(_bannerOverlay))
         {
   scrollLayout.Children.Insert(0, _bannerOverlay);
           System.Diagnostics.Debug.WriteLine($"[App] Banner injected into ScrollView content on {pageTypeName}");
        }
       }
       else
    {
                // Fallback: wrap the content in a Grid with the banner
      var originalContent = contentPage.Content;
 if (originalContent != null && originalContent != _bannerOverlay)
         {
  var wrapperGrid = new Grid
        {
    RowDefinitions =
             {
      new RowDefinition { Height = GridLength.Auto },
              new RowDefinition { Height = GridLength.Star }
 }
      };

     Grid.SetRow(_bannerOverlay, 0);
              wrapperGrid.Children.Add(_bannerOverlay);

           if (originalContent is View view)
   {
         Grid.SetRow(view, 1);
                  wrapperGrid.Children.Add(view);
                 }

  contentPage.Content = wrapperGrid;
         System.Diagnostics.Debug.WriteLine($"[App] Banner wrapped content on {pageTypeName}");
           }
               }
    }
    else
              {
         System.Diagnostics.Debug.WriteLine($"[App] Current page is not ContentPage: {pageTypeName}");
            }
        }
          catch (Exception ex)
            {
  System.Diagnostics.Debug.WriteLine($"[App] InjectBannerIntoCurrentPage error: {ex.Message}");
   }
        }

        private async void OnBannerYesClicked(object? sender, EventArgs e)
        {
            try
            {
 await _settingsService.SetOfflineModeAsync(true);

     if (MainPage is AppShell shell)
     {
    shell.OnEnableOfflineClicked(sender, e);
     }

    UpdateBannerState(true, true, false, string.Empty);

 var shellCurrent = Shell.Current;
       if (shellCurrent != null)
  {
           await shellCurrent.DisplayAlert("Offline Enabled", "Offline mode enabled. The app will use cached data where available.", "OK");
    }
}
            catch (Exception ex)
            {
           System.Diagnostics.Debug.WriteLine($"[App] OnBannerYesClicked error: {ex.Message}");
     }
        }

        private void OnBannerNoClicked(object? sender, EventArgs e)
     {
   try
         {
    if (MainPage is AppShell shell)
   {
     shell.OnDismissConnectionClicked(sender, e);
}

     UpdateBannerState(false, false, false, string.Empty);
            }
            catch (Exception ex)
        {
                System.Diagnostics.Debug.WriteLine($"[App] OnBannerNoClicked error: {ex.Message}");
         }
   }

        private async Task SafeInitializeThemeAsync()
      {
 try
            {
     await InitializeThemeAsync();
    }
            catch (Exception ex)
 {
       System.Diagnostics.Debug.WriteLine($"[App] SafeInitializeThemeAsync error: {ex.Message}");
 }
        }

        private async Task InitializeThemeAsync()
        {
            try
            {
    var theme = await _settingsService.GetThemeAsync();
     await MainThread.InvokeOnMainThreadAsync(() =>
       {
        UserAppTheme = theme == "Dark" ? AppTheme.Dark : AppTheme.Light;
     });
     System.Diagnostics.Debug.WriteLine($"[App] Theme initialized: {theme}");
            }
            catch (Exception ex)
 {
       System.Diagnostics.Debug.WriteLine($"[App] Failed to initialize theme: {ex.Message}");
           UserAppTheme = AppTheme.Unspecified; // Use system default
          }
}

        protected override async void OnStart()
  {
            base.OnStart();
      System.Diagnostics.Debug.WriteLine("[App] OnStart called");

 try
            {
    // CRITICAL: Give minimal time for Shell to be fully initialized before any navigation
   // The Shell needs time to register routes after it's created
     await Task.Delay(100); // Reduced from 500ms

// Ensure Shell.Current is available
     var shellCurrent = Shell.Current;
  if (shellCurrent == null)
      {
   System.Diagnostics.Debug.WriteLine("[App] Shell.Current is null, waiting longer...");
  await Task.Delay(200); // Reduced from 1000ms
 shellCurrent = Shell.Current;
        }

 if (shellCurrent == null)
  {
   System.Diagnostics.Debug.WriteLine("[App] Shell.Current still null after waiting, skipping navigation");
  return;
    }

     // Check if user is logged in
    var token = await _settingsService.GetTokenAsync();
  var expiration = await _settingsService.GetTokenExpirationAsync();

                if (string.IsNullOrEmpty(token) || expiration == null || expiration < DateTime.UtcNow)
 {
    // Token is missing or expired - the app should already be showing the default page
    // The Shell's LoginPage FlyoutItem has IsVisible bound to IsLoggedOut which should handle this
         System.Diagnostics.Debug.WriteLine("[App] User not logged in - Shell should show login state");

  // Only navigate if Shell is ready and has the route registered
try
     {
  // Try relative navigation first (safer)
  await shellCurrent.GoToAsync("LoginPage");
    }
     catch (Exception navEx)
  {
       System.Diagnostics.Debug.WriteLine($"[App] Navigation to LoginPage failed: {navEx.Message}");
     // Don't crash - the Shell's default state should handle this
     }
       }
     else
      {
      // Token exists and is valid, preload all data in background
     System.Diagnostics.Debug.WriteLine("[App] User authenticated, triggering data preload");
        _ = Task.Run(async () =>
      {
    try
        {
      await _dataPreloadService.PreloadAllDataAsync();
   }
  catch (Exception ex)
        {
       System.Diagnostics.Debug.WriteLine($"[App] Data preload error: {ex.Message}");
     }
     });

     // Update auth state
  if (MainPage is AppShell shell)
  {
      shell.UpdateAuthenticationState(true);
          }

     // Check for pending navigation from notification tap (cold start)
     await HandlePendingNotificationNavigationAsync();
       }
 }
   catch (Exception ex)
   {
  System.Diagnostics.Debug.WriteLine($"[App] OnStart error: {ex.Message}");
            }
    }

        /// <summary>
        /// Handle pending notification navigation from cold start or background resume.
   /// Works on all platforms by using the centralized NotificationNavigationService.
        /// </summary>
        private async Task HandlePendingNotificationNavigationAsync()
        {
         try
   {
    System.Diagnostics.Debug.WriteLine("[App] Checking for pending notification navigation...");
      
         // First check the centralized navigation service
          if (_notificationNavigationService != null && _notificationNavigationService.HasPendingNavigation)
    {
          System.Diagnostics.Debug.WriteLine("[App] Found pending navigation in NotificationNavigationService");
      await Task.Delay(300); // Give app time to initialize
       
  var success = await _notificationNavigationService.TryExecutePendingNavigationAsync();
       if (success)
   {
          System.Diagnostics.Debug.WriteLine("[App] ✓ Pending navigation executed via service");
           return;
             }
     }
                
#if ANDROID
         // Fallback for Android: check MainActivity's static pending navigation
 await HandleAndroidPendingNavigationAsync();
#else
        // For other platforms, just navigate to MainPage
       await SafeNavigateToMainPageAsync();
#endif
            }
     catch (Exception ex)
   {
System.Diagnostics.Debug.WriteLine($"[App] HandlePendingNotificationNavigationAsync error: {ex.Message}");
    await SafeNavigateToMainPageAsync();
            }
        }

#if ANDROID
        private async Task HandleAndroidPendingNavigationAsync()
    {
       try
{
    System.Diagnostics.Debug.WriteLine("[App] Checking for pending navigation in OnStart...");
        await Task.Delay(300); // Give app time to initialize

    var shellCurrent = Shell.Current;
   if (shellCurrent == null)
  {
     System.Diagnostics.Debug.WriteLine("[App] Shell.Current is null in HandleAndroidPendingNavigationAsync");
     return;
    }

        if (ObsidianScout.MainActivity.HasPendingNavigation())
   {
       var navUri = ObsidianScout.MainActivity.GetPendingNavigationUri();
      System.Diagnostics.Debug.WriteLine($"[App] Found pending navigation in OnStart: {navUri}");

      if (!string.IsNullOrEmpty(navUri))
          {
try
     {
       System.Diagnostics.Debug.WriteLine($"[App] Navigating to MainPage first...");
        await shellCurrent.GoToAsync("MainPage");

 // Small delay to ensure MainPage is loaded
    await Task.Delay(500);

        System.Diagnostics.Debug.WriteLine($"[App] Executing pending navigation: {navUri}");
     await shellCurrent.GoToAsync(navUri);
    ObsidianScout.MainActivity.ClearPendingNavigation();
   System.Diagnostics.Debug.WriteLine($"[App] ✓ Pending navigation completed from OnStart");
            return; // Don't navigate to MainPage again
    }
            catch (Exception ex)
   {
  System.Diagnostics.Debug.WriteLine($"[App] Navigation error in OnStart: {ex.Message}");
   ObsidianScout.MainActivity.ClearPendingNavigation();
}
 }
   }

     await SafeNavigateToMainPageAsync();
}
     catch (Exception ex)
         {
            System.Diagnostics.Debug.WriteLine($"[App] HandleAndroidPendingNavigationAsync error: {ex.Message}");
    }
 }
#endif

 private async Task SafeNavigateToMainPageAsync()
        {
    try
    {
      var shellCurrent = Shell.Current;
 if (shellCurrent != null)
         {
     await shellCurrent.GoToAsync("MainPage");
   }
    }
   catch (Exception navEx)
            {
  System.Diagnostics.Debug.WriteLine($"[App] Navigation to MainPage failed: {navEx.Message}");
    }
    }

        protected override async void OnResume()
     {
     base.OnResume();
            System.Diagnostics.Debug.WriteLine("[App] OnResume called");

            // Check for pending notification navigation when app resumes
 try
 {
          await Task.Delay(200); // Small delay for app to be ready
       
      // Try the centralized navigation service first
    if (_notificationNavigationService != null && _notificationNavigationService.HasPendingNavigation)
         {
     System.Diagnostics.Debug.WriteLine("[App] Found pending navigation on resume");
           await _notificationNavigationService.TryExecutePendingNavigationAsync();
        return;
           }
      }
            catch (Exception ex)
      {
            System.Diagnostics.Debug.WriteLine($"[App] OnResume navigation check error: {ex.Message}");
      }

#if ANDROID
     try
    {
// Give app a moment to fully resume
     await Task.Delay(300);

             var shellCurrent = Shell.Current;
      if (shellCurrent == null)
 {
   System.Diagnostics.Debug.WriteLine("[App] Shell.Current is null in OnResume, skipping");
 return;
        }

    if (MainActivity.HasPendingNavigation())
    {
     var navUri = MainActivity.GetPendingNavigationUri();
     System.Diagnostics.Debug.WriteLine($"[App] Found pending navigation in OnResume: {navUri}");

         if (!string.IsNullOrEmpty(navUri))
            {
    try
  {
      // Remove leading // if present for safer navigation
       var safeUri = navUri.TrimStart('/');
   System.Diagnostics.Debug.WriteLine($"[App] Executing pending navigation: {safeUri}");
     await shellCurrent.GoToAsync(safeUri);
          MainActivity.ClearPendingNavigation();
           System.Diagnostics.Debug.WriteLine($"[App] ✓ Navigation completed from OnResume");
        }
    catch (Exception ex)
     {
             System.Diagnostics.Debug.WriteLine($"[App] Navigation error in OnResume: {ex.Message}");
     MainActivity.ClearPendingNavigation();
       }
 }
  }
        else
    {
       System.Diagnostics.Debug.WriteLine("[App] No pending navigation in OnResume");
  }
        }
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[App] Error in OnResume: {ex.Message}");
    }
#endif
   }

   protected override void OnSleep()
  {
            base.OnSleep();
            System.Diagnostics.Debug.WriteLine("[App] App going to sleep");
        }
    }
}