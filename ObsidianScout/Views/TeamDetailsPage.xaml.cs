using ObsidianScout.ViewModels;

namespace ObsidianScout.Views;

public partial class TeamDetailsPage : ContentPage
{
    public TeamDetailsPage(TeamDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
