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
        private System.Timers.Timer? _timer;
        private bool _disposed = false;
        private bool _lastKnownState = true; // Assume connected initially
        private readonly object _stateLock = new object(); // Thread-safe state access
        private DateTime _lastLogTime = DateTime.MinValue;
        private const int LOG_THROTTLE_SECONDS = 30; // Only log every 30 seconds to reduce noise

        public event EventHandler<bool>? ConnectivityChanged;

        public ConnectivityService()
        {
            try
            {
 // Initialize with current state
 _lastKnownState = CheckNetworkAccess();

        // Poll every 30 seconds to fire connectivity changed events
     _timer = new System.Timers.Timer(30_000);
      _timer.AutoReset = true;
  _timer.Elapsed += Timer_Elapsed;
             _timer.Start();

           // Also subscribe to MAUI connectivity changes for faster updates
        SubscribeToMauiConnectivity();
     }
            catch (Exception ex)
            {
      System.Diagnostics.Debug.WriteLine($"[ConnectivityService] Constructor error: {ex.Message}");
    }
        }

    private void SubscribeToMauiConnectivity()
        {
        try
            {
    if (Connectivity.Current != null)
          {
       Connectivity.ConnectivityChanged += OnMauiConnectivityChanged;
      }
      }
            catch (Exception ex)
          {
                System.Diagnostics.Debug.WriteLine($"[ConnectivityService] Failed to subscribe to MAUI connectivity: {ex.Message}");
            }
 }

        private void OnMauiConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
   {
 try
    {
        var isOnline = e?.NetworkAccess == NetworkAccess.Internet;
       System.Diagnostics.Debug.WriteLine($"[ConnectivityService] MAUI connectivity changed: {e?.NetworkAccess} (IsOnline={isOnline})");
             
                lock (_stateLock)
                {
                    _lastKnownState = isOnline;
                }
     
           // Invoke event safely
           SafeInvokeConnectivityChanged(isOnline);
            }
            catch (Exception ex)
            {
System.Diagnostics.Debug.WriteLine($"[ConnectivityService] OnMauiConnectivityChanged error: {ex.Message}");
            }
        }

        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
     try
            {
     var current = CheckNetworkAccess();
       bool previous;
       lock (_stateLock)
       {
           previous = _lastKnownState;
       }
                
                if (previous != current)
        {
   System.Diagnostics.Debug.WriteLine($"[ConnectivityService] Connectivity changed via timer: {previous} ? {current}");
   lock (_stateLock)
   {
       _lastKnownState = current;
   }
             SafeInvokeConnectivityChanged(current);
     }
       }
            catch (Exception ex)
            {
           System.Diagnostics.Debug.WriteLine($"[ConnectivityService] Timer_Elapsed error: {ex.Message}");
    }
   }

        private void SafeInvokeConnectivityChanged(bool isConnected)
        {
        try
{
       ConnectivityChanged?.Invoke(this, isConnected);
            }
     catch (Exception ex)
            {
  System.Diagnostics.Debug.WriteLine($"[ConnectivityService] ConnectivityChanged handler error: {ex.Message}");
    }
     }

        private bool CheckNetworkAccess()
        {
            try
   {
           var connectivity = Connectivity.Current;
           if (connectivity == null)
       {
           System.Diagnostics.Debug.WriteLine("[ConnectivityService] Connectivity.Current is null");
           lock (_stateLock)
           {
               return _lastKnownState; // Return last known state
           }
   }
      
           return connectivity.NetworkAccess == NetworkAccess.Internet;
     }
            catch (Exception ex)
            {
        System.Diagnostics.Debug.WriteLine($"[ConnectivityService] CheckNetworkAccess error: {ex.Message}");
  lock (_stateLock)
  {
      return _lastKnownState; // Return last known state on error
  }
         }
        }

   // CRITICAL FIX: Check live every time instead of caching
  public bool IsConnected
        {
            get
            {
    try
     {
                var connectivity = Connectivity.Current;
          if (connectivity == null)
                    {
    // Only log occasionally to reduce noise
    if ((DateTime.Now - _lastLogTime).TotalSeconds > LOG_THROTTLE_SECONDS)
    {
        System.Diagnostics.Debug.WriteLine("[ConnectivityService] IsConnected: Connectivity.Current is null, returning last known state");
        _lastLogTime = DateTime.Now;
    }
    lock (_stateLock)
    {
        return _lastKnownState;
    }
      }
         
        var networkAccess = connectivity.NetworkAccess;
         var isConnected = networkAccess == NetworkAccess.Internet;
        
             // Update last known state thread-safely
    lock (_stateLock)
    {
        _lastKnownState = isConnected;
    }
           
      return isConnected;
      }
   catch (Exception ex)
     {
    System.Diagnostics.Debug.WriteLine($"[ConnectivityService] IsConnected check failed: {ex.Message}");
          lock (_stateLock)
          {
              return _lastKnownState; // Return last known state on error
          }
          }
     }
        }

        public void Dispose()
    {
       if (_disposed) return;
            _disposed = true;

            try
            {
 _timer?.Stop();
    _timer?.Dispose();
                _timer = null;
      }
          catch (Exception ex)
  {
System.Diagnostics.Debug.WriteLine($"[ConnectivityService] Timer dispose error: {ex.Message}");
            }

     try
            {
       if (Connectivity.Current != null)
          {
       Connectivity.ConnectivityChanged -= OnMauiConnectivityChanged;
 }
            }
      catch (Exception ex)
          {
              System.Diagnostics.Debug.WriteLine($"[ConnectivityService] Unsubscribe error: {ex.Message}");
       }
      }
    }
}
