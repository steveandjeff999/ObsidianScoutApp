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
		
		// Flag to track if initial navigation has been done
		private bool _initialNavigationComplete = false;
		
		// Flag to prevent multiple initialization
		private bool _isInitializing = false;
		private readonly object _initLock = new object();

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

		public bool ShowConnectionBanner
		{
			get => _showConnectionBanner;
			set
			{
				_showConnectionBanner = value;
				OnPropertyChanged();
			}
		}

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
			ShowConnectionBanner = IsConnectionProblem && !IsOfflineMode && !_suppressBannerForSession;
			ShowTitleBanner = IsOfflineMode || ShowConnectionBanner;

			try
			{
				if (Application.Current is App app)
				{
					app.UpdateBannerState(ShowTitleBanner, IsOfflineMode, ShowConnectionBanner, ConnectionProblemMessage);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"UpdateShowConnectionBanner - App banner update error: {ex.Message}");
			}
		}

		public AppShell(ISettingsService settingsService, IApiService apiService)
		{
			_settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
			_apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));

			InitializeComponent();
			BindingContext = this;

			// Subscribe to offline mode changes with error handling
			try
			{
				_settingsService.OfflineModeChanged += SettingsService_OfflineModeChanged;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] Failed to subscribe to OfflineModeChanged: {ex.Message}");
			}

			// Register routes used by Shell
			RegisterRoutes();

			// CRITICAL: Check auth status synchronously first to set IsLoggedIn before any navigation
			_ = SafeInitializeAuthAndNavigationAsync();

			Navigating += OnNavigating;
		}

		private void RegisterRoutes()
		{
			try
			{
				Routing.RegisterRoute("MainPage", typeof(MainPage));
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
				Routing.RegisterRoute("MenuPage", typeof(Views.MenuPage));
				Routing.RegisterRoute("LoginPage", typeof(LoginPage));
				Routing.RegisterRoute("DataPage", typeof(DataPage));
				Routing.RegisterRoute("Chat", typeof(ChatPage));
				Routing.RegisterRoute("ChatPage", typeof(ChatPage));
				Routing.RegisterRoute("MatchPredictionPage", typeof(MatchPredictionPage));
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] RegisterRoutes error: {ex.Message}");
			}
		}

		/// <summary>
		/// Initialize authentication state and configure navigation BEFORE any UI appears
		/// </summary>
		private async Task SafeInitializeAuthAndNavigationAsync()
		{
			// Prevent concurrent initialization
			lock (_initLock)
			{
				if (_isInitializing)
				{
					System.Diagnostics.Debug.WriteLine("[AppShell] Already initializing, skipping duplicate call");
					return;
				}
				_isInitializing = true;
			}

			try
			{
				await InitializeAuthAndNavigationAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] SafeInitializeAuthAndNavigationAsync error: {ex.Message}");
				// On error, default to logged out state
				IsLoggedIn = false;
				try
				{
					await MainThread.InvokeOnMainThreadAsync(() =>
					{
						ConfigurePlatformNavigation(false);
					});
				}
				catch (Exception innerEx)
				{
					System.Diagnostics.Debug.WriteLine($"[AppShell] Error configuring navigation on error: {innerEx.Message}");
				}
				_initialNavigationComplete = true;
			}
			finally
			{
				lock (_initLock)
				{
					_isInitializing = false;
				}
			}
		}

		private async Task InitializeAuthAndNavigationAsync()
		{
			try
			{
				System.Diagnostics.Debug.WriteLine("[AppShell] InitializeAuthAndNavigationAsync - Starting...");
				
				// Check authentication status from stored token with timeout
				string? token = null;
				DateTime? expiration = null;
				
				try
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
					token = await _settingsService.GetTokenAsync();
					expiration = await _settingsService.GetTokenExpirationAsync();
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[AppShell] Error reading auth data: {ex.Message}");
				}
				
				var isLoggedIn = !string.IsNullOrEmpty(token) && expiration.HasValue && expiration.Value > DateTime.UtcNow;

				System.Diagnostics.Debug.WriteLine($"[AppShell] Token exists: {!string.IsNullOrEmpty(token)}");
				System.Diagnostics.Debug.WriteLine($"[AppShell] Expiration: {expiration}");
				System.Diagnostics.Debug.WriteLine($"[AppShell] Is expired: {expiration.HasValue && expiration.Value <= DateTime.UtcNow}");
				System.Diagnostics.Debug.WriteLine($"[AppShell] IsLoggedIn: {isLoggedIn}");

				// Set login state BEFORE configuring navigation
				IsLoggedIn = isLoggedIn;

				if (isLoggedIn)
				{
					// Load user roles and info with error handling
					try
					{
						var roles = await _settingsService.GetUserRolesAsync() ?? new List<string>();
						System.Diagnostics.Debug.WriteLine($"[AppShell] User has {roles.Count} roles");

						HasAnalyticsAccess = roles.Any(r =>
							r?.Equals("analytics", StringComparison.OrdinalIgnoreCase) == true ||
							r?.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) == true ||
							r?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true ||
							r?.Equals("superadmin", StringComparison.OrdinalIgnoreCase) == true);

						HasManagementAccess = roles.Any(r =>
							r?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true ||
							r?.Equals("superadmin", StringComparison.OrdinalIgnoreCase) == true ||
							r?.Equals("management", StringComparison.OrdinalIgnoreCase) == true ||
							r?.Equals("manager", StringComparison.OrdinalIgnoreCase) == true);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"[AppShell] Error loading roles: {ex.Message}");
						HasAnalyticsAccess = false;
						HasManagementAccess = false;
					}

					await SafeLoadCurrentUserInfoAsync();
				}

				// Load offline mode state with error handling
				try
				{
					IsOfflineMode = await _settingsService.GetOfflineModeAsync();
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[AppShell] Error loading offline mode: {ex.Message}");
					IsOfflineMode = false;
				}

				// Configure platform navigation AFTER auth state is determined
				try
				{
					await MainThread.InvokeOnMainThreadAsync(() =>
					{
						try
						{
							ConfigurePlatformNavigation(isLoggedIn);
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"[AppShell] ConfigurePlatformNavigation error: {ex.Message}");
						}
					});
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[AppShell] MainThread navigation error: {ex.Message}");
				}

				_initialNavigationComplete = true;

				// Start background tasks after navigation is set up - fire and forget
				_ = SafeStartBackgroundTasksAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] InitializeAuthAndNavigationAsync error: {ex.Message}");
				// On error, default to logged out state
				IsLoggedIn = false;
				try
				{
					await MainThread.InvokeOnMainThreadAsync(() =>
					{
						ConfigurePlatformNavigation(false);
					});
				}
				catch { }
				_initialNavigationComplete = true;
			}
		}

		private async Task SafeStartBackgroundTasksAsync()
		{
			try
			{
				await Task.Delay(1000); // Wait for UI to stabilize
				_ = StartupHealthCheckAsync();
				_ = StartPeriodicHealthCheckLoopAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] Error starting background tasks: {ex.Message}");
			}
		}

		private async Task SafeLoadCurrentUserInfoAsync()
		{
			try
			{
				await LoadCurrentUserInfoAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] SafeLoadCurrentUserInfoAsync error: {ex.Message}");
				CurrentUsername = string.Empty;
				CurrentTeamInfo = string.Empty;
				UserInitials = "?";
			}
		}

		/// <summary>
		/// Configure platform-specific navigation based on login state
		/// </summary>
		private void ConfigurePlatformNavigation(bool isLoggedIn)
		{
			System.Diagnostics.Debug.WriteLine($"[AppShell] ConfigurePlatformNavigation - IsLoggedIn: {isLoggedIn}");

			try
			{
#if ANDROID || IOS
				// On mobile platforms, use TabBars
				FlyoutBehavior = FlyoutBehavior.Disabled;

				// Hide all FlyoutItems on mobile
				try
				{
					foreach (var item in this.Items.ToList())
					{
						if (item is FlyoutItem)
						{
							item.IsVisible = false;
						}
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[AppShell] Error hiding flyout items: {ex.Message}");
				}

				var loginTabBar = this.FindByName<TabBar>("LoginTabBar");
				var mobileTabBar = this.FindByName<TabBar>("MobileTabBar");

				if (isLoggedIn)
				{
					// User is logged in - show main TabBar, hide login TabBar
					System.Diagnostics.Debug.WriteLine("[AppShell] Showing MobileTabBar (logged in)");
					
					if (loginTabBar != null)
					{
						loginTabBar.IsVisible = false;
					}
					if (mobileTabBar != null)
					{
						mobileTabBar.IsVisible = true;
						// Navigate to the main TabBar's first tab
						MainThread.BeginInvokeOnMainThread(async () =>
						{
							try
							{
								await Task.Delay(100); // Small delay for shell to be ready
								var shellCurrent = Shell.Current;
								if (shellCurrent != null)
								{
									await shellCurrent.GoToAsync("//MobileTabBar/MainPage");
									System.Diagnostics.Debug.WriteLine("[AppShell] Navigated to MobileTabBar/MainPage");
								}
							}
							catch (Exception ex)
							{
								System.Diagnostics.Debug.WriteLine($"[AppShell] Navigation to MainPage failed: {ex.Message}");
							}
						});
					}
				}
				else
				{
					// User is NOT logged in - show login TabBar, hide main TabBar
					System.Diagnostics.Debug.WriteLine("[AppShell] Showing LoginTabBar (not logged in)");
					
					if (mobileTabBar != null)
					{
						mobileTabBar.IsVisible = false;
					}
					if (loginTabBar != null)
					{
						loginTabBar.IsVisible = true;
						// Navigate to the login TabBar
						MainThread.BeginInvokeOnMainThread(async () =>
						{
							try
							{
								await Task.Delay(100);
								var shellCurrent = Shell.Current;
								if (shellCurrent != null)
								{
									await shellCurrent.GoToAsync("//LoginTabBar/LoginPage");
									System.Diagnostics.Debug.WriteLine("[AppShell] Navigated to LoginTabBar/LoginPage");
								}
							}
							catch (Exception ex)
							{
								System.Diagnostics.Debug.WriteLine($"[AppShell] Navigation to LoginPage failed: {ex.Message}");
							}
						});
					}
				}
#else
				// On desktop (Windows, Mac), use flyout
				var loginTabBar = this.FindByName<TabBar>("LoginTabBar");
				var mobileTabBar = this.FindByName<TabBar>("MobileTabBar");
				
				// Hide both TabBars on desktop - they're for mobile only
				if (loginTabBar != null) loginTabBar.IsVisible = false;
				if (mobileTabBar != null) mobileTabBar.IsVisible = false;

				FlyoutBehavior = FlyoutBehavior.Flyout;
				
				// Navigate to appropriate page based on login state
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					try
					{
						await Task.Delay(150); // Small delay for shell to be ready
						var shellCurrent = Shell.Current;
						if (shellCurrent != null)
						{
							if (isLoggedIn)
							{
								// User is logged in - navigate to MainPage via FlyoutItem
								await shellCurrent.GoToAsync("//MainPage");
								System.Diagnostics.Debug.WriteLine("[AppShell] Windows: Navigated to //MainPage (logged in)");
							}
							else
							{
								// User is NOT logged in - navigate to LoginPage via FlyoutItem
								await shellCurrent.GoToAsync("//LoginPage");
								System.Diagnostics.Debug.WriteLine("[AppShell] Windows: Navigated to //LoginPage (not logged in)");
							}
						}
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"[AppShell] Windows navigation error: {ex.Message}");
					}
				});
