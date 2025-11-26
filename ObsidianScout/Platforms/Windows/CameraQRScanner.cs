using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Storage.Streams;
using ZXing;
using ZXing.Common;
using WinUIImage = Microsoft.UI.Xaml.Controls.Image;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIMedia = Microsoft.UI.Xaml.Media;

namespace ObsidianScout.Platforms.Windows
{
 public class CameraInfo
 {
 public string Name { get; set; } = string.Empty;
 public string DeviceId { get; set; } = string.Empty;
 public bool IsDefault { get; set; }
 }

 public class CameraQRScanner : IDisposable
 {
 private WinUIImage? _previewImage;
 private WriteableBitmap? _writeableBitmap;
 private MediaCapture? _mediaCapture;
 private MediaFrameReader? _frameReader;
 private readonly Action<string> _onQRCodeDetected;
 private bool _isDisposed;
 private readonly SemaphoreSlim _initLock = new(1,1);
 private readonly SemaphoreSlim _previewLock = new(1,1);
 private bool _isInitialized;
 private bool _isStarting;
 private string? _selectedCameraId;
 private bool _isPaused;
 private bool _wasInitializedBeforePause;
 private long _lastScanTime;
 private long _lastPreviewTime;
 private const int ThrottleMs =500;
 private const int PreviewThrottleMs =100;
 private readonly MultiFormatReader _reader;
 private SoftwareBitmapSource? _softwareBitmapSource; // kept around to avoid GC

 public CameraQRScanner(Action<string> onQRCodeDetected)
 {
 _onQRCodeDetected = onQRCodeDetected ?? throw new ArgumentNullException(nameof(onQRCodeDetected));

 _reader = new MultiFormatReader();
 var hints = new Dictionary<DecodeHintType, object>
 {
 { DecodeHintType.POSSIBLE_FORMATS, new List<BarcodeFormat> { BarcodeFormat.QR_CODE } },
 { DecodeHintType.TRY_HARDER, true }
 };
 _reader.Hints = hints;

 Debug.WriteLine("CameraQRScanner: Constructor initialized");
 }

 public WinUIImage? GetPreviewImage()
 {
 if (_previewImage == null)
 {
 Debug.WriteLine("CameraQRScanner: Creating Image on-demand");

 WinUIImage? imageToCreate = null;
 Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
 {
 try
 {
 imageToCreate = new WinUIImage
 {
 Stretch = WinUIMedia.Stretch.UniformToFill,
 HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
 VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch,
 Visibility = Microsoft.UI.Xaml.Visibility.Visible,
 Opacity =1.0
 };

 imageToCreate.RenderTransformOrigin = new global::Windows.Foundation.Point(0.5,0.5);
 imageToCreate.CacheMode = new Microsoft.UI.Xaml.Media.BitmapCache();

 // default placeholder
 _writeableBitmap = new WriteableBitmap(1920,1080);
 imageToCreate.Source = _writeableBitmap;

 _previewImage = imageToCreate;
 Debug.WriteLine("CameraQRScanner: Image created with WriteableBitmap");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Error creating Image: {ex.Message}");
 }
 }).Wait();
 }

 return _previewImage;
 }

