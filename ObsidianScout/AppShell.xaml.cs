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
        private readonly IApiService _apiService;
        private bool _isLoggedIn;
        private bool _hasAnalyticsAccess;
        private bool _hasManagementAccess;
        private bool _isOfflineMode;
        private ImageSource? _profilePictureSource;
        private string _userInitials = "?";

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

        public bool HasManagementAccess
        {
            get => _hasManagementAccess;
            set
            {
                _hasManagementAccess = value;
                OnPropertyChanged();
            }
        }

        public bool IsOfflineMode
        {
            get => _isOfflineMode;
            set
            {
                _isOfflineMode = value;
                OnPropertyChanged();
            }
        }

        public ImageSource? ProfilePictureSource
        {
            get => _profilePictureSource;
            set
            {
                _profilePictureSource = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasProfilePicture));
            }
        }

        public bool HasProfilePicture => ProfilePictureSource != null;

        public string UserInitials
        {
            get => _userInitials;
            set
            {
                _userInitials = value;
                OnPropertyChanged();
            }
        }

        public AppShell(ISettingsService settingsService, IApiService apiService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));

            InitializeComponent();
            BindingContext = this;

            // Register routes used by Shell
            Routing.RegisterRoute("TeamsPage", typeof(TeamsPage));
            Routing.RegisterRoute("EventsPage", typeof(EventsPage));
            Routing.RegisterRoute("ScoutingPage", typeof(ScoutingPage));
            Routing.RegisterRoute("RegisterPage", typeof(RegisterPage));
            Routing.RegisterRoute("PitScoutingPage", typeof(PitScoutingPage));
            Routing.RegisterRoute("PitScoutingEditPage", typeof(PitScoutingEditPage));
            Routing.RegisterRoute("ScoutingLandingPage", typeof(ScoutingLandingPage));
            Routing.RegisterRoute("TeamDetailsPage", typeof(TeamDetailsPage));
            Routing.RegisterRoute("MatchesPage", typeof(MatchesPage));
            Routing.RegisterRoute("GraphsPage", typeof(GraphsPage));
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
            Routing.RegisterRoute("UserPage", typeof(UserPage));
            Routing.RegisterRoute("ManagementPage", typeof(ManagementPage));
            Routing.RegisterRoute("GameConfigEditorPage", typeof(GameConfigEditorPage));
            Routing.RegisterRoute("PitConfigEditorPage", typeof(PitConfigEditorPage));
    
            // NEW: Register Chat route for deep linking from notifications
            Routing.RegisterRoute("Chat", typeof(ChatPage));
            Routing.RegisterRoute("ChatPage", typeof(ChatPage));

            // NEW: Register MatchPredictionPage for deep linking from match notifications
            Routing.RegisterRoute("MatchPredictionPage", typeof(MatchPredictionPage));

            CheckAuthStatus();

            // Load offline mode state
            _ = LoadOfflineModeStateAsync();

            Navigating += OnNavigating;
        }

        private async Task LoadOfflineModeStateAsync()
        {
            try
            {
                IsOfflineMode = await _settingsService.GetOfflineModeAsync();
                OnPropertyChanged(nameof(IsOfflineMode));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load offline mode state: {ex.Message}");
            }
        }

        // Make handler public to satisfy XAML loader
        public async void OnOfflineModeToggled(object sender, ToggledEventArgs e)
        {
            try
            {
                var value = e.Value;
                await _settingsService.SetOfflineModeAsync(value);
                IsOfflineMode = value;
                System.Diagnostics.Debug.WriteLine($"Offline mode set to: {value}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set offline mode: {ex.Message}");
            }
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
        
                    System.Diagnostics.Debug.WriteLine($"[AppShell] CheckAuthStatus: User has {roles.Count} roles");
                    foreach (var role in roles)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppShell]   Role: '{role}'");
                    }

                    HasAnalyticsAccess = roles.Any(r =>
                        r.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
                        r.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) ||
                        r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                        r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));

                    // Management access: admin, superadmin, or management role
                    HasManagementAccess = roles.Any(r =>
                        r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                        r.Equals("superadmin", StringComparison.OrdinalIgnoreCase) ||
                        r.Equals("management", StringComparison.OrdinalIgnoreCase) ||
                        r.Equals("manager", StringComparison.OrdinalIgnoreCase));

                    System.Diagnostics.Debug.WriteLine($"[AppShell] HasAnalyticsAccess: {HasAnalyticsAccess}");
                    System.Diagnostics.Debug.WriteLine($"[AppShell] HasManagementAccess: {HasManagementAccess}");

                    await LoadCurrentUserInfoAsync();
   
                    // Force refresh all property bindings
                    OnPropertyChanged(nameof(HasAnalyticsAccess));
                    OnPropertyChanged(nameof(HasManagementAccess));

         // Force update of FlyoutItems visibility after a short delay to ensure binding context is ready
          await Task.Delay(100);
  MainThread.BeginInvokeOnMainThread(() =>
   {
          try
{
           // Refresh the flyout to update visibility
  FlyoutIsPresented = false;
             System.Diagnostics.Debug.WriteLine($"[AppShell] CheckAuthStatus: Forced flyout refresh - HasManagementAccess={HasManagementAccess}");
            }
   catch (Exception ex)
         {
           System.Diagnostics.Debug.WriteLine($"[AppShell] Flyout refresh error: {ex.Message}");
 }
 });
      }
     else
    {
          HasAnalyticsAccess = false;
        HasManagementAccess = false;
     CurrentUsername = string.Empty;
     CurrentTeamInfo = string.Empty;
      System.Diagnostics.Debug.WriteLine("[AppShell] User not logged in");
    }
            }
      catch (Exception ex)
            {
       System.Diagnostics.Debug.WriteLine($"Error checking auth status: {ex.Message}");
         IsLoggedIn = false;
                HasAnalyticsAccess = false;
             HasManagementAccess = false;
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

                // Compute initials from username
                UserInitials = GetInitialsFromUsername(CurrentUsername);

                // Notify bindings
                OnPropertyChanged(nameof(CurrentUsername));
                OnPropertyChanged(nameof(CurrentTeamInfo));
                
                // Load profile picture
                await LoadProfilePictureAsync();
             }
   catch (Exception ex)
      {
System.Diagnostics.Debug.WriteLine($"Failed to load current user info: {ex}");
  CurrentUsername = string.Empty;
    CurrentTeamInfo = string.Empty;
   UserInitials = "?";
 }
  }

  private string GetInitialsFromUsername(string username)
     {
      if (string.IsNullOrWhiteSpace(username) || username == "Unknown User")
       return "?";

     // Get first 2 characters for initials
    var parts = username.Trim().Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
     if (parts.Length >= 2)
   {
     // First letter of first two parts
     return $"{parts[0][0]}{parts[1][0]}".ToUpper();
      }
   else if (parts.Length == 1 && parts[0].Length >= 2)
    {
      // First two letters of single word
     return parts[0].Substring(0, 2).ToUpper();
}
    else if (parts.Length == 1 && parts[0].Length == 1)
     {
   // Single letter
  return parts[0].ToUpper();
  }
   
            return "?";
        }

        private async Task LoadProfilePictureAsync()
        {
      try
            {
 System.Diagnostics.Debug.WriteLine("[AppShell] Loading profile picture...");
       
          var pictureBytes = await _apiService.GetProfilePictureAsync();
            
    if (pictureBytes != null && pictureBytes.Length > 0)
      {
   System.Diagnostics.Debug.WriteLine($"[AppShell] ✓ Received profile picture: {pictureBytes.Length} bytes");
         
   // Check if bytes look like a valid image (check for common image headers)
 var isValidImage = IsValidImageData(pictureBytes);
  var validationStatus = isValidImage ? "VALID" : "INVALID";
    System.Diagnostics.Debug.WriteLine($"[AppShell] Image validation: {validationStatus}");
  
         if (isValidImage)
   {
      // Create ImageSource from stream
  ProfilePictureSource = ImageSource.FromStream(() => new MemoryStream(pictureBytes));
   System.Diagnostics.Debug.WriteLine($"[AppShell] ✓ ProfilePictureSource created successfully");
       System.Diagnostics.Debug.WriteLine($"[AppShell] HasProfilePicture: {HasProfilePicture}");
  
    // Force UI update on main thread
      MainThread.BeginInvokeOnMainThread(() =>
      {
    OnPropertyChanged(nameof(ProfilePictureSource));
        OnPropertyChanged(nameof(HasProfilePicture));
   System.Diagnostics.Debug.WriteLine("[AppShell] ✓ Profile picture bindings refreshed");
      });
}
        else
  {
     System.Diagnostics.Debug.WriteLine("[AppShell] ✗ Image data validation failed - not a valid image format");
         ProfilePictureSource = null;
        }
     }
 else
  {
      System.Diagnostics.Debug.WriteLine($"[AppShell] ✗ No profile picture data received (null or empty)");
  System.Diagnostics.Debug.WriteLine($"[AppShell]    pictureBytes is null: {pictureBytes == null}");
           if (pictureBytes != null)
     {
  System.Diagnostics.Debug.WriteLine($"[AppShell]    pictureBytes length: {pictureBytes.Length}");
       }
    ProfilePictureSource = null;
         }
    }
  catch (Exception ex)
            {
     System.Diagnostics.Debug.WriteLine($"[AppShell] ✗ Failed to load profile picture: {ex.Message}");
System.Diagnostics.Debug.WriteLine($"[AppShell]    Exception type: {ex.GetType().Name}");
      System.Diagnostics.Debug.WriteLine($"[AppShell]    Stack trace: {ex.StackTrace}");
    ProfilePictureSource = null;
            }
        }

        private bool IsValidImageData(byte[] data)
        {
   if (data == null || data.Length < 4)
      return false;

            // Check for common image file signatures
 // PNG: 89 50 4E 47
       if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
       {
      System.Diagnostics.Debug.WriteLine("[AppShell] Image format: PNG");
       return true;
            }

            // JPEG: FF D8 FF
      if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
  {
 System.Diagnostics.Debug.WriteLine("[AppShell] Image format: JPEG");
            return true;
            }

    // GIF: 47 49 46
   if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
    {
       System.Diagnostics.Debug.WriteLine("[AppShell] Image format: GIF");
              return true;
            }

// BMP: 42 4D
   if (data[0] == 0x42 && data[1] == 0x4D)
       {
     System.Diagnostics.Debug.WriteLine("[AppShell] Image format: BMP");
    return true;
            }

      // WebP: starts with RIFF....WEBP
 if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
           data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
     {
                System.Diagnostics.Debug.WriteLine("[AppShell] Image format: WebP");
           return true;
   }

     System.Diagnostics.Debug.WriteLine($"[AppShell] Unknown image format. First 4 bytes: {data[0]:X2} {data[1]:X2} {data[2]:X2} {data[3]:X2}");
         return false;
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
                
                System.Diagnostics.Debug.WriteLine($"[AppShell] UpdateAuthenticationState: User has {roles.Count} roles");
                foreach (var role in roles)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppShell]   Role: '{role}'");
                }

                HasAnalyticsAccess = roles.Any(r => r.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
                                                   r.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) ||
                                                   r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                                                   r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));

                // Management access: admin, superadmin, or management role
                HasManagementAccess = roles.Any(r =>
                    r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                    r.Equals("superadmin", StringComparison.OrdinalIgnoreCase) ||
                    r.Equals("management", StringComparison.OrdinalIgnoreCase) ||
                    r.Equals("manager", StringComparison.OrdinalIgnoreCase));

                System.Diagnostics.Debug.WriteLine($"[AppShell] HasAnalyticsAccess: {HasAnalyticsAccess}");
                System.Diagnostics.Debug.WriteLine($"[AppShell] HasManagementAccess: {HasManagementAccess}");

                await LoadCurrentUserInfoAsync();
      
                // Force refresh all property bindings
                OnPropertyChanged(nameof(HasAnalyticsAccess));
                OnPropertyChanged(nameof(HasManagementAccess));
               
                // Force update of FlyoutItems visibility
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        // Refresh the flyout to update visibility
                        FlyoutIsPresented = false;
                        System.Diagnostics.Debug.WriteLine($"[AppShell] Forced flyout refresh - HasManagementAccess={HasManagementAccess}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppShell] Flyout refresh error: {ex.Message}");
                    }
                });
            }
            else
            {
                HasAnalyticsAccess = false;
                HasManagementAccess = false;
                CurrentUsername = string.Empty;
                CurrentTeamInfo = string.Empty;
                UserInitials = "?";
                ProfilePictureSource = null; // Clear profile picture on logout
                System.Diagnostics.Debug.WriteLine("[AppShell] UpdateAuthenticationState: Logged out, cleared profile picture");
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

        private async void OnFooterSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                // Navigate to the shared SettingsPage route
                await Shell.Current.GoToAsync("SettingsPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open SettingsPage from footer: {ex}");
            }
        }
    }
}
