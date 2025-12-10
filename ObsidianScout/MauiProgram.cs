using Microsoft.Extensions.Logging;
using ObsidianScout.Services;
using ObsidianScout.ViewModels;
using ObsidianScout.Views;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Microcharts.Maui;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using CommunityToolkit.Maui;

#if ANDROID
using Android.App;
using Android.Content;
#endif

namespace ObsidianScout
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
     // Set up global exception handlers FIRST before anything else
            SetupGlobalExceptionHandlers();

      var builder = MauiApp.CreateBuilder();
     builder
        .UseMauiApp<App>()
                .UseSkiaSharp()
        .UseMicrocharts()
        .UseMauiCommunityToolkit()
        .ConfigureFonts(fonts =>
       {
 fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
 fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

 // Icon font (Font Awesome free - add fa-solid-900.ttf to Resources/Fonts)
 fonts.AddFont("fa-solid-900.ttf", "FA");
 });

#if DEBUG
 builder.Logging.AddDebug();
#endif

 // Register Services
 builder.Services.AddSingleton<ISettingsService, SettingsService>();
 builder.Services.AddSingleton<ICacheService, CacheService>();
 builder.Services.AddSingleton<IDataPreloadService, DataPreloadService>();
 builder.Services.AddSingleton<IQRCodeService, QRCodeService>();
 builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
 builder.Services.AddSingleton<IUIThreadingService, UIThreadingService>();
 
 // Register notification navigation service (handles navigation from notification taps)
 builder.Services.AddSingleton<INotificationNavigationService, NotificationNavigationService>();

 // Register platform-specific services
#if ANDROID
 builder.Services.AddSingleton<ILocalNotificationService, Platforms.Android.LocalNotificationService>();
#elif WINDOWS
 builder.Services.AddSingleton<ILocalNotificationService, Platforms.Windows.LocalNotificationService>();
#else
 // For iOS/Mac, register a null implementation or stub
 builder.Services.AddSingleton<ILocalNotificationService>(sp => null!);
