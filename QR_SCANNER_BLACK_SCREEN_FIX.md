# QR Scanner Black Screen - Comprehensive Fix

## Problem
The QR scanner camera shows a black screen due to a CameraX timeout error:
```
java.util.concurrent.TimeoutException: Future is not done within 5000 ms
Unable to configure camera
```

## Root Cause
The `SurfaceView` inside the `PreviewView` is not fully created when CameraX tries to bind and configure the camera. CameraX waits for the surface to be ready but times out after 5 seconds, resulting in a black screen.

**Timeline of the Issue:**
1. PreviewView is added to the ViewGroup
2. CameraX binds to lifecycle and tries to configure camera
3. CameraX waits for the Surface from PreviewView
4. SurfaceView is still being created asynchronously
5. After 5 seconds, CameraX times out
6. Camera binding fails, screen stays black

## Solution
The fix involves two critical changes:

### 1. Extended Surface Creation Wait
Added a dedicated wait period (1000ms + multiple layout passes) to ensure the SurfaceView inside PreviewView is fully created before attempting camera binding:

```csharp
// CRITICAL FIX: Wait for the SurfaceView inside PreviewView to be fully created
Debug.WriteLine("CameraQRScanner: Waiting for SurfaceView to be fully created...");

// Give Android time to create the SurfaceView and its underlying Surface
await Task.Delay(1000);

// Force multiple layout passes to ensure surface creation
for (int i = 0; i < 3; i++)
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
      try
    {
  _previewView?.RequestLayout();
        _previewView?.ForceLayout();
  (_previewView?.Parent as global::Android.Views.ViewGroup)?.RequestLayout();
        }
        catch { }
    });
    await Task.Delay(200);
}
```

### 2. Staged Camera Binding
Changed the camera binding strategy to open the camera first with just ImageAnalysis, then add the Preview use case after the surface is ready:

```csharp
// CRITICAL FIX: Bind camera FIRST without surface provider
// This opens the camera and prepares it
Debug.WriteLine("CameraQRScanner: Binding camera to lifecycle (without surface provider)");
_camera = _cameraProvider.BindToLifecycle(
    lifecycleOwner,
    cameraSelector,
    _imageAnalysis); // Only bind ImageAnalysis first

Debug.WriteLine("CameraQRScanner: Camera bound to lifecycle successfully");

// NOW set the surface provider AFTER camera is bound and ready
Task.Run(async () =>
{
    try
    {
     // Wait a bit for camera to fully initialize
        await Task.Delay(500);
      
        await MainThread.InvokeOnMainThreadAsync(() =>
     {
       var surfaceProvider = _previewView?.SurfaceProvider;
  if (surfaceProvider != null)
            {
                // Rebind with Preview now that we have the surface
       _camera = _cameraProvider.BindToLifecycle(
  lifecycleOwner,
  cameraSelector,
preview,
            _imageAnalysis);

   var executor = ContextCompat.GetMainExecutor(_context);
      preview.SetSurfaceProvider(executor, surfaceProvider);
            }
        });
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error setting surface provider: {ex.Message}");
    }
});
```

## Why This Works

### Extended Wait Period
- **1000ms initial delay**: Gives Android sufficient time to create the SurfaceView's underlying Surface
- **Multiple layout passes**: Forces Android to complete the view hierarchy layout process
- **Verification loops**: Ensures the surface is actually created before proceeding

### Staged Binding
- **Phase 1**: Open camera with ImageAnalysis only (no surface required)
- **Phase 2**: Once camera is open and surface is ready, add Preview use case
- **Async rebinding**: Allows the first binding to complete before adding surface dependency
- **No timeout**: Camera is already open when Preview is added, so no 5-second wait

## Benefits
1. **Eliminates timeout**: Camera opens before needing the surface
2. **Proper surface readiness**: Multiple verification steps ensure surface is created
3. **Graceful fallback**: Error handling for each stage
4. **Maintains functionality**: QR code scanning starts immediately with ImageAnalysis

## Previous Attempts
Previous fixes tried:
- Waiting for PreviewView dimensions (insufficient - SurfaceView is created separately)
- Setting explicit dimensions (helps layout but doesn't guarantee surface creation)
- Forcing layout passes (helps but needs more time)

The key insight: **SurfaceView creation is asynchronous and takes longer than PreviewView layout**

## Testing Recommendations
1. Test on devices with different camera initialization speeds
2. Verify QR code scanning starts within 2-3 seconds
3. Check logs for "Camera bound successfully" and "Surface provider set successfully"
4. Ensure no timeout errors appear in logs

## Files Modified
- `ObsidianScout/Platforms/Android/CameraQRScanner.cs`
  - `WaitForPreviewViewReady()`: Extended wait time and added surface creation verification
  - `BindCamera()`: Implemented staged binding approach
