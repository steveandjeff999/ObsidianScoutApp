using ObsidianScout.ViewModels;
using Microsoft.Maui.Controls;
using System.Windows.Input;

namespace ObsidianScout.Views;

public partial class DataPage : ContentPage
{
 private bool _hasLoadedOnce = false;

 public DataPage(DataViewModel vm)
 {
 InitializeComponent();
 BindingContext = vm;

 // Don't auto-load in constructor to avoid freezing during navigation.
 // Manual load is now required (via UI command/button) — no automatic load on appear.
 }

 protected override void OnAppearing()
 {
 base.OnAppearing();

 // Auto-loading disabled per user request. Keep _hasLoadedOnce for potential future use.
 // If you want to trigger a load manually, call the DataViewModel.LoadAllCommand from the UI.
 _hasLoadedOnce = true;
 }
}