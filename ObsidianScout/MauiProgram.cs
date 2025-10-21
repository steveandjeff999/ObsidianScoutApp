using Microsoft.Extensions.Logging;
using ObsidianScout.Services;
using ObsidianScout.ViewModels;
using ObsidianScout.Views;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Microcharts.Maui;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ObsidianScout
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseMicrocharts()  // Keep Microcharts as fallback
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
            
            // Configure HttpClient with custom handler for self-signed certificates
            builder.Services.AddSingleton<HttpClient>(sp =>
            {
                var handler = new HttpClientHandler();
                
#if DEBUG
                // WARNING: Only use this in development/testing
                // Accept all SSL certificates (including self-signed)
                handler.ServerCertificateCustomValidationCallback = 
                    (sender, cert, chain, sslPolicyErrors) =>
                    {
                        // In debug mode, accept all certificates
                        return true;
                    };
#else
                // In production, you might want to validate specific certificates
                handler.ServerCertificateCustomValidationCallback = 
                    (sender, cert, chain, sslPolicyErrors) =>
                    {
                        if (sslPolicyErrors == SslPolicyErrors.None)
                        {
                            return true;
                        }
                        
                        // You can add specific certificate validation here
                        // For example, check the certificate thumbprint
                        // return cert?.Thumbprint == "YOUR_CERT_THUMBPRINT";
                        
                        // For now, accept all in release too (adjust as needed)
                        return true;
                    };
#endif
                
                return new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
            });
            
            builder.Services.AddSingleton<IApiService, ApiService>();

            // Register ViewModels
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<TeamsViewModel>();
            builder.Services.AddTransient<EventsViewModel>();
            builder.Services.AddTransient<ScoutingViewModel>();
            builder.Services.AddTransient<TeamDetailsViewModel>();
            builder.Services.AddTransient<MatchesViewModel>();
            builder.Services.AddTransient<GraphsViewModel>();
            builder.Services.AddTransient<MatchPredictionViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<UserViewModel>();

            // Register Pages
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<TeamsPage>();
            builder.Services.AddTransient<EventsPage>();
            builder.Services.AddTransient<ScoutingPage>();
            builder.Services.AddTransient<TeamDetailsPage>();
            builder.Services.AddTransient<MatchesPage>();
            builder.Services.AddTransient<GraphsPage>();
            builder.Services.AddTransient<MatchPredictionPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<UserPage>();

            return builder.Build();
        }
    }
}
