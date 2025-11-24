using Microsoft.Maui.Controls;
using ObsidianScout.ViewModels;

namespace ObsidianScout.Views;

public partial class ScoutingLandingPage : ContentPage
{
 private readonly ScoutingViewModel _viewModel;

 public ScoutingLandingPage(ScoutingViewModel viewModel)
 {
 _viewModel = viewModel;
 BindingContext = _viewModel;
 InitializeComponent();
 OpenFormButton.Clicked += OpenFormButton_Clicked;
 }

 private async void OpenFormButton_Clicked(object sender, EventArgs e)
 {
 try
 {
 // Navigate to the existing ScoutingPage route
 await Shell.Current.GoToAsync("ScoutingPage");
 }
 catch
 {
 // Fallback: push the page directly if route navigation fails
 var page = new ScoutingPage(_viewModel);
 await Navigation.PushAsync(page);
 }
 }
}
