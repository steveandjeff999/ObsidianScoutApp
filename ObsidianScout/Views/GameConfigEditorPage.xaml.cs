using ObsidianScout.ViewModels;
using Microsoft.Maui.Controls;

namespace ObsidianScout.Views;

public partial class GameConfigEditorPage : ContentPage
{
    private readonly GameConfigEditorViewModel _vm;
    private bool _isInitialized = false;

    public GameConfigEditorPage(GameConfigEditorViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            _isInitialized = true;
            await _vm.LoadAsync();
        }

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var rawBtn = this.FindByName<Button>("RawButton");
        var formBtn = this.FindByName<Button>("FormButton");

        if (rawBtn != null)
        {
            rawBtn.BackgroundColor = _vm.IsRawVisible ? Colors.Purple : Colors.Gray;
        }

        if (formBtn != null)
        {
            formBtn.BackgroundColor = _vm.IsFormVisible ? Colors.Purple : Colors.Gray;
        }
    }

    private async void OnShowRawClicked(object sender, EventArgs e)
    {
        // Disable button to prevent double-clicks
        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        await _vm.ShowRawAsync();

        var raw = this.FindByName<ScrollView>("RawScroll");
        var form = this.FindByName<ScrollView>("FormScroll");

        if (raw != null) raw.IsVisible = true;
        if (form != null) form.IsVisible = false;

        UpdateButtonStates();

        if (button != null) button.IsEnabled = true;
    }

    private async void OnShowFormClicked(object sender, EventArgs e)
    {
        // Disable button to prevent double-clicks
        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        var ok = await _vm.ShowFormAsync();

        var raw = this.FindByName<ScrollView>("RawScroll");
        var form = this.FindByName<ScrollView>("FormScroll");

        if (!ok)
        {
            // Failed to parse - stay on raw view
            if (raw != null) raw.IsVisible = true;
            if (form != null) form.IsVisible = false;
            await DisplayAlert("Parse Error", "Failed to parse JSON. Please check the JSON syntax.", "OK");
        }
        else
        {
            // Success - switch to form view
            if (raw != null) raw.IsVisible = false;
            if (form != null) form.IsVisible = true;

            // Force picker refresh after a brief delay to let UI settle
            await Task.Delay(100);
            ForcePickerRefresh();
        }

        UpdateButtonStates();

        if (button != null) button.IsEnabled = true;
    }

    /// <summary>
    /// Force all Picker controls to refresh their SelectedItem binding
    /// Optimized version with reduced overhead
    /// </summary>
    private async void ForcePickerRefresh()
    {
        try
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("[GameConfigEditorPage] Forcing picker refresh...");
#endif

            // Run refresh in background to avoid UI blocking
            await Task.Run(() =>
            {
                // Batch updates to reduce UI overhead
                var allElements = _vm.AutoElements
                    .Concat(_vm.TeleopElements)
                    .Concat(_vm.EndgameElements)
                    .ToList();

                foreach (var element in allElements)
                {
                    // Only refresh if type is actually set
                    if (!string.IsNullOrEmpty(element.Type))
                    {
                        // This triggers OnPropertyChanged on UI thread
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var temp = element.Type;
                            element.Type = temp;
                        });
                    }
                }
            });

#if DEBUG
            await Task.Delay(50); // Brief delay for logging
            var count = _vm.AutoElements.Count + _vm.TeleopElements.Count + _vm.EndgameElements.Count;
            System.Diagnostics.Debug.WriteLine($"[GameConfigEditorPage] Refreshed {count} elements");
#endif
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[GameConfigEditorPage] Error forcing picker refresh: {ex.Message}");
#endif
        }
    }
}
