using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Services;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ObsidianScout.ViewModels;

public partial class UserViewModel : ObservableObject
{
 private readonly ISettingsService _settingsService;

 [ObservableProperty]
 private string username = string.Empty;

 [ObservableProperty]
 private string emailAddress = string.Empty;

 [ObservableProperty]
 private int? teamNumber;

 private string _teamDisplay = string.Empty;
 public string TeamDisplay
 {
 get => _teamDisplay;
 set => SetProperty(ref _teamDisplay, value);
 }

 public IAsyncRelayCommand RefreshCommand { get; }

 // DI constructor used by DI container
 public UserViewModel(ISettingsService settingsService)
 {
 _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

 RefreshCommand = new AsyncRelayCommand(LoadAsync);
 _ = LoadAsync();
 }

 // Parameterless constructor for fallback (avoids throwing in activator scenarios)
 public UserViewModel() : this(new SettingsService())
 {
 }

 private async Task LoadAsync()
 {
 try
 {
 var stored = await _settingsService.GetUsernameAsync();
 if (!string.IsNullOrEmpty(stored))
 Username = stored;

 var team = await _settingsService.GetTeamNumberAsync();
 TeamNumber = team;

 // Fallback: if team is null, try to parse from AppShell.CurrentTeamInfo
 if (!TeamNumber.HasValue)
 {
 try
 {
 if (Shell.Current is ObsidianScout.AppShell shell && !string.IsNullOrEmpty(shell.CurrentTeamInfo))
 {
 // expect format like "Team123"
 var m = Regex.Match(shell.CurrentTeamInfo, "(\\d+)");
 if (m.Success && int.TryParse(m.Value, out var parsed))
 {
 TeamNumber = parsed;
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"UserViewModel fallback parse failed: {ex}");
 }
 }

 // Update formatted display — show numeric only
 TeamDisplay = TeamNumber.HasValue ? TeamNumber.Value.ToString() : string.Empty;

 // Email is not stored by default; leave empty unless you store it on login
 EmailAddress = string.Empty;
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"UserViewModel.LoadAsync failed: {ex}");
 }
 }
}
