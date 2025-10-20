using ObsidianScout.ViewModels;

namespace ObsidianScout.Views;

public partial class GraphsPage : ContentPage
{
    private readonly GraphsViewModel _viewModel;

    public GraphsPage(GraphsViewModel viewModel)
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