#endif

 // Register notification services
 builder.Services.AddSingleton<IBackgroundNotificationService>(sp =>
 {
 var apiService = sp.GetRequiredService<IApiService>();
 var settingsService = sp.GetRequiredService<ISettingsService>();
 var localNotificationService = sp.GetService<ILocalNotificationService>();
 return new BackgroundNotificationService(apiService, settingsService, localNotificationService);
 });

 builder.Services.AddSingleton<INotificationPollingService>(sp =>
 {
 var apiService = sp.GetRequiredService<IApiService>();
 var settingsService = sp.GetRequiredService<ISettingsService>();
 var localNotificationService = sp.GetService<ILocalNotificationService>();
 return new NotificationPollingService(apiService, settingsService, localNotificationService);
 });

 // Configure HttpClient with custom handler for self-signed certificates
 builder.Services.AddSingleton<HttpClient>(sp =>
 {
 var handler = new HttpClientHandler();

#if DEBUG
 // WARNING: Only use this in development/testing
 handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
 {
 // Accept all certificates in debug mode
 return true;
 };
#else
 // Production: Only allow valid certificates or your specific cert
 handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
 {
 // Check for your specific certificate thumbprint
 // Replace with your actual certificate thumbprint
 if (cert != null)
 {
 // For now, accept all in production too (update with your cert thumbprint)
 return true;
 }
 return sslPolicyErrors == SslPolicyErrors.None;
 };
#endif

 var client = new HttpClient(handler);

 // Get timeout from settings (default to 8 seconds if not set or on error)
 int timeoutSeconds = 8;
 try
 {
 var settingsService = sp.GetService<ISettingsService>();
 if (settingsService != null)
 {
 // Use Task.Run to avoid blocking on startup
 timeoutSeconds = Task.Run(async () => await settingsService.GetNetworkTimeoutAsync()).Result;
 System.Diagnostics.Debug.WriteLine($"[MauiProgram] Network timeout loaded from settings: {timeoutSeconds}s");
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[MauiProgram] Failed to load network timeout from settings: {ex.Message}. Using default: {timeoutSeconds}s");
 }

 client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
 System.Diagnostics.Debug.WriteLine($"[MauiProgram] HttpClient timeout set to {timeoutSeconds} seconds");

 return client;
 });

 builder.Services.AddSingleton<IApiService, ApiService>();

 // Register ViewModels
 builder.Services.AddTransient<LoginViewModel>();
 builder.Services.AddTransient<MainViewModel>();
 builder.Services.AddTransient<TeamsViewModel>();
 builder.Services.AddTransient<EventsViewModel>();
 builder.Services.AddTransient<ScoutingViewModel>();
 builder.Services.AddTransient<PitScoutingViewModel>();
 builder.Services.AddTransient<PitScoutingEditViewModel>();
 builder.Services.AddTransient<TeamDetailsViewModel>();
 builder.Services.AddTransient<MatchesViewModel>();
 builder.Services.AddTransient<GraphsViewModel>();
 builder.Services.AddTransient<MatchPredictionViewModel>();
 builder.Services.AddTransient<SettingsViewModel>();
 builder.Services.AddTransient<UserViewModel>();
 builder.Services.AddTransient<DataViewModel>();
 builder.Services.AddTransient<ChatViewModel>();
 builder.Services.AddTransient<ManagementViewModel>();
 builder.Services.AddTransient<GameConfigEditorViewModel>();
 builder.Services.AddTransient<PitConfigEditorViewModel>();
 builder.Services.AddTransient<RegisterViewModel>();
 builder.Services.AddTransient<QRCodeScannerViewModel>();
 builder.Services.AddTransient<ManageUsersViewModel>();
 builder.Services.AddTransient<ManageUserEditViewModel>();
 
 // Register Pages
 builder.Services.AddTransient<LoginPage>();
 builder.Services.AddTransient<MainPage>();
 builder.Services.AddTransient<TeamsPage>();
 builder.Services.AddTransient<EventsPage>();
 builder.Services.AddTransient<ScoutingPage>();
 builder.Services.AddTransient<PitScoutingPage>();
 builder.Services.AddTransient<PitScoutingEditPage>();
 builder.Services.AddTransient<ScoutingLandingPage>();
 builder.Services.AddTransient<TeamDetailsPage>();
 builder.Services.AddTransient<MatchesPage>();
 builder.Services.AddTransient<GraphsPage>();
 builder.Services.AddTransient<MatchPredictionPage>();
 builder.Services.AddTransient<SettingsPage>();
 builder.Services.AddTransient<UserPage>();
 builder.Services.AddTransient<DataPage>();
 builder.Services.AddTransient<ChatPage>();
 builder.Services.AddTransient<ManagementPage>();
 builder.Services.AddTransient<GameConfigEditorPage>();
 builder.Services.AddTransient<PitConfigEditorPage>();
 builder.Services.AddTransient<RegisterPage>();
 builder.Services.AddTransient<QRCodeScannerPage>(); // Register QRCodeScannerPage
 builder.Services.AddTransient<ManageUsersPage>();
 builder.Services.AddTransient<ManageUserEditPage>();

 var app = builder.Build();

#if ANDROID
 try
 {
 var context = Android.App.Application.Context;

 // Use persistent preferences that survive reboots
 ObsidianScout.Platforms.Android.PersistentPreferences.SetAppLaunched(context, true);

 System.Diagnostics.Debug.WriteLine("[MauiProgram] ===== APP LAUNCHED =====");
 System.Diagnostics.Debug.WriteLine("[MauiProgram] App launched flag set - starting ForegroundNotificationService");

 var intent = new Intent(context, typeof(ObsidianScout.Platforms.Android.ForegroundNotificationService));
 context.StartForegroundService(intent);

 System.Diagnostics.Debug.WriteLine("[MauiProgram] ForegroundNotificationService started successfully");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[MauiProgram] Failed to start ForegroundNotificationService: {ex.Message}");
 }
#endif

 // Start notification polling if available
 try
 {
 var notif = app.Services.GetService<INotificationPollingService>();
 notif?.Start();
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"Failed to start notification polling: {ex.Message}");
 }

 return app;
 }

        private static void SetupGlobalExceptionHandlers()
        {
          // Handle exceptions from the current AppDomain
     AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
 {
         var exception = args.ExceptionObject as Exception;
     System.Diagnostics.Debug.WriteLine($"[FATAL] Unhandled AppDomain exception: {exception?.Message}");
   System.Diagnostics.Debug.WriteLine($"[FATAL] Stack trace: {exception?.StackTrace}");
      // Don't rethrow - let the app continue if possible
          };

   // Handle exceptions from background tasks
            TaskScheduler.UnobservedTaskException += (sender, args) =>
  {
        System.Diagnostics.Debug.WriteLine($"[ERROR] Unobserved task exception: {args.Exception?.Message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] Stack trace: {args.Exception?.StackTrace}");
             args.SetObserved(); // Prevent the exception from terminating the process
       };
   }
    }
}
