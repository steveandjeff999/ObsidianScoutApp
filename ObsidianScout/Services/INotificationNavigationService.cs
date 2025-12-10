namespace ObsidianScout.Services;

/// <summary>
/// Service responsible for handling navigation triggered by notification taps.
/// Works when app is in foreground, background, or cold-started.
/// </summary>
public interface INotificationNavigationService
{
    /// <summary>
    /// Gets or sets whether there's a pending navigation from a notification tap.
    /// </summary>
    bool HasPendingNavigation { get; }
    
    /// <summary>
    /// Gets the pending navigation URI (shell route with query parameters).
    /// </summary>
    string? PendingNavigationUri { get; }
    
    /// <summary>
    /// Gets the pending navigation data dictionary.
    /// </summary>
    Dictionary<string, string>? PendingNavigationData { get; }
  
    /// <summary>
    /// Stores a pending navigation from notification data.
 /// Called when a notification is tapped.
    /// </summary>
    /// <param name="data">Dictionary containing notification type and parameters</param>
    void SetPendingNavigation(Dictionary<string, string> data);
    
    /// <summary>
    /// Clears any pending navigation.
  /// </summary>
    void ClearPendingNavigation();
    
    /// <summary>
  /// Attempts to execute any pending navigation.
    /// Should be called when app becomes active or Shell is ready.
    /// </summary>
 /// <returns>True if navigation was executed, false otherwise</returns>
    Task<bool> TryExecutePendingNavigationAsync();
    
    /// <summary>
    /// Immediately navigates based on notification data.
    /// Used when app is already in foreground.
    /// </summary>
    /// <param name="data">Dictionary containing notification type and parameters</param>
    Task NavigateFromNotificationAsync(Dictionary<string, string> data);
    
    /// <summary>
    /// Event raised when a notification navigation is requested.
    /// Useful for ViewModels to react to navigation.
    /// </summary>
    event EventHandler<NotificationNavigationEventArgs>? NavigationRequested;
}

/// <summary>
/// Event args for notification navigation events.
/// </summary>
public class NotificationNavigationEventArgs : EventArgs
{
  public string NavigationType { get; }
    public Dictionary<string, string> Data { get; }
    public string NavigationUri { get; }
 
    public NotificationNavigationEventArgs(string navigationType, Dictionary<string, string> data, string navigationUri)
  {
        NavigationType = navigationType;
        Data = data;
        NavigationUri = navigationUri;
    }
}
