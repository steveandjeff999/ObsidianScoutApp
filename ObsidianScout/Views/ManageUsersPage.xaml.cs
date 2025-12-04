using ObsidianScout.ViewModels;
using ObsidianScout.Services;
using Microsoft.Maui.Controls;

namespace ObsidianScout.Views;

public partial class ManageUsersPage : ContentPage
{
 private readonly ManageUsersViewModel _vm;
 public ManageUsersPage(ManageUsersViewModel vm)
 {
 InitializeComponent();
 BindingContext = _vm = vm;
 }

 protected override async void OnAppearing()
 {
 base.OnAppearing();
 await _vm.LoadUsersAsync();
 }

 private async void OnSearch(object sender, System.EventArgs e)
 {
 var query = SearchInput.Text;
 await _vm.LoadUsersAsync(query);
 }
}
