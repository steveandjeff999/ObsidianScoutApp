using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using ObsidianScout.Services;

namespace ObsidianScout.ViewModels;

public class RegisterViewModel : ObservableObject
{
 private readonly IApiService _apiService;
 private readonly ISettingsService _settingsService;

 public RegisterViewModel(IApiService apiService, ISettingsService settingsService)
 {
 _apiService = apiService;
 _settingsService = settingsService;
 RegisterCommand = new Command(async () => await RegisterAsync());
 }

 private string _username = string.Empty;
 public string Username
 {
 get => _username;
 set => SetProperty(ref _username, value);
 }

 private string _password = string.Empty;
 public string Password
 {
 get => _password;
 set => SetProperty(ref _password, value);
 }

 private string _confirmPassword = string.Empty;
 public string ConfirmPassword
 {
 get => _confirmPassword;
 set => SetProperty(ref _confirmPassword, value);
 }

 private string _teamNumber = string.Empty;
 public string TeamNumber
 {
 get => _teamNumber;
 set => SetProperty(ref _teamNumber, value);
 }

 private string _email = string.Empty;
 public string Email
 {
 get => _email;
 set => SetProperty(ref _email, value);
 }

 private bool _isLoading;
 public bool IsLoading
 {
 get => _isLoading;
 set => SetProperty(ref _isLoading, value);
 }

 private string _errorMessage = string.Empty;
 public string ErrorMessage
 {
 get => _errorMessage;
 set => SetProperty(ref _errorMessage, value);
 }

 public ICommand RegisterCommand { get; }

 private async Task RegisterAsync()
 {
 ErrorMessage = string.Empty;

 if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(TeamNumber))
 {
 ErrorMessage = "Please fill username, password and team number";
 return;
 }

 if (!int.TryParse(TeamNumber, out var tn) || tn <=0)
 {
 ErrorMessage = "Please enter a valid team number";
 return;
 }

 if (!string.IsNullOrEmpty(ConfirmPassword) && ConfirmPassword != Password)
 {
 ErrorMessage = "Password and confirm password do not match";
 return;
 }

 IsLoading = true;
 try
 {
 var resp = await _apiService.RegisterAsync(Username.Trim(), Password, string.IsNullOrWhiteSpace(ConfirmPassword) ? null : ConfirmPassword, tn, string.IsNullOrWhiteSpace(Email) ? null : Email.Trim());
 if (resp != null && resp.Success)
 {
 // Update settings and navigate to main page
 await _settingsService.SetUsernameAsync(resp.User?.Username ?? Username);
 await _settingsService.SetTeamNumberAsync(resp.User?.TeamNumber ?? tn);
 // Notify AppShell of auth change
 if (Shell.Current is AppShell appShell)
 {
 appShell.UpdateAuthenticationState(true);
 }
 await Shell.Current.GoToAsync("//MainPage");
 }
 else
 {
 ErrorMessage = resp?.Error ?? "Registration failed";
 }
 }
 catch (Exception ex)
 {
 ErrorMessage = ex.Message;
 }
 finally
 {
 IsLoading = false;
 }
 }
}
