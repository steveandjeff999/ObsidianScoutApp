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

        public event EventHandler<bool>? ConnectivityChanged;

        public ConnectivityService()
        {
            // Don't cache initial state, check live every time

            // Poll every 30 seconds to fire connectivity changed events
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
            var isOnline = e.NetworkAccess == NetworkAccess.Internet;
            System.Diagnostics.Debug.WriteLine($"[ConnectivityService] MAUI connectivity changed: {e.NetworkAccess} (IsOnline={isOnline})");
            ConnectivityChanged?.Invoke(this, isOnline);
        }

        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var current = CheckNetworkAccess();
            var previous = IsConnected; // Check live, then compare
            if (previous != current)
            {
                System.Diagnostics.Debug.WriteLine($"[ConnectivityService] Connectivity changed via timer: {previous} ? {current}");
                ConnectivityChanged?.Invoke(this, current);
            }
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

        // CRITICAL FIX: Check live every time instead of caching
        public bool IsConnected
        {
            get
            {
                try
                {
                    var networkAccess = Connectivity.Current.NetworkAccess;
                    var isConnected = networkAccess == NetworkAccess.Internet;
                    System.Diagnostics.Debug.WriteLine($"[ConnectivityService] IsConnected called: NetworkAccess={networkAccess}, Result={isConnected}");
                    return isConnected;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConnectivityService] IsConnected check failed: {ex.Message}");
                    return false;
                }
            }
        }

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
