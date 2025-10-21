using ObsidianScout.Services;

namespace ObsidianScout
{
    public partial class App : Application
    {
        private readonly ISettingsService _settingsService;
        private readonly IDataPreloadService _dataPreloadService;

        public App(ISettingsService settingsService, IDataPreloadService dataPreloadService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _dataPreloadService = dataPreloadService;
            
            // Initialize theme before showing UI
            _ = InitializeThemeAsync();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell(_settingsService));
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
                
                // Update auth state and navigate
                if (Windows[0].Page is AppShell shell)
                {
                    shell.UpdateAuthenticationState(true);
                }
                await Shell.Current.GoToAsync("//MainPage");
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            
            // Optionally refresh data when app resumes
            System.Diagnostics.Debug.WriteLine("[App] App resumed, checking for stale data");
            _ = Task.Run(async () =>
            {
                try
                {
                    var token = await _settingsService.GetTokenAsync();
                    if (!string.IsNullOrEmpty(token))
                    {
                        await _dataPreloadService.PreloadAllDataAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Resume preload failed: {ex.Message}");
                }
            });
        }
    }
}