using ObsidianScout.ViewModels;
using Microsoft.Maui.Controls;

namespace ObsidianScout.Views;

public partial class ManagementPage : ContentPage
{
 private readonly ManagementViewModel _vm;

 public ManagementPage(ManagementViewModel vm)
 {
 InitializeComponent();
 BindingContext = _vm = vm;
 }

 private async void OnEditGameConfigClicked(object sender, EventArgs e)
 {
 try
 {
 await Shell.Current.GoToAsync("GameConfigEditorPage");
 }
 catch
 {
 // fallback: push page manually
 var services = Application.Current?.Handler?.MauiContext?.Services;
 var editorVm = services?.GetService<ObsidianScout.ViewModels.GameConfigEditorViewModel>() ?? new ObsidianScout.ViewModels.GameConfigEditorViewModel(services?.GetService<ObsidianScout.Services.IApiService>()!, services?.GetService<ObsidianScout.Services.ISettingsService>()!);
 var page = new GameConfigEditorPage(editorVm);
 await Shell.Current.Navigation.PushAsync(page);
 }
 }

 private async void OnManageUsersClicked(object sender, EventArgs e)
 {
 try
 {
 await Shell.Current.GoToAsync("ManageUsersPage");
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"Failed to navigate to ManageUsersPage: {ex.Message}");
 await DisplayAlert("Navigation Error", "Could not open Manage Users page. Please try again.", "OK");
 }
 }
}
