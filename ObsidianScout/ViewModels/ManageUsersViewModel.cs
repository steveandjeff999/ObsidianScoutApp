using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ObsidianScout.ViewModels;

public class ManageUsersViewModel : BindableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;

    public ObservableCollection<User> Users { get; } = new ObservableCollection<User>();
    public ICommand RefreshCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand EditCommand { get; }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public ManageUsersViewModel(IApiService apiService, ISettingsService settingsService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
        RefreshCommand = new Command(async () => await LoadUsersAsync());
        DeleteCommand = new Command<User>(async (u) => await DeleteUserAsync(u));
        EditCommand = new Command<User>(async (u) => await EditUserAsync(u));
        CreateCommand = new Command(async () => await CreateUserAsync());
    }

    public async Task LoadUsersAsync(string? search = null)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            Users.Clear();
            var resp = await _apiService.GetAdminUsersAsync(search);
            if (resp != null && resp.Success && resp.Users != null)
            {
                foreach (var u in resp.Users)
                    Users.Add(u);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ManageUsers: failed to load users: {resp?.Error}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteUserAsync(User? user)
    {
        if (user == null) return;
        var confirm = await Application.Current.MainPage.DisplayAlert("Delete User", $"Are you sure you want to delete {user.Username}?", "Yes", "No");
        if (!confirm) return;
        var resp = await _apiService.DeleteAdminUserAsync(user.Id);
        if (resp != null && resp.Success)
        {
            Users.Remove(user);
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", resp?.Error ?? "Failed to delete user", "OK");
        }
    }

    private async Task EditUserAsync(User? user)
    {
        if (user == null) return;
        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var vm = services?.GetService<ObsidianScout.ViewModels.ManageUserEditViewModel>() ?? new ObsidianScout.ViewModels.ManageUserEditViewModel(services?.GetService<ObsidianScout.Services.IApiService>()!, services?.GetService<ObsidianScout.Services.ISettingsService>()!);
            await vm.LoadAsync(user.Id);
            // Always create the page with the loaded viewmodel to ensure bindings are populated
            var page = new ObsidianScout.Views.ManageUserEditPage(vm);
            await Shell.Current.Navigation.PushAsync(page);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EditUserAsync failed: {ex.Message}");
            await Application.Current.MainPage.DisplayAlert("Error", "Could not open edit page", "OK");
        }
    }

    private async Task CreateUserAsync()
    {
        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var vm = services?.GetService<ObsidianScout.ViewModels.ManageUserCreateViewModel>() ?? new ObsidianScout.ViewModels.ManageUserCreateViewModel(services?.GetService<ObsidianScout.Services.IApiService>()!, services?.GetService<ObsidianScout.Services.ISettingsService>()!);
            var page = new ObsidianScout.Views.ManageUserCreatePage(vm);
            await Shell.Current.Navigation.PushAsync(page);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateUserAsync failed: {ex.Message}");
            await Application.Current.MainPage.DisplayAlert("Error", "Could not open create user page", "OK");
        }
    }
}
