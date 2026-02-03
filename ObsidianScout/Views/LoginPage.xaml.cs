using ObsidianScout.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace ObsidianScout.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Attach Completed handlers in code to avoid XAML method resolution issues
        var username = this.FindByName<Entry>("UsernameEntry");
        var password = this.FindByName<Entry>("PasswordEntry");
        var team = this.FindByName<Entry>("TeamNumberEntry");
        var openWeb = this.FindByName<Button>("OpenWebButton");

        if (username != null)
        {
            // After username, move to team number
            username.Completed += OnUsernameCompleted;
            username.ReturnType = ReturnType.Next;
        }

        if (team != null)
        {
            // After team number, move to password
            team.Completed += OnTeamNumberCompleted;
            team.ReturnType = ReturnType.Next;
        }

        if (password != null)
        {
            // After password, submit login
            password.Completed += OnPasswordCompleted;
            password.ReturnType = ReturnType.Go;
        }

        if (openWeb != null)
        {
            openWeb.Clicked += OnOpenWebClicked;
        }
    }

    // Move focus from username to team number
    public void OnUsernameCompleted(object sender, System.EventArgs e)
    {
        var team = this.FindByName<Entry>("TeamNumberEntry");
        team?.Focus();
    }

    // Move focus from team number to password
    public void OnTeamNumberCompleted(object sender, System.EventArgs e)
    {
        var pwd = this.FindByName<Entry>("PasswordEntry");
        pwd?.Focus();
    }

    // Trigger login when completed on password
    public void OnPasswordCompleted(object sender, System.EventArgs e)
    {
        // Invoke the bound login command if available
        if (BindingContext is ViewModels.LoginViewModel vm)
        {
            var cmd = vm.LoginCommand;
            if (cmd != null && cmd.CanExecute(null))
            {
                cmd.Execute(null);
            }
        }
    }

    // Informational message about visiting the web interface
    public async void OnOpenWebClicked(object? sender, System.EventArgs e)
    {
        await DisplayAlert("Create Account", "To create an account, please visit the web interface for ObsidianScout.", "OK");
    }

    // Navigate to in-app registration page
    public async void OnCreateAccountClicked(object? sender, System.EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("RegisterPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to navigate to RegisterPage: {ex.Message}");
            await DisplayAlert("Error", "Unable to open registration page.", "OK");
        }
    }
}
