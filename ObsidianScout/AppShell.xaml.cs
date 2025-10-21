using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ObsidianScout.Services;
using ObsidianScout.ViewModels;
using ObsidianScout.Views;

namespace ObsidianScout
{
    public partial class AppShell : Shell
    {
        private readonly ISettingsService _settingsService;
        private bool _isLoggedIn;
        private bool _hasAnalyticsAccess;

        public string CurrentUsername { get; set; } = string.Empty;
        public string CurrentTeamInfo { get; set; } = string.Empty;

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                _isLoggedIn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLoggedOut));
            }
        }

        public bool IsLoggedOut => !IsLoggedIn;

        public bool HasAnalyticsAccess
        {
            get => _hasAnalyticsAccess;
            set
            {
                _hasAnalyticsAccess = value;
                OnPropertyChanged();
            }
        }

        public AppShell(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            InitializeComponent();
            BindingContext = this;

            // Register routes used by Shell
            Routing.RegisterRoute("TeamsPage", typeof(TeamsPage));
            Routing.RegisterRoute("EventsPage", typeof(EventsPage));
            Routing.RegisterRoute("ScoutingPage", typeof(ScoutingPage));
            Routing.RegisterRoute("TeamDetailsPage", typeof(TeamDetailsPage));
            Routing.RegisterRoute("MatchesPage", typeof(MatchesPage));
            Routing.RegisterRoute("GraphsPage", typeof(GraphsPage));
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
            Routing.RegisterRoute("UserPage", typeof(UserPage));

            CheckAuthStatus();

            Navigating += OnNavigating;
        }

        private async void CheckAuthStatus()
        {
            try
            {
                var token = await _settingsService.GetTokenAsync();
                var expiration = await _settingsService.GetTokenExpirationAsync();

                IsLoggedIn = !string.IsNullOrEmpty(token) && expiration.HasValue && expiration.Value > DateTime.UtcNow;

                if (IsLoggedIn)
                {
                    var roles = await _settingsService.GetUserRolesAsync();

                    HasAnalyticsAccess = roles.Any(r =>
                        r.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
                        r.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) ||
                        r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                        r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));

                    await LoadCurrentUserInfoAsync();
                }
                else
                {
                    HasAnalyticsAccess = false;
                    CurrentUsername = string.Empty;
                    CurrentTeamInfo = string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking auth status: {ex.Message}");
                IsLoggedIn = false;
                HasAnalyticsAccess = false;
            }
        }

        private async Task LoadCurrentUserInfoAsync()
        {
            try
            {
                var username = await _settingsService.GetUsernameAsync();
                var teamNumber = await _settingsService.GetTeamNumberAsync();

                CurrentUsername = string.IsNullOrEmpty(username) ? "Unknown User" : username;
                CurrentTeamInfo = teamNumber.HasValue ? $"Team {teamNumber.Value}" : string.Empty;

                // Notify bindings
                OnPropertyChanged(nameof(CurrentUsername));
                OnPropertyChanged(nameof(CurrentTeamInfo));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load current user info: {ex}");
                CurrentUsername = string.Empty;
                CurrentTeamInfo = string.Empty;
            }
        }

        private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            var target = e.Target.Location.OriginalString;

            // Allow login page
            if (target.Contains("LoginPage", StringComparison.OrdinalIgnoreCase))
                return;

            if (!IsLoggedIn && (target.Contains("MainPage", StringComparison.OrdinalIgnoreCase) ||
                                target.Contains("TeamsPage", StringComparison.OrdinalIgnoreCase) ||
                                target.Contains("EventsPage", StringComparison.OrdinalIgnoreCase) ||
                                target.Contains("ScoutingPage", StringComparison.OrdinalIgnoreCase) ||
                                target.Contains("GraphsPage", StringComparison.OrdinalIgnoreCase)))
            {
                e.Cancel();
                _ = Shell.Current.DisplayAlertAsync("Authentication Required", "Please log in to access this page.", "OK");
                _ = Shell.Current.GoToAsync("//LoginPage");
                return;
            }

            if (target.Contains("GraphsPage", StringComparison.OrdinalIgnoreCase) && !HasAnalyticsAccess)
            {
                e.Cancel();
                _ = Shell.Current.DisplayAlertAsync("Access Denied", "You need analytics privileges to access this page.", "OK");
            }
        }

        public async void UpdateAuthenticationState(bool isLoggedIn)
        {
            IsLoggedIn = isLoggedIn;

            if (IsLoggedIn)
            {
                var roles = await _settingsService.GetUserRolesAsync();
                HasAnalyticsAccess = roles.Any(r => r.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
                                                   r.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) ||
                                                   r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                                                   r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));

                await LoadCurrentUserInfoAsync();
            }
            else
            {
                HasAnalyticsAccess = false;
                CurrentUsername = string.Empty;
                CurrentTeamInfo = string.Empty;
            }

            OnPropertyChanged(nameof(IsLoggedIn));
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            try
            {
                var confirm = await DisplayAlertAsync("Logout", "Are you sure you want to logout?", "Yes", "No");
                if (!confirm)
                    return;

                try
                {
                    await _settingsService.ClearAuthDataAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppShell] Failed to clear auth data: {ex}");
                    await DisplayAlertAsync("Logout", "Warning: failed to clear some local data. You have been logged out of the app UI.", "OK");
                }

                UpdateAuthenticationState(false);
                await GoToAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] Logout failed: {ex}");
                await DisplayAlertAsync("Logout Failed", "An error occurred while logging out. Please try again.", "OK");
            }
        }

        private async void OnUserTapped(object sender, EventArgs e)
        {
            const string route = "UserPage";
            try
            {
                // Try simple route then absolute route then fallback to PushAsync
                try
                {
                    await Shell.Current.GoToAsync(route);
                    return;
                }
                catch (Exception exRoute)
                {
                    System.Diagnostics.Debug.WriteLine($"GoToAsync(route) failed: {exRoute}");
                }

                try
                {
                    await Shell.Current.GoToAsync($"//{route}");
                    return;
                }
                catch (Exception exAbs)
                {
                    System.Diagnostics.Debug.WriteLine($"GoToAsync(//route) failed: {exAbs}");
                }

                try
                {
                    // Fallback: resolve UserViewModel from DI if available, otherwise create one
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    var vm = services?.GetService<ObsidianScout.ViewModels.UserViewModel>() ?? new ObsidianScout.ViewModels.UserViewModel();
                    var page = new UserPage(vm);
                    await Shell.Current.Navigation.PushAsync(page);
                    return;
                }
                catch (Exception exPush)
                {
                    System.Diagnostics.Debug.WriteLine($"PushAsync fallback failed: {exPush}");
                }

                // If all fails, show an error
                await DisplayAlertAsync("Navigation failed", "Could not open user page.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnUserTapped unexpected error: {ex}");
            }
        }
    }
}
