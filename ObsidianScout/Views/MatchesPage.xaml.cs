using ObsidianScout.ViewModels;

namespace ObsidianScout.Views;

public partial class MatchesPage : ContentPage
{
    private readonly MatchesViewModel _viewModel;

    public MatchesPage(MatchesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
