using Microsoft.Maui.Controls;
using System;
using ObsidianScout.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ObsidianScout.Views
{
	public partial class MenuPage : ContentPage
	{
		private ISettingsService? _settingsService;

		public MenuPage()
		{
			InitializeComponent();
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();
			
			try
			{
				// Get settings service
				var services = Application.Current?.Handler?.MauiContext?.Services;
				_settingsService = services?.GetService<ISettingsService>();

				// Update management button visibility based on permissions
				if (Shell.Current?.BindingContext is AppShell shell)
				{
					ManagementButton.IsVisible = shell.HasManagementAccess;
					ManageUsersButton.IsVisible = shell.HasAdminAccess;
					
					// Update user info display
					UsernameLabel.Text = !string.IsNullOrEmpty(shell.CurrentUsername) ? shell.CurrentUsername : "User";
					TeamLabel.Text = !string.IsNullOrEmpty(shell.CurrentTeamInfo) ? shell.CurrentTeamInfo : "";
					
					// Load offline mode state
					if (_settingsService != null)
					{
						var isOffline = await _settingsService.GetOfflineModeAsync();
						OfflineModeSwitch.IsToggled = isOffline;
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[MenuPage] OnAppearing error: {ex.Message}");
			}
		}

		private async void OnOfflineModeToggled(object sender, ToggledEventArgs e)
		{
			try
			{
				if (_settingsService != null)
				{
					await _settingsService.SetOfflineModeAsync(e.Value);
					System.Diagnostics.Debug.WriteLine($"[MenuPage] Offline mode set to: {e.Value}");

					// Update AppShell state if available
					if (Shell.Current?.BindingContext is AppShell shell)
					{
						shell.IsOfflineMode = e.Value;
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[MenuPage] Failed to set offline mode: {ex.Message}");
				await DisplayAlert("Error", "Failed to update offline mode setting.", "OK");
				
				// Revert switch on error
				OfflineModeSwitch.IsToggled = !e.Value;
			}
		}

		private async void OnHomeClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("//MainPage");
		}

		private async void OnTeamsClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("TeamsPage");
		}

		private async void OnEventsClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("EventsPage");
		}

		private async void OnMatchesClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("MatchesPage");
		}

		private async void OnQRScannerClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("QRCodeScannerPage");
		}

		private async void OnGraphsClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("GraphsPage");
		}

		private async void OnMatchPredictionClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("MatchPredictionPage");
		}

		private async void OnDataClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("DataPage");
		}

		private async void OnChatClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("ChatPage");
		}

		private async void OnManagementClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("ManagementPage");
		}

		private async void OnManageUsersClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("ManageUsersPage");
		}

		private async void OnSettingsClicked(object sender, EventArgs e)
		{
			await SafeNavigateAsync("SettingsPage");
		}

		private async void OnLogoutClicked(object sender, EventArgs e)
		{
			try
			{
				var confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
				if (!confirm)
					return;

				// Get settings service and clear auth data
				var services = Application.Current?.Handler?.MauiContext?.Services;
				var settingsService = services?.GetService<ISettingsService>();
				
				if (settingsService != null)
				{
					await settingsService.ClearAuthDataAsync();
				}

				// Update AppShell state
				if (Shell.Current?.BindingContext is AppShell shell)
				{
					shell.UpdateAuthenticationState(false);
				}

				// Navigate to login
				await SafeNavigateAsync("//LoginPage");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[MenuPage] Logout error: {ex.Message}");
				await DisplayAlert("Error", "Failed to logout. Please try again.", "OK");
			}
		}

		/// <summary>
		/// Safe navigation that handles route errors gracefully
		/// </summary>
		private async Task SafeNavigateAsync(string route)
		{
			try
			{
				if (Shell.Current == null)
				{
					System.Diagnostics.Debug.WriteLine($"[MenuPage] Shell.Current is null, cannot navigate to {route}");
					return;
				}

				System.Diagnostics.Debug.WriteLine($"[MenuPage] Navigating to: {route}");
				await Shell.Current.GoToAsync(route);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[MenuPage] Navigation to {route} failed: {ex.Message}");
				
				// Try with leading slashes as fallback if not already using them
				if (!route.StartsWith("//"))
				{
					try
					{
						await Shell.Current.GoToAsync($"//{route}");
					}
					catch (Exception ex2)
					{
						System.Diagnostics.Debug.WriteLine($"[MenuPage] Fallback navigation to //{route} also failed: {ex2.Message}");
						await DisplayAlert("Navigation Error", $"Could not navigate to {route}.", "OK");
					}
				}
				else
				{
					await DisplayAlert("Navigation Error", $"Could not navigate to {route}.", "OK");
				}
			}
		}
	}
}
