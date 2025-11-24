using ObsidianScout.ViewModels;
using Microsoft.Maui.Controls;

namespace ObsidianScout.Views;

public partial class RegisterPage : ContentPage
{
 public RegisterPage(RegisterViewModel viewModel)
 {
 InitializeComponent();
 BindingContext = viewModel;
 }
}
