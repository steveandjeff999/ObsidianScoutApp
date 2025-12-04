using ObsidianScout.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ObsidianScout.Views;

public partial class ManageUserEditPage : ContentPage
{
 private readonly ManageUserEditViewModel _vm;
 public ManageUserEditPage(ManageUserEditViewModel vm)
 {
 InitializeComponent();
 BindingContext = _vm = vm;
 }
}
