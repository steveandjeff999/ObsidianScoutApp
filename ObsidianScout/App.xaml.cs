using ObsidianScout.Services;
using ObsidianScout.Views.Controls;

namespace ObsidianScout
{
    public partial class App : Application
    {
        private readonly ISettingsService _settingsService;
    private readonly IDataPreloadService _dataPreloadService;
        private readonly INotificationPollingService? _notificationPollingService;
        private readonly IServiceProvider _services;

        // Global banner overlay instance
        private ConnectionBannerView? _bannerOverlay;

        public App(IServiceProvider services)
{
        _services = services;
        
        // Get services from the provider
            _settingsService = services.GetRequiredService<ISettingsService>();
       _dataPreloadService = services.GetRequiredService<IDataPreloadService>();
      _notificationPollingService = services.GetService<INotificationPollingService>();

    InitializeComponent();

 MainPage = new AppShell(
    services.GetRequiredService<ISettingsService>(),
    services.GetRequiredService<IApiService>()
            );
        }

 protected override Window CreateWindow(IActivationState? activationState)
        {
      var window = base.CreateWindow(activationState);

            // Handle data preload and background services
         _ = InitializeThemeAsync();

            return window;
    }

      // Public method to show/hide banner - called by AppShell
        public void UpdateBannerState(bool showBanner, bool isOfflineMode, bool showConnectionProblem, string message)
 {
            MainThread.BeginInvokeOnMainThread(() =>
    {
   try
          {
   EnsureBannerOverlayExists();
          if (_bannerOverlay != null)
         {
 _bannerOverlay.ShowBanner = showBanner;
          _bannerOverlay.ShowOfflineBanner = isOfflineMode;
   _bannerOverlay.ShowConnectionProblem = showConnectionProblem && !isOfflineMode;
             _bannerOverlay.Message = message;
            }
   }
  catch (Exception ex)
                {
   System.Diagnostics.Debug.WriteLine($"[App] UpdateBannerState error: {ex.Message}");
      }
      });
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
 // Get the current page from Shell
  var currentPage = Shell.Current?.CurrentPage;
  if (currentPage == null) return;

           // Remove banner from previous parent if any
      if (_bannerOverlay.Parent is Layout oldLayout)
   {
        oldLayout.Children.Remove(_bannerOverlay);
                }

    // Only ContentPage has Content property
        if (currentPage is ContentPage contentPage)
 {
    if (contentPage.Content is Layout layout)
{
       // Check if banner is already in this layout
                 if (!layout.Children.Contains(_bannerOverlay))
         {
// Insert at position 0 so it appears at top
       layout.Children.Insert(0, _bannerOverlay);
             System.Diagnostics.Debug.WriteLine($"[App] Banner injected into {currentPage.GetType().Name}");
   }
               }
  else if (contentPage.Content is Grid grid)
        {
                 if (!grid.Children.Contains(_bannerOverlay))
      {
       // Add to grid row 0 with high ZIndex
        Grid.SetRow(_bannerOverlay, 0);
                 Grid.SetColumnSpan(_bannerOverlay, 99);
    _bannerOverlay.ZIndex = 9999;
 grid.Children.Add(_bannerOverlay);
                 System.Diagnostics.Debug.WriteLine($"[App] Banner injected into Grid on {currentPage.GetType().Name}");
      }
          }
         else if (contentPage.Content is ScrollView scrollView && scrollView.Content is Layout scrollLayout)
       {
      // For ScrollView, we need to wrap content
        if (!scrollLayout.Children.Contains(_bannerOverlay))
    {
         scrollLayout.Children.Insert(0, _bannerOverlay);
       System.Diagnostics.Debug.WriteLine($"[App] Banner injected into ScrollView content on {currentPage.GetType().Name}");
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
   System.Diagnostics.Debug.WriteLine($"[App] Banner wrapped content on {currentPage.GetType().Name}");
   }
              }
 }
 else
       {
          System.Diagnostics.Debug.WriteLine($"[App] Current page is not ContentPage: {currentPage.GetType().Name}");
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

     await Shell.Current.DisplayAlert("Offline Enabled", "Offline mode enabled. The app will use cached data where available.", "OK");
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

        private async Task InitializeThemeAsync()
     {
   try
      {
     var theme = await _settingsService.GetThemeAsync();
  UserAppTheme = theme == "Dark" ? AppTheme.Dark : AppTheme.Light;
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
       // CRITICAL: Wait for Shell to be fully initialized before any navigation
                // The Shell needs time to register routes after it's created
    await Task.Delay(500);

              // Ensure Shell.Current is available
       if (Shell.Current == null)
        {
  System.Diagnostics.Debug.WriteLine("[App] Shell.Current is null, waiting longer...");
    await Task.Delay(1000);
     }

    if (Shell.Current == null)
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
  await Shell.Current.GoToAsync("LoginPage");
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
          _ = Task.Run(async () => await _dataPreloadService.PreloadAllDataAsync());

         // Update auth state
  if (MainPage is AppShell shell)
         {
                     shell.UpdateAuthenticationState(true);
      }

        // Check for pending navigation from notification tap (cold start)
#if ANDROID
        System.Diagnostics.Debug.WriteLine("[App] Checking for pending navigation in OnStart...");
           await Task.Delay(300); // Give app time to initialize

             if (ObsidianScout.MainActivity.HasPendingNavigation())
        {
  var navUri = ObsidianScout.MainActivity.GetPendingNavigationUri();
  System.Diagnostics.Debug.WriteLine($"[App] Found pending navigation in OnStart: {navUri}");

 if (!string.IsNullOrEmpty(navUri))
        {
          try
               {
 System.Diagnostics.Debug.WriteLine($"[App] Navigating to MainPage first...");
           await Shell.Current.GoToAsync("MainPage");

         // Small delay to ensure MainPage is loaded
          await Task.Delay(500);

             System.Diagnostics.Debug.WriteLine($"[App] Executing pending navigation: {navUri}");
await Shell.Current.GoToAsync(navUri);
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
#endif

        try
  {
        await Shell.Current.GoToAsync("MainPage");
            }
           catch (Exception navEx)
     {
             System.Diagnostics.Debug.WriteLine($"[App] Navigation to MainPage failed: {navEx.Message}");
      }
       }
            }
    catch (Exception ex)
      {
     System.Diagnostics.Debug.WriteLine($"[App] OnStart error: {ex.Message}");
  }
        }
        protected override async void OnResume()
    {
  base.OnResume();
     System.Diagnostics.Debug.WriteLine("[App] OnResume called");

#if ANDROID
       try
       {
     // Give app a moment to fully resume
   await Task.Delay(300);

       if (Shell.Current == null)
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
    await Shell.Current.GoToAsync(safeUri);
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