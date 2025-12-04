using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ObsidianScout.Services;
using ObsidianScout.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System;

namespace ObsidianScout.ViewModels;

public class ManageUserEditViewModel : BindableObject
{
 private readonly IApiService _apiService;
 private readonly ISettingsService _settingsService;
 private int _userId;

 public class RoleItem : BindableObject
 {
 private bool _isSelected;
 public string Name { get; set; } = string.Empty;
 public string? Description { get; set; }
 public bool IsSelected
 {
 get => _isSelected;
 set { _isSelected = value; OnPropertyChanged(); }
 }

 // Controls whether this role checkbox should be visible (e.g., superadmin hidden for non-superadmins)
 private bool _isVisible = true;
 public bool IsVisible
 {
 get => _isVisible;
 set { _isVisible = value; OnPropertyChanged(); }
 }
 }

 private string _username = string.Empty;
 public string Username
 {
 get => _username;
 set { _username = value; OnPropertyChanged(); }
 }

 private string? _email;
 public string? Email
 {
 get => _email;
 set { _email = value; OnPropertyChanged(); }
 }

 private string? _password;
 public string? Password
 {
 get => _password;
 set { _password = value; OnPropertyChanged(); }
 }

 public ObservableCollection<RoleItem> Roles { get; } = new ObservableCollection<RoleItem>();

 private bool _isBusy;
 public bool IsBusy
 {
 get => _isBusy;
 set { _isBusy = value; OnPropertyChanged(); }
 }

 // Whether the current authenticated user has the 'superadmin' role
 private bool _isCurrentUserSuperadmin;
 public bool IsCurrentUserSuperadmin
 {
 get => _isCurrentUserSuperadmin;
 private set { _isCurrentUserSuperadmin = value; OnPropertyChanged(); }
 }

 public ICommand SaveCommand { get; }
 public ICommand CancelCommand { get; }

 public ManageUserEditViewModel(IApiService apiService, ISettingsService settingsService)
 {
 _apiService = apiService;
 _settingsService = settingsService;
 SaveCommand = new Command(async () => await SaveAsync());
 CancelCommand = new Command(async () => await CancelAsync());
 }

 public async Task LoadAsync(int userId)
 {
 _userId = userId;
 IsBusy = true;
 try
 {
 // Load whether current user is superadmin from settings (local cached roles)
 try
 {
 var currentRoles = await _settingsService.GetUserRolesAsync();
 IsCurrentUserSuperadmin = currentRoles.Any(r => string.Equals(r, "superadmin", StringComparison.OrdinalIgnoreCase));
 }
 catch
 {
 IsCurrentUserSuperadmin = false;
 }

 // Load user
 var resp = await _apiService.GetAdminUserAsync(userId);
 if (resp != null && resp.Success && resp.User != null)
 {
 Username = resp.User.Username;
 Email = resp.User.Email;
 }
 else
 {
 await Application.Current.MainPage.DisplayAlert("Error", resp?.Error ?? "Failed to load user", "OK");
 }

 // Load available roles
 var rolesResp = await _apiService.GetAdminRolesAsync();
 Roles.Clear();
 if (rolesResp != null && rolesResp.Success && rolesResp.Roles != null)
 {
 var userRoles = resp?.User?.Roles ?? new System.Collections.Generic.List<string>();
 foreach (var r in rolesResp.Roles)
 {
 var item = new RoleItem
 {
 Name = r.Name,
 Description = r.Description,
 IsSelected = userRoles.Contains(r.Name),
 // Only show superadmin toggle to superadmins
 IsVisible = IsCurrentUserSuperadmin || !string.Equals(r.Name, "superadmin", StringComparison.OrdinalIgnoreCase)
 };
 Roles.Add(item);
 }
 }
 }
 finally
 {
 IsBusy = false;
 }
 }

 private async Task SaveAsync()
 {
 if (IsBusy) return;
 IsBusy = true;
 try
 {
 var selectedRoles = Roles.Where(r => r.IsSelected).Select(r => r.Name).ToList();
 var req = new UpdateUserRequest
 {
 Username = Username,
 Email = Email,
 Password = string.IsNullOrWhiteSpace(Password) ? null : Password,
 Roles = selectedRoles
 };
 var resp = await _apiService.UpdateAdminUserAsync(_userId, req);
 if (resp != null && resp.Success)
 {
 await Application.Current.MainPage.DisplayAlert("Success", "User updated", "OK");
 // Close the modal/page
 await Shell.Current.GoToAsync("..", true);
 }
 else
 {
 await Application.Current.MainPage.DisplayAlert("Error", resp?.Error ?? "Failed to update user", "OK");
 }
 }
 finally
 {
 IsBusy = false;
 }
 }

 private async Task CancelAsync()
 {
 await Shell.Current.GoToAsync("..", true);
 }
}
