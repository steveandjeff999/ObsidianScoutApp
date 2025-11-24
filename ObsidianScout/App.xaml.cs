using ObsidianScout.Services;

namespace ObsidianScout
{
    public partial class App : Application
    {
        private readonly ISettingsService _settingsService;
        private readonly IDataPreloadService _dataPreloadService;
        private readonly INotificationPollingService? _notificationPollingService;
        private readonly IServiceProvider _services;

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
       _ = InitializeAsync();

          return window;
        }

     private async Task InitializeAsync()
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

      // Check if user is logged in
    var token = await _settingsService.GetTokenAsync();
   var expiration = await _settingsService.GetTokenExpirationAsync();

        if (string.IsNullOrEmpty(token) || expiration == null || expiration < DateTime.UtcNow)
   {
  // Token is missing or expired, go to login
 await Shell.Current.GoToAsync("//LoginPage");
    }
    else
    {
     // Token exists and is valid, preload all data in background
        System.Diagnostics.Debug.WriteLine("[App] User authenticated, triggering data preload");
    _ = Task.Run(async () => await _dataPreloadService.PreloadAllDataAsync());

  // Update auth state
     if (Windows[0].Page is AppShell shell)
  {
       shell.UpdateAuthenticationState(true);
 }

   // Check for pending navigation from notification tap (cold start)
      #if ANDROID
  System.Diagnostics.Debug.WriteLine("[App] Checking for pending navigation in OnStart...");
     await Task.Delay(800); // Give app time to initialize

         if (ObsidianScout.MainActivity.HasPendingNavigation())
          {
        var navUri = ObsidianScout.MainActivity.GetPendingNavigationUri();
     System.Diagnostics.Debug.WriteLine($"[App] Found pending navigation in OnStart: {navUri}");

   if (!string.IsNullOrEmpty(navUri))
    {
    try
   {
        System.Diagnostics.Debug.WriteLine($"[App] Navigating to MainPage first...");
       await Shell.Current.GoToAsync("//MainPage");
       
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

   await Shell.Current.GoToAsync("//MainPage");
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
            
       if (MainActivity.HasPendingNavigation())
   {
var navUri = MainActivity.GetPendingNavigationUri();
        System.Diagnostics.Debug.WriteLine($"[App] Found pending navigation in OnResume: {navUri}");

  if (!string.IsNullOrEmpty(navUri))
            {
 try
         {
         System.Diagnostics.Debug.WriteLine($"[App] Executing pending navigation: {navUri}");
    await Shell.Current.GoToAsync(navUri);
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