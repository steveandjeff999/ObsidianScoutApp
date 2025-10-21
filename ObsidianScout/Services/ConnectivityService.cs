using System;
using Microsoft.Maui.Networking;

namespace ObsidianScout.Services
{
    public interface IConnectivityService
    {
        bool IsConnected { get; }
        event EventHandler<bool>? ConnectivityChanged;
    }

    public class ConnectivityService : IConnectivityService, IDisposable
    {
        private readonly System.Timers.Timer _timer;
        private bool _isConnected;

        public event EventHandler<bool>? ConnectivityChanged;

        public ConnectivityService()
        {
            _isConnected = CheckNetworkAccess();

            // Poll every 30 seconds
            _timer = new System.Timers.Timer(30_000);
            _timer.AutoReset = true;
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();

            // Also subscribe to MAUI connectivity changes for faster updates
            try
            {
                Connectivity.ConnectivityChanged += OnMauiConnectivityChanged;
            }
            catch
            {
                // Ignore if not available on platform
            }
        }

        private void OnMauiConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            UpdateConnectivity(e.NetworkAccess == NetworkAccess.Internet);
        }

        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var current = CheckNetworkAccess();
            UpdateConnectivity(current);
        }

        private bool CheckNetworkAccess()
        {
            try
            {
                return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateConnectivity(bool current)
        {
            if (_isConnected != current)
            {
                _isConnected = current;
                ConnectivityChanged?.Invoke(this, _isConnected);
            }
        }

        public bool IsConnected => _isConnected;

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();

            try
            {
                Connectivity.ConnectivityChanged -= OnMauiConnectivityChanged;
            }
            catch { }
        }
    }
}
