using System;
using System.Linq;
using ObsidianScout.Services;
using ObsidianScout.Views;

namespace ObsidianScout
{
    public partial class AppShell : Shell
    {
        private readonly ISettingsService _settingsService;
        private bool _isLoggedIn;
        private bool _hasAnalyticsAccess;

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
            _settingsService = settingsService;

 InitializeComponent();
            BindingContext = this;

 // Register routes for navigation
          Routing.RegisterRoute("TeamsPage", typeof(TeamsPage));
        Routing.RegisterRoute("EventsPage", typeof(EventsPage));
 Routing.RegisterRoute("ScoutingPage", typeof(ScoutingPage));
 Routing.RegisterRoute("TeamDetailsPage", typeof(TeamDetailsPage));
         Routing.RegisterRoute("MatchesPage", typeof(MatchesPage));
     Routing.RegisterRoute("GraphsPage", typeof(GraphsPage));
 // Explicitly register SettingsPage to avoid implicit ShellContent name resolution issues
 Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));

            // Check authentication status
            CheckAuthStatus();
      
            // Listen for navigation to update auth state
            Navigating += OnNavigating;
        }

        private async void CheckAuthStatus()
   {
      try
  {
     var token = await _settingsService.GetTokenAsync();
    var expiration = await _settingsService.GetTokenExpirationAsync();
     
     IsLoggedIn = !string.IsNullOrEmpty(token) && 
  expiration.HasValue && 
       expiration.Value > DateTime.UtcNow;
             
         // Check user roles for analytics access
       if (IsLoggedIn)
   {
 var roles = await _settingsService.GetUserRolesAsync();
        
       // DEBUG: Log roles for troubleshooting
System.Diagnostics.Debug.WriteLine($"DEBUG: Found {roles.Count} roles:");
    foreach (var role in roles)
         {
    System.Diagnostics.Debug.WriteLine($"  - '{role}'");
 }
     
       // Case-insensitive check for multiple role variations
   HasAnalyticsAccess = roles.Any(r => 
     r.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
            r.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) ||
    r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
           r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));
        
    System.Diagnostics.Debug.WriteLine($"DEBUG: HasAnalyticsAccess = {HasAnalyticsAccess}");
      }
    else
 {
  HasAnalyticsAccess = false;
   }
     
  System.Diagnostics.Debug.WriteLine($"Auth Status - IsLoggedIn: {IsLoggedIn}, HasAnalyticsAccess: {HasAnalyticsAccess}");
    }
         catch (Exception ex)
      {
       System.Diagnostics.Debug.WriteLine($"Error checking auth status: {ex.Message}");
 IsLoggedIn = false;
         HasAnalyticsAccess = false;
       }
  }

        private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
{
   // Check authentication before allowing navigation to protected pages
            var target = e.Target.Location.OriginalString;
         
            System.Diagnostics.Debug.WriteLine($"Navigating to: {target}, IsLoggedIn: {IsLoggedIn}");
  
    // Allow navigation to login page
    if (target.Contains("LoginPage", StringComparison.OrdinalIgnoreCase))
    {
      return;
    }
   
       // Block navigation to protected pages if not logged in
     if (!IsLoggedIn && (
   target.Contains("MainPage", StringComparison.OrdinalIgnoreCase) ||
     target.Contains("TeamsPage", StringComparison.OrdinalIgnoreCase) ||
           target.Contains("EventsPage", StringComparison.OrdinalIgnoreCase) ||
      target.Contains("ScoutingPage", StringComparison.OrdinalIgnoreCase) ||
           target.Contains("GraphsPage", StringComparison.OrdinalIgnoreCase)))
       {
    System.Diagnostics.Debug.WriteLine("❌ Navigation blocked - User not logged in");
       e.Cancel();
           
       // Redirect to login
      _ = Shell.Current.DisplayAlertAsync("Authentication Required", 
       "Please log in to access this page.", 
  "OK");
    
       _ = Shell.Current.GoToAsync("//LoginPage");
      return;
      }
            
  // Block navigation to Graphs page if user doesn't have analytics access
      if (target.Contains("GraphsPage", StringComparison.OrdinalIgnoreCase) && !HasAnalyticsAccess)
      {
      System.Diagnostics.Debug.WriteLine("❌ Navigation blocked - User doesn't have analytics access");
                e.Cancel();
           
    _ = Shell.Current.DisplayAlertAsync("Access Denied", 
      "You need analytics privileges to access this page.", 
 "OK");
  }
     }

  public async void UpdateAuthenticationState(bool isLoggedIn)
      {
            IsLoggedIn = isLoggedIn;
    
   // Update analytics access based on roles
            if (IsLoggedIn)
  {
              var roles = await _settingsService.GetUserRolesAsync();
             
    // Case-insensitive check for multiple role variations
   HasAnalyticsAccess = roles.Any(r => 
     r.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
   r.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) ||
      r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
          r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));
   }
         else
     {
         HasAnalyticsAccess = false;
     }
         
    System.Diagnostics.Debug.WriteLine($"✓ Auth state updated: IsLoggedIn = {IsLoggedIn}, HasAnalyticsAccess = {HasAnalyticsAccess}");
     }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
 try
 {
 var confirm = await DisplayAlertAsync("Logout",
 "Are you sure you want to logout?",
 "Yes",
 "No");

 if (!confirm)
 return;

 try
 {
 await _settingsService.ClearAuthDataAsync();
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[AppShell] Failed to clear auth data: {ex}");
 // Inform the user but continue with logout UI flow
 await DisplayAlertAsync("Logout", "Warning: failed to clear some local data. You have been logged out of the app UI.", "OK");
 }

 // Update UI state
 UpdateAuthenticationState(false);

 try
 {
 await GoToAsync("//LoginPage");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[AppShell] Navigation to LoginPage failed: {ex}");
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"[AppShell] Logout failed: {ex}");
 await DisplayAlertAsync("Logout Failed", "An error occurred while logging out. Please try again.", "OK");
 }
 }
    }
}
