using ObsidianScout.ViewModels;

namespace ObsidianScout.Views;

public partial class TeamsPage : ContentPage
{
    private readonly TeamsViewModel _viewModel;

    public TeamsPage(TeamsViewModel viewModel)
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
