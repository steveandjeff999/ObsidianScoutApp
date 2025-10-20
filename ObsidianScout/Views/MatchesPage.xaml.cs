using ObsidianScout.ViewModels;

namespace ObsidianScout.Views;

public partial class MatchesPage : ContentPage
{
    public MatchesPage(MatchesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
