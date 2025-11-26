using Android.Content;
using Android.Graphics;
using Android.Runtime;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using Java.Util.Concurrent;
using Microsoft.Maui.Platform;
using System.Diagnostics;
using ZXing;
using ZXing.Common;

namespace ObsidianScout.Platforms.Android;

public class CameraInfo
{
    public string Name { get; set; } = string.Empty;
    public int LensFacing { get; set; }
    public bool IsDefault { get; set; }
}

public class CameraQRScanner : IDisposable
{
    private PreviewView? _previewView;
    private ProcessCameraProvider? _cameraProvider;
    private ICamera? _camera;
    private ImageAnalysis? _imageAnalysis;
    private readonly Context _context;
    private readonly Action<string> _onQRCodeDetected;
    private bool _isDisposed;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;
    private bool _isStarting;
    private int _selectedCameraLensFacing = CameraSelector.LensFacingBack; // Default to back camera
    private bool _isPaused; // Track pause state
    private bool _wasInitializedBeforePause; // Remember state before pause

    public CameraQRScanner(Context context, Action<string> onQRCodeDetected)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _onQRCodeDetected = onQRCodeDetected ?? throw new ArgumentNullException(nameof(onQRCodeDetected));
        
        // DON'T create PreviewView here - it must be created when we have a valid parent container
        Debug.WriteLine("CameraQRScanner: Constructor - PreviewView will be created when needed");
    }

    public PreviewView? GetPreviewView()
    {
        // Create PreviewView on demand if it doesn't exist
        if (_previewView == null)
        {
      Debug.WriteLine("CameraQRScanner: Creating PreviewView on-demand");
            
   // CRITICAL: Must create on main thread
    bool created = false;
 PreviewView? tempView = null;
     
      MainThread.InvokeOnMainThreadAsync(() =>
            {
         tempView = new PreviewView(_context)
     {
     LayoutParameters = new global::Android.Views.ViewGroup.LayoutParams(
        global::Android.Views.ViewGroup.LayoutParams.MatchParent,
    global::Android.Views.ViewGroup.LayoutParams.MatchParent)
          };
         
 // Set implementation mode for better performance
      tempView.SetImplementationMode(PreviewView.ImplementationMode.Performance);
      
    // Set scale type to fill container
           tempView.SetScaleType(PreviewView.ScaleType.FillCenter);
     
                created = true;
            }).Wait(); // Wait for main thread operation to complete
    
    if (created && tempView != null)
 {
         _previewView = tempView;
          Debug.WriteLine($"CameraQRScanner: PreviewView created successfully");
            }
        }
        
        Debug.WriteLine($"CameraQRScanner: GetPreviewView - _previewView is {(_previewView != null ? "NOT NULL" : "NULL")}");
        return _previewView;
    }

    public async Task StartAsync()
    {
        await _initLock.WaitAsync();
        try
     {
  if (_isDisposed)
        {
          Debug.WriteLine("CameraQRScanner: Already disposed");
        return;
            }

  if (_isInitialized)
            {
       Debug.WriteLine("CameraQRScanner: Already initialized, skipping");
                return;
    }

       if (_isStarting)
            {
   Debug.WriteLine("CameraQRScanner: Already starting, skipping");
  return;
       }

      _isStarting = true;
          _isPaused = false; // Clear pause state when starting
      Debug.WriteLine("CameraQRScanner: Starting camera initialization");
            Debug.WriteLine($"CameraQRScanner: State - Disposed:{_isDisposed}, Initialized:{_isInitialized}, Paused:{_isPaused}");

      if (_previewView == null)
   {
    Debug.WriteLine("CameraQRScanner: ERROR - PreviewView is null in StartAsync!");
          _previewView = new PreviewView(_context)
   {
  LayoutParameters = new global::Android.Views.ViewGroup.LayoutParams(
           global::Android.Views.ViewGroup.LayoutParams.MatchParent,
           global::Android.Views.ViewGroup.LayoutParams.MatchParent)
     };
     _previewView.SetImplementationMode(PreviewView.ImplementationMode.Performance);
            }

    Debug.WriteLine("CameraQRScanner: PreviewView ready for camera initialization");
 Debug.WriteLine($"CameraQRScanner: PreviewView state - Size:{_previewView.Width}x{_previewView.Height}, Attached:{_previewView.IsAttachedToWindow}");
   
      // Wait for PreviewView to be attached and have dimensions
      await WaitForPreviewViewReady();

    // Get camera provider with proper async handling
            var cameraProviderFuture = ProcessCameraProvider.GetInstance(_context);
            var executor = ContextCompat.GetMainExecutor(_context);

        var tcs = new TaskCompletionSource<ProcessCameraProvider>();

 cameraProviderFuture.AddListener(new Java.Lang.Runnable(() =>
            {
   try
    {
     var provider = (ProcessCameraProvider?)cameraProviderFuture.Get();
      if (provider != null)
    {
 Debug.WriteLine("CameraQRScanner: Camera provider obtained");
      tcs.TrySetResult(provider);
}
  else
        {
       Debug.WriteLine("CameraQRScanner: Camera provider was null");
          tcs.TrySetException(new InvalidOperationException("Camera provider is null"));
      }
 }
catch (Exception ex)
   {
              Debug.WriteLine($"CameraQRScanner: Error getting camera provider: {ex.Message}");
            tcs.TrySetException(ex);
         }
         }), executor);

   // Wait for camera provider
            _cameraProvider = await tcs.Task;

      if (_cameraProvider != null && !_isDisposed)
       {
        Debug.WriteLine("CameraQRScanner: Binding camera on main thread");
      
        // CRITICAL FIX: Bind camera synchronously on main thread with surface provider ready
     await MainThread.InvokeOnMainThreadAsync(() =>
         {
  if (!_isDisposed)
      {
               BindCamera();
     }
     });
          _isInitialized = true;
       Debug.WriteLine("CameraQRScanner: Camera bound successfully");
     Debug.WriteLine($"CameraQRScanner: Final state - Camera:{_camera != null}, Analysis:{_imageAnalysis != null}");
       }
        }
   catch (Exception ex)
  {
  Debug.WriteLine($"CameraQRScanner: Start error: {ex.Message}\n{ex.StackTrace}");
       _isStarting = false;
            throw;
        }
finally
        {
            _isStarting = false;
     _initLock.Release();
        }
    }

    private async Task WaitForPreviewViewReady()
    {
      if (_previewView == null)
        {
       Debug.WriteLine("CameraQRScanner: ERROR - PreviewView is null in WaitForPreviewViewReady");
        throw new InvalidOperationException("PreviewView is null");
   }

        Debug.WriteLine($"CameraQRScanner: Initial PreviewView size: {_previewView.Width}x{_previewView.Height}");
       Debug.WriteLine($"CameraQRScanner: PreviewView attached to window: {_previewView.IsAttachedToWindow}");

   // CRITICAL: PreviewView MUST be attached to window before camera binding
    int attachAttempts = 0;
     while (!_previewView.IsAttachedToWindow && attachAttempts < 100)
      {
      if (attachAttempts % 20 == 0)
    {
     Debug.WriteLine($"CameraQRScanner: Waiting for PreviewView attachment, attempt {attachAttempts}");
     }
    await Task.Delay(50);
        attachAttempts++;
        }

  Debug.WriteLine($"CameraQRScanner: PreviewView attached after {attachAttempts} attempts: {_previewView.IsAttachedToWindow}");

 if (!_previewView.IsAttachedToWindow)
     {
      throw new InvalidOperationException("PreviewView failed to attach to window after 5 seconds");
        }

 // SurfaceProvider check - CRITICAL for camera binding
      int surfaceAttempts = 0;
    while (_previewView.SurfaceProvider == null && surfaceAttempts < 60)
        {
       if (surfaceAttempts % 10 == 0)
     {
      Debug.WriteLine($"CameraQRScanner: Waiting for SurfaceProvider in WaitForPreviewViewReady, attempt {surfaceAttempts}");
     }
 await Task.Delay(100);
   surfaceAttempts++;
        }

        Debug.WriteLine($"CameraQRScanner: SurfaceProvider ready: {_previewView.SurfaceProvider != null} after {surfaceAttempts} attempts");

  if (_previewView.SurfaceProvider == null)
        {
    Debug.WriteLine("CameraQRScanner: ERROR - SurfaceProvider is null after waiting!");
    throw new InvalidOperationException("Surface failed to initialize after 6 seconds");
   }

  Debug.WriteLine("CameraQRScanner: PreviewView is ready for camera binding");
    }

    private void BindCamera()
{
        if (_cameraProvider == null || _previewView == null || _isDisposed)
 {
     Debug.WriteLine($"CameraQRScanner: BindCamera - provider={_cameraProvider != null}, preview={_previewView != null}, disposed={_isDisposed}");
       return;
 }

     try
        {
     // Unbind any previous use cases
  _cameraProvider.UnbindAll();
  Debug.WriteLine("CameraQRScanner: Unbound all previous camera use cases");

      // Get lifecycle owner
if (Platform.CurrentActivity is not AndroidX.Lifecycle.ILifecycleOwner lifecycleOwner)
   {
    Debug.WriteLine("CameraQRScanner: Current activity is not a lifecycle owner");
       throw new InvalidOperationException("Current activity must implement ILifecycleOwner");
   }
            Debug.WriteLine($"CameraQRScanner: Lifecycle owner obtained: {lifecycleOwner.GetType().Name}");

 // Get current rotation
    var windowManager = Platform.CurrentActivity?.WindowManager;
      if (windowManager?.DefaultDisplay == null)
  {
      Debug.WriteLine("CameraQRScanner: Cannot get display rotation");
   return;
   }

    var rotation = (int)windowManager.DefaultDisplay.Rotation;
    Debug.WriteLine($"CameraQRScanner: Display rotation: {rotation}");

    // CRITICAL: Final validation of surface provider
     var surfaceProvider = _previewView.SurfaceProvider;
  
  if (surfaceProvider == null)
   {
    Debug.WriteLine("CameraQRScanner: ERROR - Surface provider is null at bind time!");
   throw new InvalidOperationException("Surface provider is not available");
  }

     Debug.WriteLine("CameraQRScanner: Surface provider validated successfully");
     Debug.WriteLine($"CameraQRScanner: PreviewView dimensions: {_previewView.Width}x{_previewView.Height}");
     Debug.WriteLine($"CameraQRScanner: PreviewView measured: {_previewView.MeasuredWidth}x{_previewView.MeasuredHeight}");

     // Preview use case with optimized settings
 var preview = new Preview.Builder()
   .SetTargetRotation(rotation)
 .Build();

 // Set surface provider on main thread with validated provider
   var executor = ContextCompat.GetMainExecutor(_context);
    preview.SetSurfaceProvider(executor, surfaceProvider);
      Debug.WriteLine("CameraQRScanner: Surface provider set to Preview use case");

            // Image analysis use case with optimized settings
        _imageAnalysis = new ImageAnalysis.Builder()
    .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
      .SetTargetRotation(rotation)
          .Build();

 _imageAnalysis.SetAnalyzer(
   Executors.NewSingleThreadExecutor(),
        new QRCodeAnalyzer(_onQRCodeDetected, _context));
Debug.WriteLine("CameraQRScanner: Image analysis analyzer set");

// Camera selector - use selected camera
            Debug.WriteLine($"CameraQRScanner: Using camera with lens facing: {_selectedCameraLensFacing}");
    var cameraSelector = new CameraSelector.Builder()
   .RequireLensFacing(_selectedCameraLensFacing)
  .Build();

// Verify camera exists
if (!_cameraProvider.HasCamera(cameraSelector))
     {
       Debug.WriteLine($"CameraQRScanner: WARNING - Selected camera not available, falling back to back camera");
     _selectedCameraLensFacing = CameraSelector.LensFacingBack;
            cameraSelector = new CameraSelector.Builder()
     .RequireLensFacing(CameraSelector.LensFacingBack)
          .Build();
       }

 // Bind camera with both use cases at once
     Debug.WriteLine("CameraQRScanner: Binding camera to lifecycle with Preview and ImageAnalysis");
       _camera = _cameraProvider.BindToLifecycle(
     lifecycleOwner,
    cameraSelector,
            preview,
   _imageAnalysis);

  Debug.WriteLine("CameraQRScanner: ? Camera bound successfully!");
Debug.WriteLine($"CameraQRScanner: Camera info - Has flash: {_camera.CameraInfo?.HasFlashUnit}");
     Debug.WriteLine($"CameraQRScanner: Preview view final size: {_previewView.Width}x{_previewView.Height}");
    
   // Final verification
     if (_camera == null)
   {
     throw new InvalidOperationException("Camera binding returned null");
        }
  }
   catch (Exception ex)
     {
     Debug.WriteLine($"CameraQRScanner: BindCamera ERROR: {ex.Message}");
     Debug.WriteLine($"CameraQRScanner: Stack trace: {ex.StackTrace}");
 throw;
        }
    }

    public void Stop()
    {
        try
        {
     Debug.WriteLine("CameraQRScanner: Stopping");

            // Must run on main thread
       MainThread.BeginInvokeOnMainThread(() =>
            {
    try
            {
         _cameraProvider?.UnbindAll();
  _imageAnalysis?.ClearAnalyzer();
           _camera = null;
          _isInitialized = false;
      _isStarting = false;
        Debug.WriteLine("CameraQRScanner: Camera stopped successfully");
       }
      catch (Exception ex)
       {
          Debug.WriteLine($"CameraQRScanner: Stop unbind error: {ex.Message}");
     }
      });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CameraQRScanner: Stop error: {ex.Message}");
        }
    }

    /// <summary>
    /// Pause camera without disposing (for OnDisappearing)
    /// </summary>
    public void Pause()
    {
        try
     {
    Debug.WriteLine("CameraQRScanner: Pausing camera");
    Debug.WriteLine($"CameraQRScanner: Pre-pause state - Initialized:{_isInitialized}, Paused:{_isPaused}");
     _wasInitializedBeforePause = _isInitialized;
      _isPaused = true;

  // Pause image analysis but keep camera bound
            MainThread.BeginInvokeOnMainThread(() =>
      {
       try
            {
     // Just pause analysis, don't unbind camera
        _imageAnalysis?.ClearAnalyzer();
    Debug.WriteLine("CameraQRScanner: Camera paused (preview still visible)");
  Debug.WriteLine($"CameraQRScanner: Post-pause state - Camera:{_camera != null}, PreviewView:{_previewView != null}");
         }
  catch (Exception ex)
 {
   Debug.WriteLine($"CameraQRScanner: Pause error: {ex.Message}");
        }
     });
        }
        catch (Exception ex)
        {
       Debug.WriteLine($"CameraQRScanner: Pause error: {ex.Message}");
 }
    }

 /// <summary>
    /// Resume camera from pause
    /// </summary>
    public async Task ResumeAsync()
    {
        try
        {
      if (!_isPaused)
     {
       Debug.WriteLine("CameraQRScanner: Not paused, skipping resume");
   return;
     }

       Debug.WriteLine("CameraQRScanner: Resuming camera");
      Debug.WriteLine($"CameraQRScanner: Pre-resume state - WasInitialized:{_wasInitializedBeforePause}, Camera:{_camera != null}");
 _isPaused = false;

     // If camera was initialized before pause, just restart analyzer
 if (_wasInitializedBeforePause && _imageAnalysis != null && _camera != null)
       {
      await MainThread.InvokeOnMainThreadAsync(() =>
       {
     try
          {
   // Restart the analyzer
        _imageAnalysis.SetAnalyzer(
 Executors.NewSingleThreadExecutor(),
    new QRCodeAnalyzer(_onQRCodeDetected, _context));
    
        Debug.WriteLine("CameraQRScanner: Camera resumed (analyzer restarted)");
    Debug.WriteLine($"CameraQRScanner: Post-resume state - Scanning active");
      }
       catch (Exception ex)
          {
    Debug.WriteLine($"CameraQRScanner: Resume analyzer error: {ex.Message}");
   }
      });
     }
        else if (_wasInitializedBeforePause)
 {
 // Need to fully restart camera
      Debug.WriteLine("CameraQRScanner: Camera needs full restart");
  await StartAsync();
    }
 else
  {
     Debug.WriteLine("CameraQRScanner: Camera was not initialized before pause, skipping resume");
  }
        }
   catch (Exception ex)
        {
  Debug.WriteLine($"CameraQRScanner: Resume error: {ex.Message}");
        }
    }

    public void ToggleFlashlight(bool on)
{
  try
        {
         // Must run on main thread
         MainThread.BeginInvokeOnMainThread(() =>
        {
                try
          {
              if (_camera?.CameraInfo?.HasFlashUnit == true)
       {
          _camera.CameraControl?.EnableTorch(on);
      Debug.WriteLine($"CameraQRScanner: Flashlight {(on ? "enabled" : "disabled")}");
   }
       else
      {
  Debug.WriteLine("CameraQRScanner: No flash unit available");
         }
        }
 catch (Exception ex)
        {
 Debug.WriteLine($"CameraQRScanner: Flashlight toggle error: {ex.Message}");
                }
     });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CameraQRScanner: Flashlight error: {ex.Message}");
        }
  }

    public void Dispose()
    {
    if (_isDisposed) return;

        Debug.WriteLine("CameraQRScanner: Disposing");
        _isDisposed = true;
        _isInitialized = false;
        _isStarting = false;

     Stop();

        MainThread.BeginInvokeOnMainThread(() =>
      {
  try
       {
   _imageAnalysis?.Dispose();
         
          // Remove PreviewView from parent to prevent memory leaks
    if (_previewView?.Parent is global::Android.Views.ViewGroup parent)
   {
         parent.RemoveView(_previewView);
           Debug.WriteLine("CameraQRScanner: PreviewView removed from parent");
                }
     
    // Dispose camera provider
       _cameraProvider?.Dispose();
     _cameraProvider = null;
       
  Debug.WriteLine("CameraQRScanner: Disposed successfully");
 }
          catch (Exception ex)
    {
                Debug.WriteLine($"CameraQRScanner: Dispose error: {ex.Message}");
  }
      });

        _initLock.Dispose();

        GC.SuppressFinalize(this);
    }

    private class QRCodeAnalyzer : Java.Lang.Object, ImageAnalysis.IAnalyzer
    {
      private readonly Action<string> _onQRCodeDetected;
        private readonly Context _context;
        private long _lastScanTime;
        private readonly ZXing.MultiFormatReader _reader;
  private const int ThrottleMs = 500; // Scan at most every 500ms

    public QRCodeAnalyzer(Action<string> onQRCodeDetected, Context context)
    {
   _onQRCodeDetected = onQRCodeDetected;
  _context = context;
            
   // Initialize ZXing reader with QR code focus
            _reader = new ZXing.MultiFormatReader();
       var hints = new Dictionary<DecodeHintType, object>
            {
  { DecodeHintType.POSSIBLE_FORMATS, new List<BarcodeFormat> { BarcodeFormat.QR_CODE } },
                { DecodeHintType.TRY_HARDER, true }
        };
   _reader.Hints = hints;
        }

        // Required by IAnalyzer interface
  public global::Android.Util.Size? DefaultTargetResolution => null;

        // Required by IAnalyzer interface
        public int TargetCoordinateSystem => ImageAnalysis.CoordinateSystemOriginal;

   public void Analyze(IImageProxy image)
     {
            try
       {
    // Throttle scanning
var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
     if (currentTime - _lastScanTime < ThrottleMs)
         {
                  return;
           }

// Get image data
       var plane = image.GetPlanes()?[0];
   if (plane?.Buffer == null)
      {
              return;
                }

    var buffer = plane.Buffer;
         var imageData = new byte[buffer.Remaining()];
     buffer.Get(imageData);

      // Convert to ZXing luminance source
                var source = new PlanarYUVLuminanceSource(
         imageData,
           image.Width,
        image.Height,
            0, 0,
        image.Width,
  image.Height,
          false);

          // Try to decode
     var binarizer = new HybridBinarizer(source);
  var binaryBitmap = new BinaryBitmap(binarizer);
       
       var result = _reader.decode(binaryBitmap);

     if (result != null && !string.IsNullOrWhiteSpace(result.Text))
              {
           _lastScanTime = currentTime;
        Debug.WriteLine($"CameraQRScanner: QR code detected: {result.Text.Substring(0, Math.Min(50, result.Text.Length))}...");
      _onQRCodeDetected?.Invoke(result.Text);
    }
            }
   catch (ReaderException)
    {
         // No QR code found - this is expected most of the time
            }
            catch (Exception ex)
            {
 Debug.WriteLine($"CameraQRScanner: Analysis error: {ex.Message}");
   }
       finally
       {
                image?.Close();
        }
        }

 protected override void Dispose(bool disposing)
        {
     if (disposing)
     {
   // Reader doesn't need explicit cleanup
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Gets list of available cameras
    /// </summary>
    public async Task<List<CameraInfo>> GetAvailableCamerasAsync()
    {
        var cameras = new List<CameraInfo>();
        
   try
        {
 if (_cameraProvider == null)
       {
         var cameraProviderFuture = ProcessCameraProvider.GetInstance(_context);
       var executor = ContextCompat.GetMainExecutor(_context);
   var tcs = new TaskCompletionSource<ProcessCameraProvider>();

    cameraProviderFuture.AddListener(new Java.Lang.Runnable(() =>
          {
         try
     {
          var provider = (ProcessCameraProvider?)cameraProviderFuture.Get();
            if (provider != null)
 {
  tcs.TrySetResult(provider);
      }
        else
 {
           tcs.TrySetException(new InvalidOperationException("Camera provider is null"));
            }
  }
            catch (Exception ex)
            {
       tcs.TrySetException(ex);
 }
  }), executor);

   _cameraProvider = await tcs.Task;
            }

            if (_cameraProvider != null)
          {
          // Try back camera
        var backSelector = new CameraSelector.Builder()
    .RequireLensFacing(CameraSelector.LensFacingBack)
     .Build();
         
           if (_cameraProvider.HasCamera(backSelector))
    {
          cameras.Add(new CameraInfo
      {
        Name = "Back Camera",
      LensFacing = CameraSelector.LensFacingBack,
     IsDefault = true
         });
     }

                // Try front camera
  var frontSelector = new CameraSelector.Builder()
          .RequireLensFacing(CameraSelector.LensFacingFront)
           .Build();
         
            if (_cameraProvider.HasCamera(frontSelector))
         {
           cameras.Add(new CameraInfo
          {
     Name = "Front Camera",
   LensFacing = CameraSelector.LensFacingFront,
         IsDefault = false
       });
   }
            }

    Debug.WriteLine($"CameraQRScanner: Found {cameras.Count} cameras");
    }
        catch (Exception ex)
  {
     Debug.WriteLine($"CameraQRScanner: Error getting available cameras: {ex.Message}");
        }

        return cameras;
    }

    /// <summary>
    /// Checks if camera is healthy and recovers if needed
    /// </summary>
    public async Task<bool> CheckAndRecoverAsync()
    {
      try
        {
   if (_isDisposed)
       {
     Debug.WriteLine("CameraQRScanner: Cannot check - already disposed");
          return false;
      }

 if (_isPaused)
      {
       Debug.WriteLine("CameraQRScanner: Camera is paused, resuming");
     await ResumeAsync();
    return true;
            }

 if (!_isInitialized)
       {
                Debug.WriteLine("CameraQRScanner: Camera not initialized, starting");
    await StartAsync();
      return true;
 }

     // Check if camera is still bound
   if (_camera == null || _cameraProvider == null)
       {
     Debug.WriteLine("CameraQRScanner: Camera or provider is null, recovering");
       _isInitialized = false;
       await StartAsync();
    return true;
            }

    // Check if PreviewView is still valid
            if (_previewView == null || !_previewView.IsAttachedToWindow)
    {
     Debug.WriteLine("CameraQRScanner: PreviewView detached, recovering");
        _isInitialized = false;
  await StartAsync();
         return true;
    }

   Debug.WriteLine("CameraQRScanner: Camera is healthy");
    return true;
        }
        catch (Exception ex)
  {
        Debug.WriteLine($"CameraQRScanner: Recovery failed: {ex.Message}");
     return false;
        }
    }

    /// <summary>
/// Switches to specified camera
    /// </summary>
    public async Task SwitchCameraAsync(int lensFacing)
    {
  if (_isDisposed) return;

        Debug.WriteLine($"CameraQRScanner: Switching to camera with lens facing: {lensFacing}");
        
        _selectedCameraLensFacing = lensFacing;
    
      // Stop current camera
        if (_isInitialized)
 {
            await MainThread.InvokeOnMainThreadAsync(() =>
          {
            _cameraProvider?.UnbindAll();
            _isInitialized = false;
   });
   
            // Wait a bit for cleanup
            await Task.Delay(300);
     }

        // Restart with new camera
    await StartAsync();
    }

    /// <summary>
    /// Explicitly ensures PreviewView Surface is ready - call AFTER adding to container
    /// </summary>
    public async Task EnsureSurfaceReadyAsync()
    {
        if (_previewView == null)
   {
    Debug.WriteLine("CameraQRScanner: EnsureSurfaceReady - PreviewView is null");
            return;
        }

        Debug.WriteLine("CameraQRScanner: Ensuring Surface is ready AFTER container attachment");
   
// Wait for attachment
  int attachAttempts = 0;
        while (!_previewView.IsAttachedToWindow && attachAttempts < 50)
   {
  await Task.Delay(50);
  attachAttempts++;
     }

    Debug.WriteLine($"CameraQRScanner: Attached to window: {_previewView.IsAttachedToWindow} after {attachAttempts} attempts");

        if (!_previewView.IsAttachedToWindow)
      {
    Debug.WriteLine("CameraQRScanner: WARNING - PreviewView not attached to window");
      return;
        }

// Force multiple layout and rendering passes
       for (int i = 0; i < 5; i++)
  {
       await MainThread.InvokeOnMainThreadAsync(() =>
    {
       _previewView?.RequestLayout();
      _previewView?.ForceLayout();
  _previewView?.Invalidate();
  _previewView?.PostInvalidate();
   
    var parent = _previewView?.Parent as global::Android.Views.ViewGroup;
    parent?.RequestLayout();
    parent?.ForceLayout();
   parent?.Invalidate();
 parent?.PostInvalidate();
    });
 await Task.Delay(150);
  }

   // Wait specifically for SurfaceProvider
        int surfaceAttempts = 0;
      while (_previewView.SurfaceProvider == null && surfaceAttempts < 40)
        {
if (surfaceAttempts % 10 == 0)
     {
     Debug.WriteLine($"CameraQRScanner: Waiting for SurfaceProvider, attempt {surfaceAttempts}");
   }
     await Task.Delay(100);
   surfaceAttempts++;
 }

       Debug.WriteLine($"CameraQRScanner: SurfaceProvider ready: {_previewView.SurfaceProvider != null} after {surfaceAttempts} attempts");
 }

    /// <summary>
 /// Validates that camera feed is actually rendering
    /// </summary>
    public bool ValidateCameraFeed()
    {
    try
    {
            if (_previewView == null)
            {
          Debug.WriteLine("CameraQRScanner: Validate - PreviewView is null");
   return false;
        }

        if (!_previewView.IsAttachedToWindow)
      {
 Debug.WriteLine("CameraQRScanner: Validate - PreviewView not attached to window");
    return false;
   }

            if (_previewView.SurfaceProvider == null)
        {
           Debug.WriteLine("CameraQRScanner: Validate - SurfaceProvider is null");
   return false;
   }

    if (_camera == null)
      {
  Debug.WriteLine("CameraQRScanner: Validate - Camera is null");
       return false;
            }

        if (!_isInitialized)
    {
  Debug.WriteLine("CameraQRScanner: Validate - Not initialized");
      return false;
   }

  Debug.WriteLine("CameraQRScanner: ? Camera feed validation PASSED");
    Debug.WriteLine($"CameraQRScanner: - PreviewView size: {_previewView.Width}x{_previewView.Height}");
     Debug.WriteLine($"CameraQRScanner: - Surface attached: {_previewView.IsAttachedToWindow}");
      Debug.WriteLine($"CameraQRScanner: - Camera bound: {_camera != null}");
      return true;
        }
catch (Exception ex)
   {
     Debug.WriteLine($"CameraQRScanner: Validation error: {ex.Message}");
        return false;
    }
  }
}
