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
    InitializeComponent();
    }

    private void OnYesClicked(object? sender, EventArgs e)
    {
        YesClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnNoClicked(object? sender, EventArgs e)
    {
        NoClicked?.Invoke(this, EventArgs.Empty);
    }
}
