using Microsoft.Extensions.DependencyInjection;
using ObsidianScout.ViewModels;
using ObsidianScout.Services;

namespace ObsidianScout.Views;

public partial class UserPage : ContentPage
{
 private readonly UserViewModel _viewModel;

 // Parameterless constructor used by Shell routing / DI
 public UserPage() : this(null)
 {
 }

 public UserPage(UserViewModel? viewModel)
 {
 // Resolve dependencies if viewModel not provided
 if (viewModel == null)
 {
 var services = Application.Current?.Handler?.MauiContext?.Services;
 var settings = services?.GetService<ISettingsService>() ?? new SettingsService();
 _viewModel = new UserViewModel(settings);
 }
 else
 {
 _viewModel = viewModel;
 }

 InitializeComponent();
 BindingContext = _viewModel;
 }
}
