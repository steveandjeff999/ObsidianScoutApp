using ObsidianScout.ViewModels;
using System.ComponentModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace ObsidianScout.Views;

public partial class GraphsPage : ContentPage
{
    private readonly GraphsViewModel _viewModel;

    public GraphsPage(GraphsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
        // Listen for PlotlyHtml changes to load into WebView
        if (_viewModel is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
        // If already prepared, load the Plotly HTML
        if (!string.IsNullOrEmpty(_viewModel.PlotlyHtml))
            LoadPlotlyHtml(_viewModel.PlotlyHtml);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "PlotlyHtml")
        {
            var vm = BindingContext as GraphsViewModel;
            if (vm != null && !string.IsNullOrEmpty(vm.PlotlyHtml))
            {
                System.Diagnostics.Debug.WriteLine("[GraphsPage] PlotlyHtml changed - forcing WebView reload");
                // Force WebView to refresh by clearing first then loading
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var web = this.FindByName<WebView>("PlotlyWebView");
                    if (web != null)
                    {
                        // Clear existing content first
                        web.Source = null;
                        // Small delay to ensure clear is processed
                        Task.Delay(100).ContinueWith(_ =>
                        {
                            MainThread.BeginInvokeOnMainThread(() => LoadPlotlyHtml(vm.PlotlyHtml));
                        });
                    }
                });
            }
        }
    }

    private void LoadPlotlyHtml(string html)
    {
        try
        {
            var web = this.FindByName<WebView>("PlotlyWebView");
            if (web == null)
            {
                System.Diagnostics.Debug.WriteLine("[GraphsPage] WebView 'PlotlyWebView' not found");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[GraphsPage] Loading Plotly HTML (length: {html?.Length ?? 0})");

            // If the payload looks like raw HTML, use HtmlWebViewSource.
            // If it's an absolute URI (file:// or http(s)://), use UrlWebViewSource so scripts and local files load correctly.
            var trimmed = html?.Trim() ?? string.Empty;
            if (trimmed.StartsWith("<", StringComparison.Ordinal))
            {
                try
                {
                    // Try loading inline HTML first
                    var htmlSource = new HtmlWebViewSource { Html = html };
                    // If the HTML references relative assets (plotly JS) set base URL on Android so the WebView can resolve
                    if (DeviceInfo.Platform == DevicePlatform.Android)
                    {
                        htmlSource.BaseUrl = "file:///android_asset/";
                    }
                    web.Source = htmlSource;
                    System.Diagnostics.Debug.WriteLine("[GraphsPage] Loaded HTML into WebView via HtmlWebViewSource");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GraphsPage] HtmlWebViewSource load failed: {ex.Message}");
                    // If inline load fails (commonly on Windows due to NavigateToString limits), write to disk and load via file://
                    try
                    {
                        if (DeviceInfo.Platform != DevicePlatform.Android)
                        {
                            var dir = Path.Combine(FileSystem.AppDataDirectory, "plotly_bundle");
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            // Add timestamp to filename to force reload and avoid caching
                            var timestamp = DateTime.Now.Ticks;
                            var path = Path.Combine(dir, $"plot_{timestamp}.html");
                            File.WriteAllText(path, html);
                            var uri = new Uri(path).AbsoluteUri;
                            web.Source = new UrlWebViewSource { Url = uri };
                            System.Diagnostics.Debug.WriteLine($"[GraphsPage] Loaded HTML via file URI: {uri}");
                        }
                        else
                        {
                            // Android shouldn't hit here; fallback to inline
                            web.Source = new HtmlWebViewSource { Html = html };
                        }
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GraphsPage] Fallback write/load failed: {ex2.Message}");
                    }
                }
            }
            else if (Uri.IsWellFormedUriString(html, UriKind.Absolute))
            {
                web.Source = new UrlWebViewSource { Url = html };
                System.Diagnostics.Debug.WriteLine($"[GraphsPage] Loaded HTML via URL: {html}");
            }
            else
            {
                // Fallback: treat as HTML
                web.Source = new HtmlWebViewSource { Html = html };
                System.Diagnostics.Debug.WriteLine("[GraphsPage] Loaded HTML (fallback)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GraphsPage] LoadPlotlyHtml error: {ex.Message}");
        }
    }
}
