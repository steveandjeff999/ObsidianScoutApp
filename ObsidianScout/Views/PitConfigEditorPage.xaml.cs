using ObsidianScout.ViewModels;
using Microsoft.Maui.Controls;

namespace ObsidianScout.Views;

[QueryProperty(nameof(AllianceId), "AllianceId")]
[QueryProperty(nameof(AllianceName), "AllianceName")]
public partial class PitConfigEditorPage : ContentPage
{
    private readonly PitConfigEditorViewModel _viewModel;
    private bool _isInitialized = false;
    private bool _queryPropertiesSet = false;
    
    private int? _allianceId;
    public string? AllianceId 
    { 
        get => _allianceId?.ToString();
        set 
        {
            System.Diagnostics.Debug.WriteLine($"[PitConfigEditorPage] AllianceId property setter called with value: {value}");
            if (int.TryParse(value, out var id))
            {
                _allianceId = id;
                _viewModel.AllianceId = id;
                System.Diagnostics.Debug.WriteLine($"[PitConfigEditorPage] ? AllianceId set to: {id}");
            }
            else if (string.IsNullOrEmpty(value))
            {
                _allianceId = null;
                _viewModel.AllianceId = null;
                System.Diagnostics.Debug.WriteLine($"[PitConfigEditorPage] ? AllianceId cleared (null/empty value)");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PitConfigEditorPage] ? Failed to parse AllianceId from: {value}");
            }
            _queryPropertiesSet = true;
            
            // Trigger load if page is already appearing
            if (_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine($"[PitConfigEditorPage] Query property set after appearing - triggering reload");
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () => await _viewModel.LoadAsync());
            }
        }
    }
    
    public string? AllianceName 
    { 
        get => _viewModel.AllianceName;
        set 
        {
            System.Diagnostics.Debug.WriteLine($"[PitConfigEditorPage] AllianceName property setter called with value: {value}");
            _viewModel.AllianceName = value;
        }
    }

    public PitConfigEditorPage(PitConfigEditorViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        System.Diagnostics.Debug.WriteLine($"[PitConfigEditorPage] OnAppearing - _queryPropertiesSet: {_queryPropertiesSet}, AllianceId: {_allianceId}, AllianceName: {_viewModel.AllianceName}");

        // Wait a brief moment for query properties to be set by the navigation system
        if (!_queryPropertiesSet)
        {
            System.Diagnostics.Debug.WriteLine($"[PitConfigEditorPage] Waiting for query properties to be set...");
            await Task.Delay(50);
        }

        if (!_isInitialized)
        {
            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine($"[PitConfigEditorPage] Initializing with AllianceId: {_allianceId}, AllianceName: {_viewModel.AllianceName}");
            await _viewModel.LoadAsync();
        }
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Reset state when leaving the page so it can be reinitialized on next visit
        _isInitialized = false;
        _queryPropertiesSet = false;
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
