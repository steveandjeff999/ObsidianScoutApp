using System;
using Microsoft.Maui.Controls;

namespace ObsidianScout.Views.Controls;

public partial class ConnectionBannerView : ContentView
{
    public static readonly BindableProperty ShowBannerProperty =
 BindableProperty.Create(nameof(ShowBanner), typeof(bool), typeof(ConnectionBannerView), false);

    public static readonly BindableProperty ShowOfflineBannerProperty =
    BindableProperty.Create(nameof(ShowOfflineBanner), typeof(bool), typeof(ConnectionBannerView), false);

    public static readonly BindableProperty ShowConnectionProblemProperty =
      BindableProperty.Create(nameof(ShowConnectionProblem), typeof(bool), typeof(ConnectionBannerView), false);

  public static readonly BindableProperty MessageProperty =
    BindableProperty.Create(nameof(Message), typeof(string), typeof(ConnectionBannerView), string.Empty);

    public bool ShowBanner
    {
        get => (bool)GetValue(ShowBannerProperty);
        set => SetValue(ShowBannerProperty, value);
    }

    public bool ShowOfflineBanner
    {
        get => (bool)GetValue(ShowOfflineBannerProperty);
        set => SetValue(ShowOfflineBannerProperty, value);
    }

    public bool ShowConnectionProblem
    {
        get => (bool)GetValue(ShowConnectionProblemProperty);
        set => SetValue(ShowConnectionProblemProperty, value);
    }

    public string Message
    {
 get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public event EventHandler? YesClicked;
    public event EventHandler? NoClicked;

    public ConnectionBannerView()
    {
    try
        {
            InitializeComponent();
        }
        catch (Exception ex)
  {
   System.Diagnostics.Debug.WriteLine($"[ConnectionBannerView] InitializeComponent error: {ex.Message}");
        }
    }

    private void OnYesClicked(object? sender, EventArgs e)
    {
  try
        {
            YesClicked?.Invoke(this, EventArgs.Empty);
}
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionBannerView] OnYesClicked error: {ex.Message}");
        }
    }

    private void OnNoClicked(object? sender, EventArgs e)
    {
        try
        {
  NoClicked?.Invoke(this, EventArgs.Empty);
        }
     catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionBannerView] OnNoClicked error: {ex.Message}");
        }
    }
}
