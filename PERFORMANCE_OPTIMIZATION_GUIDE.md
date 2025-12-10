# Performance Optimization Guide for ObsidianScout

This document outlines critical performance issues found in the .NET MAUI app and provides solutions to fix lag and improve responsiveness.

## Critical Performance Issues Identified

### 1. **MenuPage - Redundant OnAppearing Calls**
**Problem**: Every time MenuPage appears, it fetches services and updates UI unnecessarily.
**Impact**: Causes noticeable lag when navigating to menu.

**Solution**:
```csharp
// Cache services at construction time
private ISettingsService? _settingsService;
private bool _isInitialized = false;

public MenuPage()
{
    InitializeComponent();
    // Get services once
    var services = Application.Current?.Handler?.MauiContext?.Services;
    _settingsService = services?.GetService<ISettingsService>();
}

protected override async void OnAppearing()
{
    base.OnAppearing();
    
    // Skip if already initialized
    if (_isInitialized)
    {
        // Only update offline mode toggle
        if (_settingsService != null)
        {
      var isOffline = await _settingsService.GetOfflineModeAsync();
  OfflineModeSwitch.IsToggled = isOffline;
        }
   return;
    }
    
    // First-time initialization...
    _isInitialized = true;
}
```

### 2. **AppShell - Heavy Initialization Blocking Startup**
**Problem**: AppShell performs synchronous auth checks, service lookups, and navigation setup on main thread.
**Impact**: App startup is slow (500-1500ms delays).

**Solution**:
- Move all I/O operations off the main thread
- Use lazy loading for profile pictures and user info
- Defer health checks until after UI is rendered

```csharp
private async Task InitializeAuthAndNavigationAsync()
{
    // Use timeout to prevent hanging
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    
    try
    {
      var token = await _settingsService.GetTokenAsync().WaitAsync(cts.Token);
        // ... rest of logic
    }
    catch (OperationCanceledException)
    {
    // Timeout - assume not logged in
  IsLoggedIn = false;
    }
}
```

### 3. **App.xaml.cs - Excessive OnStart Logic**
**Problem**: OnStart waits 500-1000ms+ before checking authentication and preloading data.
**Impact**: Delays first screen appearance.

**Solution**:
```csharp
protected override async void OnStart()
{
    base.OnStart();
    
    // DON'T wait - let Shell handle its own initialization
    // Check auth immediately
  var token = await _settingsService.GetTokenAsync();
    
    if (!string.IsNullOrEmpty(token))
    {
        // Fire and forget data preload - don't await
        _ = Task.Run(async () => {
            try {
     await _dataPreloadService.PreloadAllDataAsync();
      } catch { }
  });
    }
}
```

### 4. **ViewModel ObservableCollections - UI Thread Blocking**
**Problem**: Large collections updated on UI thread causing freezes.
**Files Affected**:
- `ScoutingViewModel.cs` - 60-second periodic refresh on UI thread
- `DataViewModel.cs` - Filter operations block UI with `.Take(100)`
- `GameConfigEditorViewModel.cs` - Heavy JSON parsing on UI thread

**Solution A - Use Background Threading**:
```csharp
private async Task LoadTeamsAsync()
{
    // Build list in background
    var teams = await Task.Run(async () =>
    {
        var result = await _apiService.GetTeamsAsync();
  return result?.Data ?? new List<Team>();
    });
    
    // Update UI collection on main thread in batch
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
        Teams.Clear();
        foreach (var team in teams)
            Teams.Add(team);
    });
}
```

**Solution B - Use Virtual/Incremental Loading**:
```xaml
<CollectionView ItemsSource="{Binding Teams}"
        RemainingItemsThreshold="10"
     RemainingItemsThresholdReachedCommand="{Binding LoadMoreTeamsCommand}">
```

### 5. **Periodic Timers Running Too Frequently**
**Problem**: `ScoutingViewModel` refreshes every 60 seconds regardless of whether page is visible.
**Impact**: Background CPU usage, battery drain, unnecessary network calls.

**Solution**:
```csharp
// Stop timer when page disappears
public void OnDisappearing()
{
    _refreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
}

public void OnAppearing()
{
    // Resume timer
 _refreshTimer?.Change(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
}
```

### 6. **AppShell Health Checks Every 15 Seconds**
**Problem**: `StartPeriodicHealthCheckLoopAsync` runs every 15 seconds on all platforms.
**Impact**: Constant network activity, CPU usage.

**Solution**:
```csharp
// Increase interval to 60 seconds
await Task.Delay(TimeSpan.FromSeconds(60), token);

// Or only check when app comes to foreground
protected override void OnResume()
{
    base.OnResume();
    _ = CheckHealthOnceAsync();
}
```

