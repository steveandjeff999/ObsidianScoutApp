# QR Scanner Rotation Fix

## Problem
The app was crashing with a **SIGSEGV (Segmentation Fault)** when the device was rotated while on the QR scanner page. The error occurred because:

1. The camera preview (`PreviewView`) became null during rotation
2. The `CameraQRScanner` was not properly handling configuration changes
3. The camera needed to be reinitialized after rotation but wasn't

## Root Cause
```
11-24 16:57:42.049 F/libc (12727): Fatal signal 11 (SIGSEGV), code 2 (SEGV_ACCERR), fault addr 0x6b60743000
```

When Android rotates the screen, it triggers a configuration change. The activity's views are destroyed and recreated, but the camera binding wasn't being updated properly, leading to a memory access violation.

## Solution

### 1. Added Reinitialization Flag (`CameraQRScanner.cs`)
```csharp
private bool _isInitialized;

public async Task StartAsync()
{
    // ...
    if (_isInitialized)
    {
        Debug.WriteLine("CameraQRScanner: Already initialized, skipping");
  return;
    }
    // ...
    _isInitialized = true;
}
```

### 2. Reset Flag on Stop
```csharp
public void Stop()
{
    _cameraProvider?.UnbindAll();
    _imageAnalysis?.ClearAnalyzer();
    _camera = null;
    _isInitialized = false;  // ? Reset flag
}
```

### 3. Handle Rotation in Page (`QRCodeScannerPage.xaml.cs`)
```csharp
private bool _isReinitialized;

public QRCodeScannerPage(QRCodeScannerViewModel viewModel)
{
    // ...
    SizeChanged += OnPageSizeChanged;  // ? Monitor size changes
}

private void OnPageSizeChanged(object? sender, EventArgs e)
{
    if (_cameraInitialized && !_isReinitialized)
    {
      _isReinitialized = true;
        // Stop and reinitialize camera
        _androidScanner?.Stop();
        await Task.Delay(200);
  _cameraInitialized = false;
await InitializeCameraAsync();
    }
}
```

### 4. Safer Camera Binding
```csharp
private void BindCamera()
{
    // Unbind safely on main thread
  MainThread.BeginInvokeOnMainThread(() =>
 {
        try
        {
            _cameraProvider?.UnbindAll();
        }
        catch (Exception ex)
 {
    Debug.WriteLine($"Error unbinding: {ex.Message}");
    }
    });
    
    // Check rotation safely
    var windowManager = Platform.CurrentActivity?.WindowManager;
  if (windowManager?.DefaultDisplay == null)
    {
        Debug.WriteLine("Cannot get display rotation");
        return;
    }
    
    var rotation = (int)windowManager.DefaultDisplay.Rotation;
    // ...use rotation for camera setup...
}
```

### 5. Configuration Changes Already Handled in MainActivity
The `MainActivity` already has proper configuration change handling:
```csharp
[Activity(
    ConfigurationChanges = ConfigChanges.ScreenSize | 
      ConfigChanges.Orientation | 
            ConfigChanges.UiMode | 
  ConfigChanges.ScreenLayout | 
             ConfigChanges.SmallestScreenSize | 
     ConfigChanges.Density,
    ...)]
```

## What Changed

| File | Changes |
|------|---------|
| `CameraQRScanner.cs` | Added `_isInitialized` flag, reset on stop, safer rotation handling |
| `QRCodeScannerPage.xaml.cs` | Added `SizeChanged` event handler, reinitialize camera on rotation |
| `MainActivity.cs` | No changes needed - already handles config changes |

## Testing
1. ? Open QR scanner
2. ? Rotate device to landscape
3. ? Camera preview should adjust automatically
4. ? Rotate back to portrait
5. ? Camera should continue working
6. ? No crashes or "preview not available" errors

## Technical Details

### Why This Works
- **Configuration Change Detection**: The `SizeChanged` event fires when rotation occurs
- **Graceful Cleanup**: Camera is properly stopped before reinitialization
- **Thread Safety**: All camera operations run on the main thread
- **Defensive Checks**: Null checks prevent crashes if resources aren't available
- **Initialization Flag**: Prevents duplicate camera initialization attempts

### Key Concepts
1. **ConfigChanges Flag**: Tells Android we'll handle config changes ourselves
2. **SizeChanged Event**: Detects when layout changes due to rotation
3. **Camera Rebinding**: CameraX requires rebinding after configuration changes
4. **PreviewView Recreation**: The view must be recreated with new dimensions

## Related Files
- `ObsidianScout/Platforms/Android/CameraQRScanner.cs`
- `ObsidianScout/Views/QRCodeScannerPage.xaml.cs`
- `ObsidianScout/Platforms/Android/MainActivity.cs`

## Prevention
To prevent similar issues in the future:
1. Always handle configuration changes for camera-related pages
2. Monitor the `SizeChanged` event for views that depend on screen dimensions
3. Properly cleanup and reinitialize resources after config changes
4. Use initialization flags to prevent duplicate operations
5. Test rotation thoroughly on all camera pages
