using ObsidianScout.ViewModels;
using Microsoft.Maui.Controls;

namespace ObsidianScout.Views;

public partial class DataPage : ContentPage
{
 public DataPage(DataViewModel vm)
 {
 InitializeComponent();
 BindingContext = vm;

 // Trigger initial load when page is created (executes non-blocking async command)
 if (vm != null && vm.LoadAllCommand != null && vm.LoadAllCommand.CanExecute(null))
 {
 vm.LoadAllCommand.Execute(null);
 }
 }
}