using System;
using System.IO;
using System.Linq;
using System.Threading;
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

		private bool _isConnectionProblem;
		private string _connectionProblemMessage = string.Empty;

		private bool _showConnectionBanner;
		private bool _showTitleBanner;

		// Periodic health check cancellation and session suppression
		private CancellationTokenSource? _healthCheckCts;
		private bool _suppressBannerForSession = false;

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
				UpdateShowConnectionBanner();
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

		// New connection problem properties
		public bool IsConnectionProblem
		{
			get => _isConnectionProblem;
			set
			{
				_isConnectionProblem = value;
				OnPropertyChanged();
				UpdateShowConnectionBanner();
			}
		}

		public string ConnectionProblemMessage
		{
			get => _connectionProblemMessage;
			set
			{
				_connectionProblemMessage = value;
				OnPropertyChanged();
			}
		}

		// Banner visibility computed: show only when connection problem exists and offline mode is NOT enabled
		public bool ShowConnectionBanner
		{
			get => _showConnectionBanner;
			set
			{
				_showConnectionBanner = value;
				OnPropertyChanged();
			}
		}

		// Title banner visibility: show if either offline mode label or connection banner should be visible
		public bool ShowTitleBanner
		{
			get => _showTitleBanner;
			set
			{
				_showTitleBanner = value;
				OnPropertyChanged();
			}
		}

		private void UpdateShowConnectionBanner()
		{
			// Determine if connection banner should be shown
			ShowConnectionBanner = IsConnectionProblem && !IsOfflineMode && !_suppressBannerForSession;

			// Title area is visible when offline label OR connection banner is visible
			ShowTitleBanner = IsOfflineMode || ShowConnectionBanner;

			// Ensure the Shell's TitleView is removed when there are no banners so the bar does not reserve space
			try
			{
				MainThread.BeginInvokeOnMainThread(() =>
				{
					try
					{
						if (ShowTitleBanner)
						{
							// Restore the TitleView to the XAML container if needed
							Shell.SetTitleView(this, TitleContainer);
						}
						else
						{
							// Remove the TitleView entirely to collapse the title area
							Shell.SetTitleView(this, null);
						}
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"UpdateShowConnectionBanner - TitleView update error: {ex.Message}");
					}
				});
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"UpdateShowConnectionBanner scheduling error: {ex.Message}");
			}
		}

		public AppShell(ISettingsService settingsService, IApiService apiService)
		{
			_settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
			_apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));

			InitializeComponent();
			BindingContext = this;

			// Subscribe to offline mode changes from SettingsService so banner hides when offline enabled elsewhere
			try
			{
				_settingsService.OfflineModeChanged += SettingsService_OfflineModeChanged;
			}
			catch { }

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
			Routing.RegisterRoute("QRCodeScannerPage", typeof(QRCodeScannerPage));
			Routing.RegisterRoute("ManageUsersPage", typeof(ManageUsersPage));

			// NEW: Register Chat route for deep linking from notifications
			Routing.RegisterRoute("Chat", typeof(ChatPage));
			Routing.RegisterRoute("ChatPage", typeof(ChatPage));

			// NEW: Register MatchPredictionPage for deep linking from match notifications
			Routing.RegisterRoute("MatchPredictionPage", typeof(MatchPredictionPage));

			CheckAuthStatus();

			// Load offline mode state
			_ = LoadOfflineModeStateAsync();

			// Perform startup health check immediately (so banner can show at app start)
			_ = StartupHealthCheckAsync();

			// Start periodic health check loop
			_ = StartPeriodicHealthCheckLoopAsync();

			Navigating += OnNavigating;
		}

		// Handler for offline mode changes triggered elsewhere in the app
		private void SettingsService_OfflineModeChanged(object? sender, bool enabled)
		{
			// Ensure UI updates occur on main thread
			MainThread.BeginInvokeOnMainThread(() =>
			{
				IsOfflineMode = enabled;
				if (enabled)
				{
					// Hide connection banner when offline mode is enabled elsewhere
					SetConnectionProblemState(false, null, overrideSuppress: true);
				}
				else
				{
					// When offline mode turned off elsewhere, re-evaluate health immediately
					_ = CheckHealthOnceAsync();
				}
			});
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

				if (!value)
				{
					// If offline mode was turned off by the user, immediately check server
					_ = CheckHealthOnceAsync();
				}
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
						System.Diagnostics.Debug.WriteLine($"[AppShell] Role: '{role}'");
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

			(CurrentUsername, CurrentTeamInfo) = (string.IsNullOrEmpty(username) ? "Unknown User" : username, teamNumber.HasValue ? $"Team {teamNumber.Value}" : string.Empty);

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
			// Get first2 characters for initials
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
						System.Diagnostics.Debug.WriteLine("[AppShell] ✓ ProfilePictureSource created successfully");
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
					System.Diagnostics.Debug.WriteLine($"[AppShell] pictureBytes is null: {pictureBytes == null}");
					if (pictureBytes != null)
					{
						System.Diagnostics.Debug.WriteLine($"[AppShell] pictureBytes length: {pictureBytes.Length}");
					}
					ProfilePictureSource = null;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] ✗ Failed to load profile picture: {ex.Message}");
				System.Diagnostics.Debug.WriteLine($"[AppShell] Exception type: {ex.GetType().Name}");
				System.Diagnostics.Debug.WriteLine($"[AppShell] Stack trace: {ex.StackTrace}");
				ProfilePictureSource = null;
			}
		}

		private bool IsValidImageData(byte[] data)
		{
			if (data == null || data.Length < 4)
				return false;

			// Check for common image file signatures
			// PNG:89504E47
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

			// GIF:474946
			if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
			{
				System.Diagnostics.Debug.WriteLine("[AppShell] Image format: GIF");
				return true;
			}

			// BMP:424D
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

			System.Diagnostics.Debug.WriteLine($"[AppShell] Unknown image format. First4 bytes: {data[0]:X2} {data[1]:X2} {data[2]:X2} {data[3]:X2}");
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
					System.Diagnostics.Debug.WriteLine($"[AppShell] Role: '{role}'");
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

		// New: user clicked Yes on connection banner to enable offline mode
		public async void OnEnableOfflineClicked(object sender, EventArgs e)
		{
			try
			{
				await _settingsService.SetOfflineModeAsync(true);
				IsOfflineMode = true;
				SetConnectionProblemState(false, null, overrideSuppress: true);
				await DisplayAlertAsync("Offline Enabled", "Offline mode enabled. The app will use cached data where available.", "OK");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to enable offline mode: {ex}");
				await DisplayAlertAsync("Error", "Failed to enable offline mode.", "OK");
			}
		}

		// New: user clicked No to dismiss the banner
		public void OnDismissConnectionClicked(object sender, EventArgs e)
		{
			// Suppress the banner for the rest of this app session
			_suppressBannerForSession = true;

			System.Diagnostics.Debug.WriteLine("[AppShell] Banner dismissed for session (suppressBannerForSession=true)");

			SetConnectionProblemState(false);
		}

		// Run a quick health check on startup and show banner if server unreachable
		private async Task StartupHealthCheckAsync()
		{
			try
			{
				// Wait a short moment to avoid racing with other startup tasks
				await Task.Delay(500);

				var result = await _apiService.HealthCheckAsync();
				if (result == null || !result.Success)
				{
					// Only show banner when not explicitly in offline mode
					if (!await _settingsService.GetOfflineModeAsync())
					{
						SetConnectionProblemState(true, "Cannot connect to server. Would you like to enable offline mode to use cached data and speed things up?");
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Startup health check failed: {ex}");
				try
				{
					if (!await _settingsService.GetOfflineModeAsync())
					{
						SetConnectionProblemState(true, "Cannot connect to server. Would you like to enable offline mode to use cached data and speed things up?");
					}
				}
				catch { }
			}
		}

		// Centralized setter that respects session suppression
		private void SetConnectionProblemState(bool isProblem, string? message = null, bool overrideSuppress = false)
		{
			// If user suppressed the banner for this session, do not show it again
			if (isProblem && _suppressBannerForSession && !overrideSuppress)
			{
				// still update the internal flag but don't show the banner
				_isConnectionProblem = true;
				if (message != null) ConnectionProblemMessage = message;
				OnPropertyChanged(nameof(IsConnectionProblem));
				UpdateShowConnectionBanner();
				return;
			}

			// Normal flow: set via property so bindings and update logic run
			if (message != null)
				ConnectionProblemMessage = message;

			IsConnectionProblem = isProblem;
			if (!isProblem)
				ShowConnectionBanner = false;
		}

		// New: periodic health check loop
		private async Task StartPeriodicHealthCheckLoopAsync()
		{
			_healthCheckCts = new CancellationTokenSource();
			var token = _healthCheckCts.Token;

			try
			{
				while (!token.IsCancellationRequested)
				{
					// Wait before next check
					await Task.Delay(TimeSpan.FromSeconds(15), token);

					// Perform the health check
					await CheckHealthOnceAsync();
				}
			}
			catch (TaskCanceledException)
			{
				// Expected on cancellation, no action needed
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Periodic health check error: {ex.Message}");
			}
		}

		// New: check health once, used by both startup and periodic checks
		private async Task CheckHealthOnceAsync()
		{
			try
			{
				// If the user suppressed the banner for this session, don't update connection problem state
				if (_suppressBannerForSession)
				{
					System.Diagnostics.Debug.WriteLine("[AppShell] Health check skipped UI update due to session suppression");
					// Still perform the health call in background (no UI changes) to keep internal state updated,
					// but do not change IsConnectionProblem/ShowConnectionBanner so the banner stays hidden until restart.
					try
					{
						await _apiService.HealthCheckAsync();
					}
					catch { }
					return;
				}

				var result = await _apiService.HealthCheckAsync();
				if (result == null || !result.Success)
				{
					// Show banner if there's a connection problem and we're not in offline mode
					if (!await _settingsService.GetOfflineModeAsync())
					{
						SetConnectionProblemState(true, "Cannot connect to server. Would you like to enable offline mode to use cached data and speed things up?");
					}
				}
				else
				{
					// Hide banner on successful health check
					SetConnectionProblemState(false);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Health check failed: {ex}");
				// Only update banner if the user hasn't suppressed it for this session
				if (!_suppressBannerForSession)
				{
					try
					{
						if (!await _settingsService.GetOfflineModeAsync())
						{
							SetConnectionProblemState(true, "Cannot connect to server. Would you like to enable offline mode to use cached data and speed things up?");
						}
					}
					catch { }
				}
			}
		}

		// Dispose health check when shell is disposed
		protected override void OnHandlerChanged()
		{
			base.OnHandlerChanged();
			// If handler is removed (app shutting down), cancel background checks
			if (Handler == null)
			{
				try { _healthCheckCts?.Cancel(); } catch { }
			}
		}
	}
}
