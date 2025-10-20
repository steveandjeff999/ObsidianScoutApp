using ObsidianScout.ViewModels;

namespace ObsidianScout.Views;

public partial class MatchPredictionPage : ContentPage
{
    private readonly MatchPredictionViewModel _viewModel;

    public MatchPredictionPage(MatchPredictionViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