#endif
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] ConfigurePlatformNavigation error: {ex.Message}");
			}
		}

		// Handler for offline mode changes triggered elsewhere in the app
		private void SettingsService_OfflineModeChanged(object? sender, bool enabled)
		{
			// Ensure UI updates occur on main thread
			try
			{
				MainThread.BeginInvokeOnMainThread(() =>
				{
					try
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
							_ = SafeCheckHealthOnceAsync();
						}
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"[AppShell] SettingsService_OfflineModeChanged inner error: {ex.Message}");
					}
				});
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] SettingsService_OfflineModeChanged error: {ex.Message}");
			}
		}

		private async Task SafeCheckHealthOnceAsync()
		{
			try
			{
				await CheckHealthOnceAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] SafeCheckHealthOnceAsync error: {ex.Message}");
			}
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

		private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
		{
			// Don't block navigation until initial setup is complete
			if (!_initialNavigationComplete)
				return;

			try
			{
				var target = e.Target?.Location?.OriginalString;
				if (string.IsNullOrEmpty(target))
					return;

				// Allow login and register pages always
				if (target.Contains("LoginPage", StringComparison.OrdinalIgnoreCase) ||
					target.Contains("RegisterPage", StringComparison.OrdinalIgnoreCase) ||
					target.Contains("LoginTabBar", StringComparison.OrdinalIgnoreCase))
					return;

				// Check auth for protected pages
				if (!IsLoggedIn)
				{
#if ANDROID || IOS
					// Allow MenuPage even when not logged in
					if (target.Contains("MenuPage", StringComparison.OrdinalIgnoreCase))
						return;
						
					e.Cancel();
					System.Diagnostics.Debug.WriteLine($"[AppShell] Navigation blocked - user not logged in, redirecting to LoginPage");
					var shellCurrent = Shell.Current;
					if (shellCurrent != null)
					{
						_ = shellCurrent.GoToAsync("//LoginTabBar/LoginPage");
					}
					return;
#else
					if (target.Contains("MainPage", StringComparison.OrdinalIgnoreCase) ||
						target.Contains("TeamsPage", StringComparison.OrdinalIgnoreCase) ||
						target.Contains("EventsPage", StringComparison.OrdinalIgnoreCase) ||
						target.Contains("ScoutingPage", StringComparison.OrdinalIgnoreCase) ||
						target.Contains("GraphsPage", StringComparison.OrdinalIgnoreCase))
					{
						e.Cancel();
						var shellCurrent = Shell.Current;
						if (shellCurrent != null)
						{
							_ = shellCurrent.DisplayAlert("Authentication Required", "Please log in to access this page.", "OK");
							_ = shellCurrent.GoToAsync("//LoginPage");
						}
						return;
					}
#endif
				}

				if (target.Contains("GraphsPage", StringComparison.OrdinalIgnoreCase) && !HasAnalyticsAccess)
				{
					e.Cancel();
					var shellCurrent = Shell.Current;
					if (shellCurrent != null)
					{
						_ = shellCurrent.DisplayAlert("Access Denied", "You need analytics privileges to access this page.", "OK");
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] OnNavigating error: {ex.Message}");
			}
		}

		/// <summary>
		/// Called after successful login to update UI and navigate to main content
		/// </summary>
		public async void UpdateAuthenticationState(bool isLoggedIn)
		{
			System.Diagnostics.Debug.WriteLine($"[AppShell] UpdateAuthenticationState: {isLoggedIn}");
			IsLoggedIn = isLoggedIn;

			try
			{
				if (IsLoggedIn)
				{
					var roles = await _settingsService.GetUserRolesAsync() ?? new List<string>();

					HasAnalyticsAccess = roles.Any(r => 
						r?.Equals("analytics", StringComparison.OrdinalIgnoreCase) == true ||
						r?.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) == true ||
						r?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true ||
						r?.Equals("superadmin", StringComparison.OrdinalIgnoreCase) == true);

					HasManagementAccess = roles.Any(r =>
						r?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true ||
						r?.Equals("superadmin", StringComparison.OrdinalIgnoreCase) == true ||
						r?.Equals("management", StringComparison.OrdinalIgnoreCase) == true ||
						r?.Equals("manager", StringComparison.OrdinalIgnoreCase) == true);

					await LoadCurrentUserInfoAsync();

					OnPropertyChanged(nameof(HasAnalyticsAccess));
					OnPropertyChanged(nameof(HasManagementAccess));

#if ANDROID || IOS
					// Switch to main TabBar after login
					MainThread.BeginInvokeOnMainThread(() =>
					{
						ConfigurePlatformNavigation(true);
					});
#else
					MainThread.BeginInvokeOnMainThread(() =>
					{
						FlyoutIsPresented = false;
					});
#endif
				}
				else
				{
					HasAnalyticsAccess = false;
					HasManagementAccess = false;
					CurrentUsername = string.Empty;
					CurrentTeamInfo = string.Empty;
					UserInitials = "?";
					ProfilePictureSource = null;

#if ANDROID || IOS
					// Switch to login TabBar after logout
					MainThread.BeginInvokeOnMainThread(() =>
					{
						ConfigurePlatformNavigation(false);
					});
#endif
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] UpdateAuthenticationState error: {ex.Message}");
			}

			OnPropertyChanged(nameof(IsLoggedIn));
		}

		private async Task LoadCurrentUserInfoAsync()
		{
			try
			{
				var username = await _settingsService.GetUsernameAsync();
				var teamNumber = await _settingsService.GetTeamNumberAsync();

				(CurrentUsername, CurrentTeamInfo) = (string.IsNullOrEmpty(username) ? "Unknown User" : username, teamNumber.HasValue ? $"Team {teamNumber.Value}" : string.Empty);

				UserInitials = GetInitialsFromUsername(CurrentUsername);

				OnPropertyChanged(nameof(CurrentUsername));
				OnPropertyChanged(nameof(CurrentTeamInfo));

				// Load profile picture in background (don't await)
				_ = LoadProfilePictureAsync();
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
			try
			{
				var parts = username.Trim().Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 2)
				{
					return $"{parts[0][0]}{parts[1][0]}".ToUpper();
				}
				else if (parts.Length == 1 && parts[0].Length >= 2)
				{
					return parts[0].Substring(0, 2).ToUpper();
				}
				else if (parts.Length == 1 && parts[0].Length == 1)
				{
					return parts[0].ToUpper();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] GetInitialsFromUsername error: {ex.Message}");
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
					var isValidImage = IsValidImageData(pictureBytes);
					if (isValidImage)
					{
						ProfilePictureSource = ImageSource.FromStream(() => new MemoryStream(pictureBytes));
						MainThread.BeginInvokeOnMainThread(() =>
						{
							OnPropertyChanged(nameof(ProfilePictureSource));
							OnPropertyChanged(nameof(HasProfilePicture));
						});
					}
					else
					{
						ProfilePictureSource = null;
					}
				}
				else
				{
					ProfilePictureSource = null;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] Failed to load profile picture: {ex.Message}");
				ProfilePictureSource = null;
			}
		}

		private bool IsValidImageData(byte[] data)
		{
			if (data == null || data.Length < 4)
				return false;

			// PNG
			if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
				return true;
			// JPEG
			if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
				return true;
			// GIF
			if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
				return true;
			// BMP
			if (data[0] == 0x42 && data[1] == 0x4D)
			return true;
			// WebP
			if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
				data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
				return true;

			return false;
		}

		private async void OnLogoutClicked(object sender, EventArgs e)
		{
			try
			{
				var confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
				if (!confirm)
					return;

				try
				{
					await _settingsService.ClearAuthDataAsync();
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[AppShell] Failed to clear auth data: {ex}");
				}

				UpdateAuthenticationState(false);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AppShell] Logout failed: {ex}");
				await DisplayAlert("Logout Failed", "An error occurred while logging out. Please try again.", "OK");
			}
		}

		private async void OnUserTapped(object sender, EventArgs e)
		{
			const string route = "UserPage";
			try
			{
				var shellCurrent = Shell.Current;
				if (shellCurrent == null) return;

				try
				{
					await shellCurrent.GoToAsync(route);
					return;
				}
				catch { }

				try
				{
					await shellCurrent.GoToAsync($"//{route}");
					return;
				}
				catch { }

				try
				{
					var services = Application.Current?.Handler?.MauiContext?.Services;
					var vm = services?.GetService<ObsidianScout.ViewModels.UserViewModel>() ?? new ObsidianScout.ViewModels.UserViewModel();
					var page = new UserPage(vm);
					await shellCurrent.Navigation.PushAsync(page);
					return;
				}
				catch { }

				await DisplayAlert("Navigation failed", "Could not open user page.", "OK");
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
				var shellCurrent = Shell.Current;
				if (shellCurrent != null)
				{
					await shellCurrent.GoToAsync("SettingsPage");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to open SettingsPage from footer: {ex}");
			}
		}

		public async void OnEnableOfflineClicked(object? sender, EventArgs e)
		{
			try
			{
				await _settingsService.SetOfflineModeAsync(true);
				IsOfflineMode = true;
				SetConnectionProblemState(false, null, overrideSuppress: true);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to enable offline mode: {ex}");
				await DisplayAlert("Error", "Failed to enable offline mode.", "OK");
			}
		}

		public void OnDismissConnectionClicked(object? sender, EventArgs e)
		{
			_suppressBannerForSession = true;
			SetConnectionProblemState(false);
		}

		private async Task StartupHealthCheckAsync()
		{
			try
			{
				await Task.Delay(500);
				var result = await _apiService.HealthCheckAsync();
				if (result == null || !result.Success)
				{
					if (!await _settingsService.GetOfflineModeAsync())
					{
						SetConnectionProblemState(true, "Cannot connect to server. Would you like to enable offline mode?");
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
						SetConnectionProblemState(true, "Cannot connect to server. Would you like to enable offline mode?");
					}
				}
				catch { }
			}
		}

		private void SetConnectionProblemState(bool isProblem, string? message = null, bool overrideSuppress = false)
		{
			if (isProblem && _suppressBannerForSession && !overrideSuppress)
			{
				_isConnectionProblem = true;
				if (message != null) ConnectionProblemMessage = message;
				OnPropertyChanged(nameof(IsConnectionProblem));
				UpdateShowConnectionBanner();
				return;
			}

			if (message != null)
				ConnectionProblemMessage = message;

			IsConnectionProblem = isProblem;
			if (!isProblem)
				ShowConnectionBanner = false;
		}

		private async Task StartPeriodicHealthCheckLoopAsync()
		{
			_healthCheckCts = new CancellationTokenSource();
			var token = _healthCheckCts.Token;

			try
			{
				while (!token.IsCancellationRequested)
				{
					await Task.Delay(TimeSpan.FromSeconds(15), token);
					await CheckHealthOnceAsync();
				}
			}
			catch (TaskCanceledException) { }
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Periodic health check error: {ex.Message}");
			}
		}

		private async Task CheckHealthOnceAsync()
		{
			try
			{
				if (_suppressBannerForSession)
				{
					try { await _apiService.HealthCheckAsync(); } catch { }
					return;
				}

				var result = await _apiService.HealthCheckAsync();
				if (result == null || !result.Success)
				{
					if (!await _settingsService.GetOfflineModeAsync())
					{
						SetConnectionProblemState(true, "Cannot connect to server. Would you like to enable offline mode?");
					}
				}
				else
				{
					SetConnectionProblemState(false);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Health check failed: {ex}");
				if (!_suppressBannerForSession)
				{
					try
					{
						if (!await _settingsService.GetOfflineModeAsync())
						{
							SetConnectionProblemState(true, "Cannot connect to server. Would you like to enable offline mode?");
						}
					}
					catch { }
				}
			}
		}

		protected override void OnHandlerChanged()
		{
			base.OnHandlerChanged();
			if (Handler == null)
			{
				try { _healthCheckCts?.Cancel(); } catch { }
			}
		}
	}
}