 public async Task StartAsync()
 {
 await _initLock.WaitAsync();
 try
 {
 if (_isDisposed || _isInitialized || _isStarting)
 {
 Debug.WriteLine($"CameraQRScanner: Skip start - Disposed:{_isDisposed}, Initialized:{_isInitialized}, Starting:{_isStarting}");
 return;
 }

 _isStarting = true;
 _isPaused = false;
 Debug.WriteLine("CameraQRScanner: Starting camera initialization");

 if (_previewImage == null || _writeableBitmap == null)
 {
 Debug.WriteLine("CameraQRScanner: Creating preview image");
 await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
 {
 _previewImage = new WinUIImage
 {
 Stretch = WinUIMedia.Stretch.UniformToFill,
 HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
 VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch,
 Visibility = Microsoft.UI.Xaml.Visibility.Visible,
 Opacity =1.0
 };

 _previewImage.CacheMode = new Microsoft.UI.Xaml.Media.BitmapCache();

 _writeableBitmap = new WriteableBitmap(1920,1080);
 _previewImage.Source = _writeableBitmap;

 Debug.WriteLine("CameraQRScanner: Preview image created with WriteableBitmap");
 });
 }

 _mediaCapture = new MediaCapture();

 var settings = new MediaCaptureInitializationSettings
 {
 StreamingCaptureMode = StreamingCaptureMode.Video
 };

 if (!string.IsNullOrEmpty(_selectedCameraId))
 {
 settings.VideoDeviceId = _selectedCameraId;
 Debug.WriteLine($"CameraQRScanner: Using camera: {_selectedCameraId}");
 }

 await _mediaCapture.InitializeAsync(settings);
 Debug.WriteLine("CameraQRScanner: MediaCapture initialized");

 await SetupFrameReaderAsync();

 _isInitialized = true;
 Debug.WriteLine("CameraQRScanner: Camera started successfully");
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

 private async Task SetupFrameReaderAsync()
 {
 if (_mediaCapture == null)
 {
 Debug.WriteLine("CameraQRScanner: MediaCapture is null");
 return;
 }

 try
 {
 var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();
 var sourceGroup = frameSourceGroups.FirstOrDefault();

 if (sourceGroup == null)
 {
 Debug.WriteLine("CameraQRScanner: No frame source groups");
 return;
 }

 var colorSourceInfo = sourceGroup.SourceInfos
 .FirstOrDefault(info => info.SourceKind == MediaFrameSourceKind.Color);

 if (colorSourceInfo == null)
 {
 Debug.WriteLine("CameraQRScanner: No color frame source");
 return;
 }

 if (_mediaCapture.FrameSources.TryGetValue(colorSourceInfo.Id, out var frameSource))
 {
 var preferredFormat = frameSource.SupportedFormats
 .FirstOrDefault(format =>
 format.VideoFormat.Width >=640 &&
 (format.Subtype == "NV12" || format.Subtype == "YUY2" || format.Subtype == "MJPG"));

 if (preferredFormat != null)
 {
 await frameSource.SetFormatAsync(preferredFormat);
 Debug.WriteLine($"CameraQRScanner: Format set to {preferredFormat.Subtype} ({preferredFormat.VideoFormat.Width}x{preferredFormat.VideoFormat.Height})");
 }

 _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource, "BGRA8");
 _frameReader.FrameArrived += OnFrameArrived;
 await _frameReader.StartAsync();
 Debug.WriteLine("CameraQRScanner: Frame reader started");
 }
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Setup frame reader error: {ex.Message}");
 }
 }

 private async void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
 {
 if (_isPaused || _isDisposed)
 return;

 SoftwareBitmap? convertedBitmap = null;
 try
 {
 var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

 using var frame = sender.TryAcquireLatestFrame();
 if (frame?.VideoMediaFrame == null)
 return;

 var videoFrame = frame.VideoMediaFrame;
 var softwareBitmap = videoFrame.SoftwareBitmap;

 if (softwareBitmap == null)
 {
 var direct3DSurface = videoFrame.Direct3DSurface;
 if (direct3DSurface != null)
 {
 try
 {
 softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(direct3DSurface, BitmapAlphaMode.Premultiplied);
 }
 catch
 {
 return;
 }
 }
 else
 {
 return;
 }
 }

 if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
 {
 convertedBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
 }
 else
 {
 convertedBitmap = SoftwareBitmap.Copy(softwareBitmap);
 }

 if (currentTime - _lastPreviewTime >= PreviewThrottleMs)
 {
 _lastPreviewTime = currentTime;
 var bitmapForPreview = convertedBitmap;
 convertedBitmap = null;
 await UpdatePreviewAsync(bitmapForPreview!);
 }

 if (currentTime - _lastScanTime >= ThrottleMs)
 {
 _lastScanTime = currentTime;
 if (convertedBitmap != null)
 {
 var bitmapForQR = SoftwareBitmap.Copy(convertedBitmap);
 _ = ProcessFrameAsync(bitmapForQR);
 }
 else
 {
 var bitmapForQR = SoftwareBitmap.Copy(softwareBitmap);
 _ = ProcessFrameAsync(bitmapForQR);
 }
 }
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Frame error: {ex.Message}");
 }
 finally
 {
 convertedBitmap?.Dispose();
 }
 }