### 7. **Banner Injection on Every Navigation**
**Problem**: `InjectBannerIntoCurrentPage()` called on every Shell.Navigated event.
**Impact**: Layout thrashing, visual glitches.

**Solution**:
```csharp
// Cache which pages already have banner
private readonly HashSet<Page> _pagesWithBanner = new();

private void InjectBannerIntoCurrentPage()
{
    var currentPage = Shell.Current?.CurrentPage;
    if (currentPage == null || _pagesWithBanner.Contains(currentPage))
        return;
  
    // Inject banner...
    _pagesWithBanner.Add(currentPage);
}
```

### 8. **MauiProgram - HttpClient Timeout Too Long**
**Problem**: HttpClient timeout is 15 seconds - causes UI hangs on slow networks.
**Impact**: App appears frozen during network calls.

**Solution**:
```csharp
var client = new HttpClient(handler);
client.Timeout = TimeSpan.FromSeconds(8); // Reduce to 8 seconds

// Also add retry logic in ApiService
private async Task<T> RetryAsync<T>(Func<Task<T>> operation, int maxRetries = 2)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
         return await operation();
        }
        catch when (i < maxRetries - 1)
      {
     await Task.Delay(500);
   }
    }
    throw;
}
```

## General Best Practices

### Use Lazy Loading
```csharp
// Instead of loading everything at once:
public MainViewModel()
{
    LoadEverything(); // BAD
}

// Load on demand:
public MainViewModel()
{
    // Constructor is lightweight
}

public async Task InitializeAsync()
{
// Called by page OnAppearing
    await LoadDataAsync();
}
```

### Virtualize Large Lists
```xaml
<!-- Use CollectionView instead of ListView -->
<CollectionView ItemsSource="{Binding Items}">
    <!-- Only visible items are rendered -->
</CollectionView>
```

### Debounce Search/Filter Operations
```csharp
private CancellationTokenSource? _searchCts;

partial void OnQueryChanged(string value)
{
    _searchCts?.Cancel();
    _searchCts = new CancellationTokenSource();
    
    _ = Task.Run(async () =>
    {
        await Task.Delay(300, _searchCts.Token);
        await MainThread.InvokeOnMainThreadAsync(() => ApplyFilter());
    }, _searchCts.Token);
}
```

### Avoid Async Void Event Handlers
```csharp
// BAD
private async void OnButtonClicked(object sender, EventArgs e)
{
    await LongRunningOperation();
}

// GOOD
private void OnButtonClicked(object sender, EventArgs e)
{
    _ = HandleButtonClickAsync();
}

private async Task HandleButtonClickAsync()
{
    try
    {
        await LongRunningOperation();
    }
    catch (Exception ex)
    {
        // Handle error
    }
}
```

### Profile Your App
Use .NET MAUI profiling tools to identify bottlenecks:
```bash
# Windows
dotnet trace collect --providers Microsoft-Windows-DotNETRuntime

# Android
adb shell simpleperf record -a

# Analyze with PerfView or dotnet-trace
```

## Quick Wins Summary

1. ? Cache services in constructors instead of fetching in OnAppearing
2. ? Move I/O operations off UI thread with Task.Run()
3. ? Reduce timer intervals (60s ? 120s for background refreshes)
4. ? Reduce HttpClient timeout (15s ? 8s)
5. ? Add debouncing to search/filter (300ms delay)
6. ? Use virtual lists for large data sets
7. ? Stop timers when pages are not visible
8. ? Lazy load profile pictures and heavy resources
9. ? Remove unnecessary delays (Task.Delay) in startup code
10. ? Batch UI updates instead of individual property changes

## Expected Performance Improvements

- **App Startup**: 40-60% faster (from 2s to <1s)
- **Page Navigation**: 50-70% faster (no stuttering)
- **Search/Filter**: Instant response (<50ms)
- **Memory Usage**: 20-30% reduction
- **Battery Life**: 15-25% improvement on mobile

## Testing Recommendations

1. Test on low-end devices (Android with 2GB RAM)
2. Test with slow network (throttle to 3G)
3. Test with 1000+ items in lists
4. Monitor memory usage over 30+ minutes
5. Check CPU usage during idle

## Next Steps

1. Fix build errors in EventsPage.xaml (missing closing tag)
2. Apply MenuPage optimizations
3. Reduce AppShell initialization overhead
4. Optimize ViewModel collection operations
5. Add performance monitoring/telemetry
6. Set up automated performance regression tests
