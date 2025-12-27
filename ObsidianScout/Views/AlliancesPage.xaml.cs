using ObsidianScout.ViewModels;
using ObsidianScout.Services;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ObsidianScout.Views;

public partial class AlliancesPage : ContentPage
{
    private readonly AlliancesViewModel _vm;

    public AlliancesPage(AlliancesViewModel vm)
    {
        // InitializeComponent is generated from XAML; call it to wire controls
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private async void OnAccept(object sender, System.EventArgs e)
    {
        var b = sender as Button;
        var invitation = b?.BindingContext as ObsidianScout.Models.PendingInvitation;
        if (invitation != null)
            await _vm.RespondToInvitationAsync(invitation, "accept");
    }

    private async void OnDecline(object sender, System.EventArgs e)
    {
        var b = sender as Button;
        var invitation = b?.BindingContext as ObsidianScout.Models.PendingInvitation;
        if (invitation != null)
            await _vm.RespondToInvitationAsync(invitation, "decline");
    }
}