 private async Task UpdatePreviewAsync(SoftwareBitmap bitmap)
 {
 if (_previewImage == null)
 {
 bitmap?.Dispose();
 return;
 }

 if (!await _previewLock.WaitAsync(0))
 {
 bitmap?.Dispose();
 return;
 }

 SoftwareBitmap? bmpForSource = null;
 bool bmpForSourceIsConverted = false;

 try
 {
 await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
 {
 try
 {
 // Ensure bitmap format
 if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
 {
 Debug.WriteLine("CameraQRScanner: Converting SoftwareBitmap to BGRA8/Premultiplied for SoftwareBitmapSource");
 bmpForSource = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
 bmpForSourceIsConverted = true;
 // dispose original as we'll use the converted one
 bitmap.Dispose();
 }
 else
 {
 bmpForSource = bitmap;
 }

 if (_softwareBitmapSource == null)
 _softwareBitmapSource = new SoftwareBitmapSource();

 await _softwareBitmapSource.SetBitmapAsync(bmpForSource!);

 if (_previewImage.Source != _softwareBitmapSource)
 {
 _previewImage.Source = _softwareBitmapSource;
 }

 // Force layout passes
 _previewImage.InvalidateArrange();
 _previewImage.InvalidateMeasure();
 _previewImage.UpdateLayout();

 var parent = _previewImage.Parent as Microsoft.UI.Xaml.FrameworkElement;
 if (parent != null)
 {
 parent.InvalidateArrange();
 parent.InvalidateMeasure();
 parent.UpdateLayout();
 }

 Debug.WriteLine("CameraQRScanner: SoftwareBitmapSource frame rendered");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: SoftwareBitmapSource update failed: {ex.Message}");

 try
 {
 // Fallback to WriteableBitmap
 var targetBitmap = bmpForSource ?? bitmap;

 if (_writeableBitmap == null || _writeableBitmap.PixelWidth != targetBitmap.PixelWidth || _writeableBitmap.PixelHeight != targetBitmap.PixelHeight)
 {
 Debug.WriteLine($"CameraQRScanner: Fallback creating WriteableBitmap {targetBitmap.PixelWidth}x{targetBitmap.PixelHeight}");
 _writeableBitmap = new WriteableBitmap(targetBitmap.PixelWidth, targetBitmap.PixelHeight);
 _previewImage.Source = _writeableBitmap;
 }

 var pixelBuffer = new global::Windows.Storage.Streams.Buffer((uint)(targetBitmap.PixelWidth * targetBitmap.PixelHeight *4));
 targetBitmap.CopyToBuffer(pixelBuffer);
 var bytes = pixelBuffer.ToArray();

 try
 {
 for (int i =0; i +3 < bytes.Length; i +=4)
 {
 bytes[i +3] =255; // ensure opaque alpha
 }
 }
 catch { }

 using (var stream = _writeableBitmap.PixelBuffer.AsStream())
 {
 stream.Position =0;
 await stream.WriteAsync(bytes,0, bytes.Length);
 await stream.FlushAsync();
 }

 _writeableBitmap.Invalidate();
 Debug.WriteLine("CameraQRScanner: Fallback WriteableBitmap frame rendered");
 }
 catch (Exception fbEx)
 {
 Debug.WriteLine($"CameraQRScanner: Fallback update failed: {fbEx.Message}");
 }
 }
 finally
 {
 // Dispose converted bitmap if we created one inside
 if (bmpForSourceIsConverted && bmpForSource != null)
 {
 bmpForSource.Dispose();
 bmpForSource = null;
 }
 }
 });
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: UpdatePreviewAsync error: {ex.Message}");
 // ensure original bitmap disposed if not already
 if (!bmpForSourceIsConverted)
 {
 bitmap?.Dispose();
 bitmap = null;
 }
 }
 finally
 {
 // If we didn't convert the bitmap for source, dispose the original now
 if (!bmpForSourceIsConverted)
 {
 bitmap?.Dispose();
 }
 _previewLock.Release();
 }
 }

 private async Task ProcessFrameAsync(SoftwareBitmap bitmap)
 {
 try
 {
 int width = bitmap.PixelWidth;
 int height = bitmap.PixelHeight;
 int expectedSize = width * height *4; // BGRA8

 var pixelBuffer = new global::Windows.Storage.Streams.Buffer((uint)expectedSize);
 bitmap.CopyToBuffer(pixelBuffer);
 byte[] pixelData = pixelBuffer.ToArray();

 if (pixelData == null || pixelData.Length ==0)
 {
 Debug.WriteLine("CameraQRScanner: Failed to get pixel data");
 return;
 }

 var luminanceSource = new RGBLuminanceSource(pixelData, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
 var binarizer = new HybridBinarizer(luminanceSource);
 var binaryBitmap = new BinaryBitmap(binarizer);

 var result = _reader.decode(binaryBitmap);

 if (result != null && !string.IsNullOrWhiteSpace(result.Text))
 {
 Debug.WriteLine($"CameraQRScanner: QR detected: {result.Text.Substring(0, Math.Min(50, result.Text.Length))}");

 await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
 {
 _onQRCodeDetected?.Invoke(result.Text);
 });
 }
 }
 catch (ReaderException)
 {
 // No QR code found - expected
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: ProcessFrame error: {ex.Message}");
 // Save a diagnostic image for investigation on first failure
 await SaveSoftwareBitmapDiagnosticAsync(bitmap);
 }
 finally
 {
 bitmap?.Dispose();
 }
 }

 private static async Task SaveSoftwareBitmapDiagnosticAsync(SoftwareBitmap bitmap)
 {
 try
 {
 var folder = Path.GetTempPath();
 var filePath = Path.Combine(folder, $"CameraDebug_{DateTime.Now:yyyyMMdd_HHmmss}.png");

 using (var stream = new InMemoryRandomAccessStream())
 {
 var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
 encoder.SetSoftwareBitmap(bitmap);
 await encoder.FlushAsync();

 using (var fileStream = File.Create(filePath))
 {
 var outStream = stream.AsStreamForRead();
 outStream.Seek(0, SeekOrigin.Begin);
 await outStream.CopyToAsync(fileStream);
 }
 }

 Debug.WriteLine($"CameraQRScanner: Diagnostic saved to {filePath}");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Diagnostic save failed: {ex.Message}");
 }
 }

 public void Stop()
 {
 try
 {
 Debug.WriteLine("CameraQRScanner: Stopping");

 Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
 {
 try
 {
 if (_frameReader != null)
 {
 await _frameReader.StopAsync();
 _frameReader.FrameArrived -= OnFrameArrived;
 _frameReader.Dispose();
 _frameReader = null;
 }

 if (_mediaCapture != null)
 {
 try
 {
 await _mediaCapture.StopPreviewAsync();
 Debug.WriteLine("CameraQRScanner: MediaCapture preview stopped (Stop)");
 }
 catch { }
 _mediaCapture.Dispose();
 _mediaCapture = null;
 }

 _isInitialized = false;
 _isStarting = false;
 Debug.WriteLine("CameraQRScanner: Stopped");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Stop error: {ex.Message}");
 }
 });
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Stop error: {ex.Message}");
 }
 }

 public void Pause()
 {
 try
 {
 Debug.WriteLine("CameraQRScanner: Pausing");
 _wasInitializedBeforePause = _isInitialized;
 _isPaused = true;

 Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
 {
 try
 {
 if (_frameReader != null)
 {
 await _frameReader.StopAsync();
 }
 Debug.WriteLine("CameraQRScanner: Paused");
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

 public async Task ResumeAsync()
 {
 try
 {
 if (!_isPaused)
 {
 Debug.WriteLine("CameraQRScanner: Not paused");
 return;
 }

 Debug.WriteLine("CameraQRScanner: Resuming");
 _isPaused = false;

 if (_wasInitializedBeforePause && _frameReader != null && _mediaCapture != null)
 {
 await _frameReader.StartAsync();
 Debug.WriteLine("CameraQRScanner: Resumed");
 }
 else if (_wasInitializedBeforePause)
 {
 Debug.WriteLine("CameraQRScanner: Full restart needed");
 await StartAsync();
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
 Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
 {
 try
 {
 if (_mediaCapture?.VideoDeviceController?.TorchControl?.Supported == true)
 {
 _mediaCapture.VideoDeviceController.TorchControl.Enabled = on;
 Debug.WriteLine($"CameraQRScanner: Flashlight {(on ? "on" : "off")}");

 // Add a slight delay to allow the flashlight to turn on/off
 Task.Delay(500).ContinueWith(t =>
 {
 // Optionally, add any post-flashlight-toggle logic here
 });
 }
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Flashlight error: {ex.Message}");
 }
 });
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Flashlight error: {ex.Message}");
 }
 }

 public async Task<List<CameraInfo>> GetAvailableCamerasAsync()
 {
 var cameras = new List<CameraInfo>();

 try
 {
 var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

 foreach (var device in devices)
 {
 cameras.Add(new CameraInfo
 {
 Name = device.Name,
 DeviceId = device.Id,
 IsDefault = cameras.Count ==0
 });
 }

 Debug.WriteLine($"CameraQRScanner: Found {cameras.Count} cameras");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Get cameras error: {ex.Message}");
 }

 return cameras;
 }

 public async Task<bool> CheckAndRecoverAsync()
 {
 try
 {
 if (_isDisposed)
 {
 Debug.WriteLine("CameraQRScanner: Disposed");
 return false;
 }

 if (_isPaused)
 {
 Debug.WriteLine("CameraQRScanner: Resuming from pause");
 await ResumeAsync();
 return true;
 }

 if (!_isInitialized)
 {
 Debug.WriteLine("CameraQRScanner: Starting");
 await StartAsync();
 return true;
 }

 if (_mediaCapture == null || _previewImage == null)
 {
 Debug.WriteLine("CameraQRScanner: Recovering");
 _isInitialized = false;
 await StartAsync();
 return true;
 }

 Debug.WriteLine("CameraQRScanner: Healthy");
 return true;
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Recovery failed: {ex.Message}");
 return false;
 }
 }

 public async Task SwitchCameraAsync(string deviceId)
 {
 if (_isDisposed) return;

 Debug.WriteLine($"CameraQRScanner: Switching to: {deviceId}");

 _selectedCameraId = deviceId;

 if (_isInitialized)
 {
 Stop();
 await Task.Delay(500);
 }

 await StartAsync();
 }

 public bool ValidateCameraFeed()
 {
 try
 {
 if (_previewImage == null)
 {
 Debug.WriteLine("CameraQRScanner: PreviewImage null");
 return false;
 }

 if (_mediaCapture == null)
 {
 Debug.WriteLine("CameraQRScanner: MediaCapture null");
 return false;
 }

 if (!_isInitialized)
 {
 Debug.WriteLine("CameraQRScanner: Not initialized");
 return false;
 }

 Debug.WriteLine("CameraQRScanner: Validation passed");
 return true;
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Validation error: {ex.Message}");
 return false;
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

 Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
 {
 try
 {
 if (_frameReader != null)
 {
 _frameReader.FrameArrived -= OnFrameArrived;
 _frameReader.Dispose();
 _frameReader = null;
 }

 if (_mediaCapture != null)
 {
 _mediaCapture.Dispose();
 _mediaCapture = null;
 }

 _writeableBitmap = null;

 Debug.WriteLine("CameraQRScanner: Disposed");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: Dispose error: {ex.Message}");
 }
 });

 _initLock.Dispose();
 _previewLock.Dispose();
 GC.SuppressFinalize(this);
 }
 
 // Provide an ImageBrush that uses the SoftwareBitmapSource for setting as panel background
 public async Task<Microsoft.UI.Xaml.Media.ImageBrush?> GetPreviewBrushAsync()
 {
 Microsoft.UI.Xaml.Media.ImageBrush? brush = null;
 await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
 {
 try
 {
 if (_softwareBitmapSource == null)
 {
 _softwareBitmapSource = new SoftwareBitmapSource();
 }
 
 brush = new Microsoft.UI.Xaml.Media.ImageBrush()
 {
 ImageSource = _softwareBitmapSource,
 Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
 AlignmentX = Microsoft.UI.Xaml.Media.AlignmentX.Center,
 AlignmentY = Microsoft.UI.Xaml.Media.AlignmentY.Center
 };
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"CameraQRScanner: GetPreviewBrushAsync error: {ex.Message}");
 }
 });

 return brush;
 }
 }
}
