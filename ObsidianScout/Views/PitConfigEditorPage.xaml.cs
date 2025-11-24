using ObsidianScout.ViewModels;
using Microsoft.Maui.Controls;

namespace ObsidianScout.Views;

public partial class PitConfigEditorPage : ContentPage
{
    private readonly PitConfigEditorViewModel _viewModel;
    private bool _isInitialized = false;

    public PitConfigEditorPage(PitConfigEditorViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            _isInitialized = true;
            await _viewModel.LoadAsync();
        }
    }

    private async void OnShowRawClicked(object sender, EventArgs e)
    {
        // Disable button to prevent double-clicks
        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        await _viewModel.ShowRawAsync();

        if (button != null) button.IsEnabled = true;
    }

    private async void OnShowFormClicked(object sender, EventArgs e)
    {
        // Disable button to prevent double-clicks
        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        var ok = await _viewModel.ShowFormAsync();

        if (!ok)
        {
            await DisplayAlert("Parse Error", "Failed to parse JSON. Please check the JSON syntax.", "OK");
        }

        if (button != null) button.IsEnabled = true;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
