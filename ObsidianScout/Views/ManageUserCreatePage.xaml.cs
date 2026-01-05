using Microsoft.Maui.Controls;
using ObsidianScout.ViewModels;

namespace ObsidianScout.Views;

public partial class ManageUserCreatePage : ContentPage
{
    public ManageUserCreatePage(ManageUserCreateViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
