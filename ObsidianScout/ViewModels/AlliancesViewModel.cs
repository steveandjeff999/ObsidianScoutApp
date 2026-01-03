using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace ObsidianScout.ViewModels;

public partial class AlliancesViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;

    public ObservableCollection<Alliance> MyAlliances { get; } = new ObservableCollection<Alliance>();
    public ObservableCollection<PendingInvitation> PendingInvitations { get; } = new ObservableCollection<PendingInvitation>();
    public ObservableCollection<SentInvitation> SentInvitations { get; } = new ObservableCollection<SentInvitation>();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public IAsyncRelayCommand RefreshCommand { get; }

    public AlliancesViewModel(IApiService apiService, ISettingsService settingsService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            MyAlliances.Clear();
            PendingInvitations.Clear();
            SentInvitations.Clear();

            var resp = await _apiService.GetAlliancesAsync();
            if (resp != null && resp.Success)
            {
                if (resp.MyAlliances != null)
                    foreach (var a in resp.MyAlliances)
                        MyAlliances.Add(a);

                if (resp.PendingInvitations != null)
                    foreach (var p in resp.PendingInvitations)
                        PendingInvitations.Add(p);

                if (resp.SentInvitations != null)
                    foreach (var s in resp.SentInvitations)
                        SentInvitations.Add(s);
            }
            else
            {
                ErrorMessage = resp?.Error ?? "Failed to load alliances";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateAllianceAsync()
    {
        var name = await Application.Current.MainPage.DisplayPromptAsync("Create Alliance", "Alliance name:");
        if (string.IsNullOrWhiteSpace(name)) return;
        var desc = await Application.Current.MainPage.DisplayPromptAsync("Create Alliance", "Description (optional):");

        var resp = await _apiService.CreateAllianceAsync(new CreateAllianceRequest { Name = name, Description = desc });
        if (resp != null && resp.Success)
        {
            await LoadAsync();
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", resp?.Error ?? "Failed to create alliance", "OK");
        }
    }

    // Public wrapper for page code-behind to call
    public Task CreateAlliancePublicAsync() => CreateAllianceAsync();

    [RelayCommand]
    private async Task InviteAsync(Alliance alliance)
    {
        if (alliance == null) return;
        var teamStr = await Application.Current.MainPage.DisplayPromptAsync("Invite Team", "Team number to invite:");
        if (!int.TryParse(teamStr, out var teamNumber)) return;
        var message = await Application.Current.MainPage.DisplayPromptAsync("Invite Team", "Optional message:");

        var resp = await _apiService.InviteToAllianceAsync(alliance.Id, new InviteRequest { TeamNumber = teamNumber, Message = message });
        if (resp != null && resp.Success)
        {
            await LoadAsync();
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", resp?.Error ?? "Failed to send invite", "OK");
        }
    }

    public Task InviteToAlliancePublicAsync(Alliance alliance) => InviteAsync(alliance);

    // Removed RelayCommand attribute because signature has two parameters which isn't supported by source generator
    private async Task RespondAsync(PendingInvitation invitation, string response)
    {
        if (invitation == null) return;
        var resp = await _apiService.RespondToInvitationAsync(invitation.Id, new RespondInvitationRequest { Response = response });
        if (resp != null && resp.Success)
        {
            await LoadAsync();
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", resp?.Error ?? "Failed to respond to invitation", "OK");
        }
    }

    public Task RespondToInvitationAsync(PendingInvitation invitation, string response) => RespondAsync(invitation, response);

    // Removed RelayCommand attribute because signature has two parameters which isn't supported by source generator
    private async Task ToggleAllianceAsync(Alliance alliance, bool activate, bool? removeSharedData = null)
    {
        if (alliance == null) return;
        var req = new ToggleAllianceRequest { Activate = activate };
        if (removeSharedData.HasValue) req.RemoveSharedData = removeSharedData.Value;
        var resp = await _apiService.ToggleAllianceAsync(alliance.Id, req);
        if (resp != null && resp.Success)
        {
            await LoadAsync();
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", resp?.Error ?? "Failed to toggle alliance", "OK");
        }
    }

    public Task ToggleAllianceModeAsync(Alliance alliance, bool activate, bool? removeSharedData = null) => ToggleAllianceAsync(alliance, activate, removeSharedData);

    // New RelayCommand wrapper to handle UI prompting and call the underlying toggle method
    [RelayCommand]
    private async Task ToggleAllianceAction(Alliance alliance)
    {
        if (alliance == null) return;

        try
        {
            if (alliance.IsActive)
            {
                // Deactivating - ask whether to remove shared data
                var choice = await Application.Current.MainPage.DisplayActionSheet("Deactivate alliance mode", "Cancel", null,
                    "Deactivate and remove shared data", "Deactivate (keep shared data)");

                if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                    return;

                bool remove = choice == "Deactivate and remove shared data";
                await ToggleAllianceAsync(alliance, false, remove);
            }
            else
            {
                // Activating - check if another alliance is already active
                var otherActive = MyAlliances.FirstOrDefault(a => a.IsActive && a.Id != alliance.Id);
                if (otherActive != null)
                {
                    var confirmSwitch = await Application.Current.MainPage.DisplayAlert("Switch Active Alliance",
                        $"Alliance '{otherActive.Name}' is currently active. Activating '{alliance.Name}' will deactivate '{otherActive.Name}'. Continue?",
                        "Yes", "No");
                    if (!confirmSwitch) return;
                }

                var confirm = await Application.Current.MainPage.DisplayAlert("Activate alliance mode", $"Activate alliance mode for {alliance.Name}?", "Activate", "Cancel");
                if (!confirm) return;

                await ToggleAllianceAsync(alliance, true, null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleAllianceAction error: {ex.Message}");
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to toggle alliance mode", "OK");
        }
    }

    [RelayCommand]
    private async Task LeaveAllianceAsync(Alliance alliance)
    {
        if (alliance == null) return;
        var confirm = await Application.Current.MainPage.DisplayAlert("Leave Alliance", $"Are you sure you want to leave {alliance.Name}?", "Yes", "No");
        if (!confirm) return;
        var resp = await _apiService.LeaveAllianceAsync(alliance.Id);
        if (resp != null && resp.Success)
        {
            await LoadAsync();
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", resp?.Error ?? "Failed to leave alliance", "OK");
        }
    }

    public Task LeaveAlliancePublicAsync(Alliance alliance) => LeaveAllianceAsync(alliance);

    [RelayCommand]
    private async Task EditGameConfigAsync(Alliance alliance)
    {
        if (alliance == null)
        {
            System.Diagnostics.Debug.WriteLine("[AlliancesViewModel] EditGameConfigAsync: alliance is null!");
            return;
        }
        
        try
        {
            System.Diagnostics.Debug.WriteLine("=== AlliancesViewModel: EditGameConfigAsync ===");
            System.Diagnostics.Debug.WriteLine($"Alliance ID: {alliance.Id}");
            System.Diagnostics.Debug.WriteLine($"Alliance Name: {alliance.Name}");
            
            // Use query string parameters instead of Dictionary<string, object>
            // QueryProperty attributes expect string values from the URL
            var encodedName = Uri.EscapeDataString(alliance.Name ?? "");
            var route = $"GameConfigEditorPage?AllianceId={alliance.Id}&AllianceName={encodedName}";
            
            System.Diagnostics.Debug.WriteLine($"Navigating to: {route}");
            await Shell.Current.GoToAsync(route);
            System.Diagnostics.Debug.WriteLine($"? Navigation completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlliancesViewModel] EditGameConfigAsync error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[AlliancesViewModel] Stack trace: {ex.StackTrace}");
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to navigate to config editor", "OK");
        }
    }

    [RelayCommand]
    private async Task EditPitConfigAsync(Alliance alliance)
    {
        if (alliance == null)
        {
            System.Diagnostics.Debug.WriteLine("[AlliancesViewModel] EditPitConfigAsync: alliance is null!");
            return;
        }
        
        try
        {
            System.Diagnostics.Debug.WriteLine("=== AlliancesViewModel: EditPitConfigAsync ===");
            System.Diagnostics.Debug.WriteLine($"Alliance ID: {alliance.Id}");
            System.Diagnostics.Debug.WriteLine($"Alliance Name: {alliance.Name}");
            
            // Use query string parameters instead of Dictionary<string, object>
            var encodedName = Uri.EscapeDataString(alliance.Name ?? "");
            var route = $"PitConfigEditorPage?AllianceId={alliance.Id}&AllianceName={encodedName}";
            
            System.Diagnostics.Debug.WriteLine($"Navigating to: {route}");
            await Shell.Current.GoToAsync(route);
            System.Diagnostics.Debug.WriteLine($"? Navigation completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlliancesViewModel] EditPitConfigAsync error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[AlliancesViewModel] Stack trace: {ex.StackTrace}");
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to navigate to config editor", "OK");
        }
    }
}
