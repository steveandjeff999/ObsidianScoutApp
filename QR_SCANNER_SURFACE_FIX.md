# QR Scanner Surface Timeout Fix

## Problem
The camera feed was not displaying on the QR scanner page. The Android logs showed:

```
E/Camera2CameraImpl: Unable to configure camera
E/Camera2CameraImpl: java.util.concurrent.TimeoutException: Future is not done within 5000 ms
D/SurfaceView: updateSurface: has no frame
```

The PreviewView had size `0x0` when the camera was being initialized, causing the surface to timeout.

## Root Cause
The camera was being initialized **before** the `CameraContainer` (ContentView) had been laid out by the MAUI layout system. When CameraX tried to configure the capture session, the `PreviewView` had no valid size, so the Android `SurfaceView` couldn't create a surface, causing a timeout.

## Solution
Added proper synchronization to wait for the `CameraContainer` to be laid out with a valid size before starting the camera initialization:

### Key Changes in `QRCodeScannerPage.xaml.cs`:

1. **Wait for Container Layout**:
   ```csharp
   if (CameraContainer.Width <= 0 || CameraContainer.Height <= 0)
   {
       var layoutTcs = new TaskCompletionSource<bool>();
       EventHandler sizeChangedHandler = null;
       sizeChangedHandler = (s, e) =>
       {
           if (CameraContainer.Width > 0 && CameraContainer.Height > 0)
   {
               CameraContainer.SizeChanged -= sizeChangedHandler;
       layoutTcs.TrySetResult(true);
           }
  };
       CameraContainer.SizeChanged += sizeChangedHandler;
    
       var layoutTimeoutTask = Task.Delay(3000);
       var layoutCompletedTask = await Task.WhenAny(layoutTcs.Task, layoutTimeoutTask);
 
     if (layoutCompletedTask == layoutTimeoutTask)
       {
      // Timeout - show error
         return;
       }
   }
   ```

2. **Wait for PreviewView Size**:
   After adding the `PreviewView` to the container, wait for it to be laid out:
   ```csharp
   viewGroup.AddView(previewView, 0, layoutParams);
   await Task.Delay(100);
   viewGroup.RequestLayout();
   await Task.Delay(300);
   
   // Wait for PreviewView to have non-zero size
   int sizeAttempts = 0;
 while ((previewView.Width <= 0 || previewView.Height <= 0) && sizeAttempts < 20)
   {
       await Task.Delay(100);
       sizeAttempts++;
   }
   ```

3. **Increased Camera Start Timeout**:
   Changed from 10 seconds to 15 seconds to allow more time for the camera to initialize:
   ```csharp
   var timeoutTask = Task.Delay(15000);
   ```

4. **Fixed Variable Name Conflicts**:
   Renamed variables to avoid conflicts in nested scopes:
   - `timeoutTask` ? `layoutTimeoutTask` (for layout wait)
   - `completedTask` ? `layoutCompletedTask` (for layout wait)

## Testing
To verify the fix:
1. Navigate to the QR Code Scanner page
2. The camera feed should appear after a brief initialization
3. The logs should show:
   ```
   QRCodeScannerPage: Container sized: {width}x{height}
   QRCodeScannerPage: PreviewView added to container
   QRCodeScannerPage: Final PreviewView size: {width}x{height}
   CameraQRScanner: Camera bound to lifecycle successfully
   Camera2CameraImpl: CameraDevice.onOpened()
   ```

## Impact
- ? Camera feed now displays correctly
- ? No more surface timeout errors
- ? Proper lifecycle management maintained
- ? User-friendly error messages for edge cases

## Related Files
- `ObsidianScout/Views/QRCodeScannerPage.xaml.cs` - Main fix location
- `ObsidianScout/Platforms/Android/CameraQRScanner.cs` - Camera implementation

## Notes
- This fix ensures the Android view hierarchy is properly set up before camera initialization
- The `SizeChanged` event handler pattern is the recommended approach for waiting for MAUI views to be laid out
- Timeouts are included to prevent indefinite waiting in case of layout issues
