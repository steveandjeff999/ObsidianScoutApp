using ObsidianScout.ViewModels;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using System;

#if ANDROID
using ObsidianScout.Platforms.Android;
#elif WINDOWS
using ObsidianScout.Platforms.Windows;
using Microsoft.UI.Xaml.Controls;
#endif

namespace ObsidianScout.Views;

public partial class QRCodeScannerPage : ContentPage
{
 private readonly QRCodeScannerViewModel _viewModel;

#if ANDROID
 private CameraQRScanner? _androidScanner;
 private bool _cameraInitialized;
 private bool _isInitializing;
 private List<CameraInfo>? _availableCameras; // Store camera list to prevent picker from clearing
#elif WINDOWS
 private CameraQRScanner? _windowsScanner;
 private bool _cameraInitialized;
 private bool _isInitializing;
private List<CameraInfo>? _availableCameras;
#endif

 public QRCodeScannerPage(QRCodeScannerViewModel viewModel)
 {
 _viewModel = viewModel;
 BindingContext = _viewModel;
 InitializeComponent();
 _viewModel.StatusMessage = "QR Scanner Ready";

 // Size changed handler to adapt camera area
 this.SizeChanged += QRCodeScannerPage_SizeChanged;
 }

 private void QRCodeScannerPage_SizeChanged(object sender, EventArgs e)
 {
 try
 {
 var screenWidth = this.Width; // Page width
 if (double.IsNaN(screenWidth) || screenWidth <=0) return;

 // Use90% of available width for camera container but cap to a max (e.g.,800)
 var desiredWidth = Math.Min(800, screenWidth *0.9);

 CameraContainer.WidthRequest = desiredWidth;

 // Frame should be slightly smaller than container (82% of width) to leave padding
 var frameSize = desiredWidth *0.82;
 var frame = this.FindByName<Microsoft.Maui.Controls.Border>("FrameBorder");
 if (frame != null)
 {
 frame.WidthRequest = frameSize;
 frame.HeightRequest = frameSize;
 }

 // Also adjust CameraSelectorBorder width to match container (use FindByName to be safe)
 var selector = this.FindByName<Microsoft.Maui.Controls.Border>("CameraSelectorBorder");
 if (selector != null)
 {
 selector.WidthRequest = desiredWidth;
 }

 Debug.WriteLine($"QRCodeScannerPage: SizeChanged - pageWidth={screenWidth}, container={desiredWidth}, frame={frameSize}");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: SizeChanged error: {ex.Message}");
 }
 }

 protected override async void OnAppearing()
 {
 base.OnAppearing();
 Debug.WriteLine("QRCodeScannerPage: OnAppearing called");

#if ANDROID
 if (!_cameraInitialized && !_isInitializing)
 {
 Debug.WriteLine("QRCodeScannerPage: First-time initialization");
 await InitializeCameraAsync();
 }
 else if (_cameraInitialized && _androidScanner != null)
 {
 Debug.WriteLine("QRCodeScannerPage: Camera already initialized, checking health and resuming");
 
 // Small delay to ensure UI is fully rendered before camera operations
 await Task.Delay(300);
 
 // Check camera health and recover if needed
 var isHealthy = await _androidScanner.CheckAndRecoverAsync();
 
 if (isHealthy)
 {
 _viewModel.IsScanning = true;
 _viewModel.StatusMessage = "Position QR code within frame";
 }
 else
 {
 _viewModel.StatusMessage = "Camera recovery failed - try restarting scanner";
 Debug.WriteLine("QRCodeScannerPage: Camera health check failed");
 }
 }
#elif WINDOWS
 if (!_cameraInitialized && !_isInitializing)
 {
 Debug.WriteLine("QRCodeScannerPage: First-time initialization (Windows)");
 await InitializeCameraAsync();
 }
 else if (_cameraInitialized && _windowsScanner != null)
 {
 Debug.WriteLine("QRCodeScannerPage: Camera already initialized, checking health and resuming (Windows)");
 
 await Task.Delay(300);
 
 var isHealthy = await _windowsScanner.CheckAndRecoverAsync();
 
 if (isHealthy)
 {
 _viewModel.IsScanning = true;
 _viewModel.StatusMessage = "Position QR code within frame";
 }
 else
 {
 _viewModel.StatusMessage = "Camera recovery failed - try restarting scanner";
 Debug.WriteLine("QRCodeScannerPage: Camera health check failed (Windows)");
 }
 }
#else
 _viewModel.StatusMessage = "Camera scanning available on Android and Windows";
#endif
 }

 protected override void OnDisappearing()
 {
 base.OnDisappearing();
 Debug.WriteLine("QRCodeScannerPage: OnDisappearing called");

#if ANDROID
 try
{
 // Pause instead of stop to keep preview visible
 _androidScanner?.Pause();
 _viewModel.IsScanning = false;
 Debug.WriteLine("QRCodeScannerPage: Camera paused (preview maintained)");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: Error pausing camera: {ex.Message}");
 }
#elif WINDOWS
 try
 {
 _windowsScanner?.Pause();
 _viewModel.IsScanning = false;
 Debug.WriteLine("QRCodeScannerPage: Camera paused (Windows)");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: Error pausing camera (Windows): {ex.Message}");
 }
#endif
 }

 // Clean up when page is being destroyed
 protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
 {
 base.OnNavigatedFrom(args);
 Debug.WriteLine("QRCodeScannerPage: OnNavigatedFrom called");
 
#if ANDROID
 // Unsubscribe from events to prevent memory leaks
 if (_viewModel != null)
 {
 _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
 }
#elif WINDOWS
 if (_viewModel != null)
 {
 _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
 }
#endif
 }

#if ANDROID
 private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
 {
 if (e.PropertyName == nameof(QRCodeScannerViewModel.IsFlashlightOn))
 {
 try
 {
 _androidScanner?.ToggleFlashlight(_viewModel.IsFlashlightOn);
 Debug.WriteLine($"QRCodeScannerPage: Flashlight toggled to {_viewModel.IsFlashlightOn}");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: Error toggling flashlight: {ex.Message}");
 }
 }
 }

 private async Task InitializeCameraAsync()
 {
 if (_isInitializing)
 {
 Debug.WriteLine("QRCodeScannerPage: Already initializing, skipping");
 return;
 }

 _isInitializing = true;

 try
 {
 Debug.WriteLine("QRCodeScannerPage: Checking camera permissions");
 
 var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
 if (status != PermissionStatus.Granted)
 {
 Debug.WriteLine("QRCodeScannerPage: Requesting camera permission");
 status = await Permissions.RequestAsync<Permissions.Camera>();
 }

 if (status != PermissionStatus.Granted)
 {
 _viewModel.StatusMessage = "Camera permission denied - Paste QR data manually";
 await DisplayAlert("Permission Required", 
 "Camera permission is required to scan QR codes. You can still paste QR data manually below.", 
 "OK");
 return;
 }

 Debug.WriteLine("QRCodeScannerPage: Camera permission granted");
 _viewModel.StatusMessage = "Initializing camera...";

 var context = Platform.CurrentActivity;
 if (context == null)
 {
 _viewModel.StatusMessage = "Camera not available - Activity is null";
 Debug.WriteLine("QRCodeScannerPage: Platform.CurrentActivity is null");
 return;
 }

 // Only create scanner once
 if (_androidScanner == null)
 {
 Debug.WriteLine("QRCodeScannerPage: Creating new camera scanner");
 _androidScanner = new CameraQRScanner(context, (qrCode) =>
 {
 MainThread.BeginInvokeOnMainThread(() =>
 {
 Debug.WriteLine($"QRCodeScannerPage: QR code detected, length: {qrCode?.Length ??0}");
 _viewModel.OnQRCodeDetected(qrCode);
 });
 });

 // Subscribe to flashlight toggle
 _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
 _viewModel.PropertyChanged += OnViewModelPropertyChanged;
 
 // Populate camera picker
 await PopulateCameraPickerAsync();
 }

 // CRITICAL: Ensure CameraContainer has explicit size before proceeding
 Debug.WriteLine($"QRCodeScannerPage: Container initial size: {CameraContainer.Width}x{CameraContainer.Height}");
 
 // Force container to measure itself
 CameraContainer.InvalidateMeasure();
 await Task.Delay(100);
 
 if (CameraContainer.Width <=0 || CameraContainer.Height <=0)
 {
 Debug.WriteLine("QRCodeScannerPage: Waiting for container to be laid out...");
 var layoutTcs = new TaskCompletionSource<bool>();
 
 EventHandler sizeChangedHandler = null!;
 sizeChangedHandler = (s, e) =>
 {
 if (CameraContainer.Width >0 && CameraContainer.Height >0)
 {
 Debug.WriteLine($"QRCodeScannerPage: Container sized: {CameraContainer.Width}x{CameraContainer.Height}");
 CameraContainer.SizeChanged -= sizeChangedHandler;
 layoutTcs.TrySetResult(true);
 }
 };
 
 CameraContainer.SizeChanged += sizeChangedHandler;
 
 // Wait up to5 seconds for layout
 var layoutTimeoutTask = Task.Delay(5000);
 var layoutCompletedTask = await Task.WhenAny(layoutTcs.Task, layoutTimeoutTask);
 
 if (layoutCompletedTask == layoutTimeoutTask)
 {
 Debug.WriteLine("QRCodeScannerPage: Timeout waiting for container layout");
 CameraContainer.SizeChanged -= sizeChangedHandler;
 _viewModel.StatusMessage = "Failed to initialize camera view";
 await DisplayAlert("Error", "Camera view layout timeout. Please try again.", "OK");
 return;
 }
 
 Debug.WriteLine($"QRCodeScannerPage: Container ready: {CameraContainer.Width}x{CameraContainer.Height}");
 }

 // Get preview view
 var previewView = _androidScanner.GetPreviewView();
 if (previewView == null)
 {
 _viewModel.StatusMessage = "Camera preview not available";
 Debug.WriteLine("QRCodeScannerPage: PreviewView is null after creation");
 return;
 }

 Debug.WriteLine($"QRCodeScannerPage: PreviewView obtained: {previewView != null}");

 // Wait for handler to be available
 int attempts =0;
 while (CameraContainer.Handler == null && attempts <100)
 {
 await Task.Delay(50);
 attempts++;
 }

 if (CameraContainer.Handler?.PlatformView is not Android.Views.ViewGroup viewGroup)
 {
 _viewModel.StatusMessage = "Failed to attach camera preview";
 Debug.WriteLine("QRCodeScannerPage: Failed to get ViewGroup from Handler");
 return;
 }

 Debug.WriteLine($"QRCodeScannerPage: ViewGroup obtained after {attempts} attempts");

 // Add camera preview to container with explicit dimensions
 if (viewGroup.ChildCount ==0 || viewGroup.GetChildAt(0) != previewView)
 {
 // Don't remove all views - this removes MAUI's overlay elements!
 // Instead, check if PreviewView exists and remove only it
 for (int i =0; i < viewGroup.ChildCount; i++)
 {
 if (viewGroup.GetChildAt(i) is AndroidX.Camera.View.PreviewView)
 {
 viewGroup.RemoveViewAt(i);
 Debug.WriteLine("QRCodeScannerPage: Removed existing PreviewView");
 break;
 }
 }

 // Get container dimensions - use actual measured dimensions
 var containerWidth = viewGroup.MeasuredWidth >0 ? viewGroup.MeasuredWidth : viewGroup.Width;
 var containerHeight = viewGroup.MeasuredHeight >0 ? viewGroup.MeasuredHeight : viewGroup.Height;

 // Fallback to reasonable defaults if still zero
 if (containerWidth <=0)
 {
 containerWidth = (int)(DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density);
 }
 if (containerHeight <=0)
 {
 containerHeight = (int)(DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density *0.6);
 }

 Debug.WriteLine($"QRCodeScannerPage: Using container dimensions: {containerWidth}x{containerHeight}");

 // Create layout params with EXACT dimensions (not MatchParent)
 var layoutParams = new Android.Views.ViewGroup.LayoutParams(
 containerWidth,
 containerHeight);

 previewView.LayoutParameters = layoutParams;

 // CRITICAL: Add PreviewView at index0 (back) so MAUI elements stay on top
 // Set Z-order to -1 to ensure it's behind everything
 previewView.SetZ(-1f);
 viewGroup.AddView(previewView,0, layoutParams);
 Debug.WriteLine("QRCodeScannerPage: PreviewView added at index0 with Z=-1 (behind MAUI elements)");

 // CRITICAL FIX: Explicitly ensure Surface is ready AFTER adding to container
 Debug.WriteLine("QRCodeScannerPage: Waiting for Surface to be ready...");
 await _androidScanner.EnsureSurfaceReadyAsync();
 Debug.WriteLine("QRCodeScannerPage: Surface ready check complete");

 // Force immediate layout pass multiple times
 for (int i =0; i <3; i++)
 {
 await Task.Delay(100);
 previewView.RequestLayout();
 previewView.ForceLayout();
 viewGroup.RequestLayout();
 viewGroup.ForceLayout();
 }

 await Task.Delay(200);

 // Measure and layout PreviewView explicitly
 var widthSpec = Android.Views.View.MeasureSpec.MakeMeasureSpec(
 containerWidth,
 Android.Views.MeasureSpecMode.Exactly);
 var heightSpec = Android.Views.View.MeasureSpec.MakeMeasureSpec(
 containerHeight,
 Android.Views.MeasureSpecMode.Exactly);

 previewView.Measure(widthSpec, heightSpec);
 previewView.Layout(0,0, previewView.MeasuredWidth, previewView.MeasuredHeight);

 Debug.WriteLine($"QRCodeScannerPage: PreviewView measured and laid out: {previewView.MeasuredWidth}x{previewView.MeasuredHeight}");
 await Task.Delay(200);

 Debug.WriteLine($"QRCodeScannerPage: Container size: {viewGroup.Width}x{viewGroup.Height}");
 Debug.WriteLine($"QRCodeScannerPage: PreviewView size: {previewView.Width}x{previewView.Height}");
 Debug.WriteLine($"QRCodeScannerPage: PreviewView measured size: {previewView.MeasuredWidth}x{previewView.MeasuredHeight}");
 Debug.WriteLine($"QRCodeScannerPage: ViewGroup child count: {viewGroup.ChildCount}");

 // Wait for PreviewView to have valid dimensions
 int sizeAttempts =0;
 while ((previewView.Width <=0 || previewView.Height <=0) && sizeAttempts <30)
 {
 await Task.Delay(100);
 sizeAttempts++;

 if (sizeAttempts %5 ==0)
 {
 // Force layout again
 previewView.RequestLayout();
 previewView.ForceLayout();
 Debug.WriteLine($"QRCodeScannerPage: Retry {sizeAttempts}: {previewView.Width}x{previewView.Height}");
 }
 }

 if (previewView.Width <=0 || previewView.Height <=0)
 {
 Debug.WriteLine($"QRCodeScannerPage: WARNING - PreviewView still has no size: {previewView.Width}x{previewView.Height}");
 // Force explicit size one more time
 previewView.Layout(0,0, containerWidth, containerHeight);
 await Task.Delay(200);
 }

 Debug.WriteLine($"QRCodeScannerPage: Final PreviewView size: {previewView.Width}x{previewView.Height} after {sizeAttempts} attempts");
 }
 else
 {
 Debug.WriteLine("QRCodeScannerPage: PreviewView already in container");
 }

// Start camera with timeout
 Debug.WriteLine("QRCodeScannerPage: Starting camera");
var startTask = _androidScanner.StartAsync();
 var timeoutTask = Task.Delay(20000); // Increased timeout

 var completedTask = await Task.WhenAny(startTask, timeoutTask);

 if (completedTask == timeoutTask)
 {
 _viewModel.StatusMessage = "Camera initialization timed out";
 Debug.WriteLine("QRCodeScannerPage: Camera initialization timed out");
 await DisplayAlert("Timeout", 
 "Camera initialization took too long. Please try restarting the scanner.", 
 "OK");
 return;
 }

 if (startTask.IsFaulted)
 {
 var ex = startTask.Exception?.GetBaseException();
 _viewModel.StatusMessage = $"Camera error: {ex?.Message ?? "Unknown error"}";
 Debug.WriteLine($"QRCodeScannerPage: Camera start failed: {ex?.Message}\n{ex?.StackTrace}");
 await DisplayAlert("Camera Error", 
 "Failed to initialize camera: " + (ex?.Message ?? "unknown error"), 
 "OK");
 return;
 }

 await startTask;

 _viewModel.StatusMessage = "Position QR code within frame";
 _viewModel.IsScanning = true;
 _cameraInitialized = true;
 
 Debug.WriteLine("QRCodeScannerPage: Camera initialization complete");
 
// CRITICAL: Validate camera feed is actually working
 await Task.Delay(500); // Wait for first frame
 var isValid = _androidScanner.ValidateCameraFeed();
 if (!isValid)
 {
 Debug.WriteLine("QRCodeScannerPage: ?? WARNING - Camera feed validation FAILED! Attempting recovery...");
 _viewModel.StatusMessage = "Camera feed issue detected - recovering...";
 
 // Attempt recovery by restarting camera
 try
 {
 await Task.Delay(500);
 _cameraInitialized = false;
 _androidScanner.Stop();
 await Task.Delay(1000);
 
 Debug.WriteLine("QRCodeScannerPage: Attempting camera restart for recovery");
 await _androidScanner.StartAsync();
 await Task.Delay(500);
 
 // Validate again
 isValid = _androidScanner.ValidateCameraFeed();
 if (isValid)
 {
 Debug.WriteLine("QRCodeScannerPage: ? Camera feed recovered successfully!");
 _viewModel.StatusMessage = "Position QR code within frame";
 _cameraInitialized = true;
 }
 else
 {
 Debug.WriteLine("QRCodeScannerPage: ? Camera feed recovery failed");
 _viewModel.StatusMessage = "Camera feed not working - try restarting app";
 await DisplayAlert("Camera Issue", 
 "Camera feed could not be initialized. Please restart the app or check camera permissions.", 
 "OK");
 }
 }
 catch (Exception recoveryEx)
 {
 Debug.WriteLine($"QRCodeScannerPage: Recovery attempt failed: {recoveryEx.Message}");
_viewModel.StatusMessage = "Camera recovery failed - restart app";
 }
 }
 else
 {
 Debug.WriteLine("QRCodeScannerPage: ? Camera feed validation PASSED!");
 }
 }
 catch (Exception ex)
 {
 _viewModel.StatusMessage = $"Camera error: {ex.Message}";
 Debug.WriteLine($"QRCodeScannerPage: InitializeCameraAsync error: {ex.Message}\n{ex.StackTrace}");

 await DisplayAlert("Error", 
 $"An error occurred while initializing the camera: {ex.Message}", 
 "OK");
 }
 finally
 {
 _isInitializing = false;
}
 }

 private async Task PopulateCameraPickerAsync()
 {
 try
 {
 Debug.WriteLine("QRCodeScannerPage: Populating camera picker");
 
 var cameras = await _androidScanner!.GetAvailableCamerasAsync();
_availableCameras = cameras; // Store for later use
 
 if (cameras.Count >0)
 {
 var cameraNames = cameras.Select(c => c.Name).ToList();
 CameraPicker.ItemsSource = cameraNames;
 
 // Select default camera (back camera)
 var defaultIndex = cameras.FindIndex(c => c.IsDefault);
 if (defaultIndex >=0)
 {
 CameraPicker.SelectedIndex = defaultIndex;
 }
 else
 {
 CameraPicker.SelectedIndex =0;
 }
 
 Debug.WriteLine($"QRCodeScannerPage: Found {cameras.Count} cameras, selected index {CameraPicker.SelectedIndex}");
 
 // Ensure camera selector stays visible
 MainThread.BeginInvokeOnMainThread(() =>
 {
 CameraSelectorBorder.IsVisible = true;
 });
 }
 else
 {
 Debug.WriteLine("QRCodeScannerPage: No cameras found");
 CameraSelectorBorder.IsVisible = false;
 }
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: Error populating camera picker: {ex.Message}");
 // Don't hide selector on error if we already have cameras
 if (_availableCameras == null || _availableCameras.Count ==0)
 {
 CameraSelectorBorder.IsVisible = false;
 }
 }
 }

 private async void OnCameraSelectionChanged(object? sender, EventArgs e)
 {
 if (_androidScanner == null || CameraPicker.SelectedIndex <0 || _availableCameras == null)
 return;

 try
 {
 Debug.WriteLine($"QRCodeScannerPage: Camera selection changed to index {CameraPicker.SelectedIndex}");
 
 if (CameraPicker.SelectedIndex < _availableCameras.Count)
 {
 var selectedCamera = _availableCameras[CameraPicker.SelectedIndex];
 Debug.WriteLine($"QRCodeScannerPage: Switching to {selectedCamera.Name}");
 
 _viewModel.StatusMessage = $"Switching to {selectedCamera.Name}...";
 
 await _androidScanner.SwitchCameraAsync(selectedCamera.LensFacing);
 
 _viewModel.StatusMessage = "Position QR code within frame";
 }
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: Error switching camera: {ex.Message}");
 _viewModel.StatusMessage = $"Failed to switch camera: {ex.Message}";
 await DisplayAlert("Error", $"Failed to switch camera: {ex.Message}", "OK");
 }
 }
#endif

#if WINDOWS
 private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
 {
 if (e.PropertyName == nameof(QRCodeScannerViewModel.IsFlashlightOn))
 {
 try
 {
 _windowsScanner?.ToggleFlashlight(_viewModel.IsFlashlightOn);
 Debug.WriteLine($"QRCodeScannerPage: Flashlight toggled to {_viewModel.IsFlashlightOn} (Windows)");
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: Error toggling flashlight (Windows): {ex.Message}");
 }
 }
 }

 private async Task InitializeCameraAsync()
 {
 if (_isInitializing)
 {
 Debug.WriteLine("QRCodeScannerPage: Already initializing, skipping (Windows)");
 return;
 }

 _isInitializing = true;

 try
 {
 Debug.WriteLine("QRCodeScannerPage: Checking camera permissions (Windows)");
 
 // Windows automatically handles camera permissions
 _viewModel.StatusMessage = "Initializing camera...";

 // Only create scanner once
 if (_windowsScanner == null)
 {
Debug.WriteLine("QRCodeScannerPage: Creating new camera scanner (Windows)");
 _windowsScanner = new CameraQRScanner((qrCode) =>
 {
 MainThread.BeginInvokeOnMainThread(() =>
 {
 Debug.WriteLine($"QRCodeScannerPage: QR code detected, length: {qrCode?.Length ??0} (Windows)");
 _viewModel.OnQRCodeDetected(qrCode);
 });
 });

 // Subscribe to flashlight toggle
 _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
 _viewModel.PropertyChanged += OnViewModelPropertyChanged;
 
 // Populate camera picker
 await PopulateCameraPickerAsync();
 }

 // Ensure CameraContainer has explicit size
 Debug.WriteLine($"QRCodeScannerPage: Container initial size: {CameraContainer.Width}x{CameraContainer.Height} (Windows)");
 
 CameraContainer.InvalidateMeasure();
 await Task.Delay(100);
 
 if (CameraContainer.Width <=0 || CameraContainer.Height <=0)
 {
 Debug.WriteLine("QRCodeScannerPage: Waiting for container to be laid out... (Windows)");
 var layoutTcs = new TaskCompletionSource<bool>();

 EventHandler sizeChangedHandler = null!;
 sizeChangedHandler = (s, e) =>
 {
 if (CameraContainer.Width >0 && CameraContainer.Height >0)
 {
 Debug.WriteLine($"QRCodeScannerPage: Container sized: {CameraContainer.Width}x{CameraContainer.Height} (Windows)");
 CameraContainer.SizeChanged -= sizeChangedHandler;
 layoutTcs.TrySetResult(true);
 }
 };
 
 CameraContainer.SizeChanged += sizeChangedHandler;
 
 var layoutTimeoutTask = Task.Delay(5000);
 var layoutCompletedTask = await Task.WhenAny(layoutTcs.Task, layoutTimeoutTask);
 
 if (layoutCompletedTask == layoutTimeoutTask)
 {
 Debug.WriteLine("QRCodeScannerPage: Timeout waiting for container layout (Windows)");
 CameraContainer.SizeChanged -= sizeChangedHandler;
 _viewModel.StatusMessage = "Failed to initialize camera view";
 await DisplayAlert("Error", "Camera view layout timeout. Please try again.", "OK");
 return;
 }
 
 Debug.WriteLine($"QRCodeScannerPage: Container ready: {CameraContainer.Width}x{CameraContainer.Height} (Windows)");
 }

 // Get preview Image control
 var previewImage = _windowsScanner.GetPreviewImage();
 if (previewImage == null)
 {
 _viewModel.StatusMessage = "Camera preview not available";
 Debug.WriteLine("QRCodeScannerPage: Preview Image is null after creation (Windows)");
 return;
 }

 Debug.WriteLine($"QRCodeScannerPage: Preview Image obtained: {previewImage != null} (Windows)");

 // Wait for handler to be available
 int attempts =0;
 while (CameraContainer.Handler == null && attempts <100)
 {
 await Task.Delay(50);
 attempts++;
 }

 if (CameraContainer.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.Panel panel)
 {
 _viewModel.StatusMessage = "Failed to attach camera preview";
 Debug.WriteLine("QRCodeScannerPage: Failed to get Panel from Handler (Windows)");
 return;
 }

 Debug.WriteLine($"QRCodeScannerPage: Panel obtained after {attempts} attempts (Windows)");

 // Add camera preview to container
 await MainThread.InvokeOnMainThreadAsync(async () =>
 {
 // Remove existing preview images
 var existingImage = panel.Children.OfType<Microsoft.UI.Xaml.Controls.Image>().FirstOrDefault();
 if (existingImage != null)
 {
 panel.Children.Remove(existingImage);
 Debug.WriteLine("QRCodeScannerPage: Removed existing preview Image (Windows)");
 }

 // Ensure panel background does not obscure native preview and try ImageBrush backed by SoftwareBitmapSource
 try
 {
 // First set transparent filler to avoid occlusion
 panel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

 // Try to get an ImageBrush from the Windows scanner which uses the SoftwareBitmapSource
 try
 {
 var brush = await _windowsScanner!.GetPreviewBrushAsync();
 if (brush != null)
 {
 // assign brush to panel background so native frames are visible as background
 panel.Background = brush;
 Debug.WriteLine("QRCodeScannerPage: Panel background set to ImageBrush (Windows)");
 }
 }
 catch (Exception exBrush)
 {
 Debug.WriteLine($"QRCodeScannerPage: Failed to set ImageBrush background: {exBrush.Message}");
 }
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: Failed to set panel background transparent: {ex.Message}");
 }

 // Get panel dimensions for explicit sizing
 var panelWidth = panel.ActualWidth >0 ? panel.ActualWidth : CameraContainer.Width;
 var panelHeight = panel.ActualHeight >0 ? panel.ActualHeight : CameraContainer.Height;
 
 // Fallback to reasonable defaults if still zero
 if (panelWidth <=0) panelWidth =800;
 if (panelHeight <=0) panelHeight =360;

 Debug.WriteLine($"QRCodeScannerPage: Target Image dimensions: {panelWidth}x{panelHeight} (Windows)");

 // CRITICAL FIX: ADD IMAGE TO VISUAL TREE FIRST
 // Set ZIndex BEFORE adding
 Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(previewImage, -1000);
 
 // Prevent the preview image from intercepting input meant for MAUI overlays
 previewImage.IsHitTestVisible = false;
 
 // Add at index0 (BACK)
 panel.Children.Insert(0, previewImage);
 Debug.WriteLine($"QRCodeScannerPage: Image added to panel (children: {panel.Children.Count}) (Windows)");
 
 // CRITICAL: Multiple async layout passes
 for (int i =0; i <3; i++)
 {
 panel.InvalidateMeasure();
 panel.InvalidateArrange();
 panel.UpdateLayout();
 await Task.Delay(50); // Async delay
 }
 
 // NOW set all dimensions and properties AFTER Image is in tree
 previewImage.Width = panelWidth;
 previewImage.Height = panelHeight;
 
 // CRITICAL: MinWidth/MinHeight as fallback
 previewImage.MinWidth = panelWidth;
 previewImage.MinHeight = panelHeight;
 
 // CRITICAL: MaxWidth/MaxHeight to prevent collapse
 previewImage.MaxWidth = panelWidth;
 previewImage.MaxHeight = panelHeight;
 
 // Set alignment and stretch
 previewImage.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
 previewImage.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch;
 previewImage.Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill;
 
 // CRITICAL: Visibility and opacity
 previewImage.Opacity =1.0;
 previewImage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
 
 Debug.WriteLine("QRCodeScannerPage: Image properties set (Windows)");

 // CRITICAL: Force Image to measure and arrange with EXACT dimensions
 // Use WinUI's async Measure/Arrange
 previewImage.Measure(new Windows.Foundation.Size(panelWidth, panelHeight));
 previewImage.Arrange(new Windows.Foundation.Rect(0,0, panelWidth, panelHeight));
 
 // CRITICAL: Multiple layout passes with async delays
 for (int i =0; i <5; i++)
 {
 panel.InvalidateMeasure();
 panel.InvalidateArrange();
 panel.UpdateLayout();

 // Force Image layout too
 previewImage.InvalidateMeasure();
 previewImage.InvalidateArrange();
 previewImage.UpdateLayout();

 await Task.Delay(100); // Async delay

 // Check if ActualSize is now valid
 if (previewImage.ActualWidth >0 && previewImage.ActualHeight >0)
 {
 Debug.WriteLine($"QRCodeScannerPage: Image measured successfully on pass {i +1} (Windows)");
 break;
 }
 }

 // Verify final state
 var imageInTree = panel.Children.Contains(previewImage);
 Debug.WriteLine($"QRCodeScannerPage: Image in visual tree: {imageInTree} (Windows)");
 Debug.WriteLine($"QRCodeScannerPage: Panel size: {panel.ActualWidth}x{panel.ActualHeight} (Windows)");
 Debug.WriteLine($"QRCodeScannerPage: Image actual size: {previewImage.ActualWidth}x{previewImage.ActualHeight} (Windows)");
 Debug.WriteLine($"QRCodeScannerPage: Image desired size: {previewImage.DesiredSize.Width}x{previewImage.DesiredSize.Height} (Windows)");
 
 // CRITICAL: If ActualSize is still0, force with explicit RenderSize
 if (previewImage.ActualWidth ==0 || previewImage.ActualHeight ==0)
 {
 Debug.WriteLine("QRCodeScannerPage: WARNING - ActualSize still0, forcing RenderSize (Windows)");

 // Last resort: Set clip to force size
 previewImage.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry
 {
 Rect = new Windows.Foundation.Rect(0,0, panelWidth, panelHeight)
 };

 // Force final layout
 await Task.Delay(100);
 panel.UpdateLayout();
 previewImage.UpdateLayout();

 Debug.WriteLine($"QRCodeScannerPage: After clip - Image size: {previewImage.ActualWidth}x{previewImage.ActualHeight} (Windows)");
 }
 });

 // Start camera (Windows)
 Debug.WriteLine("QRCodeScannerPage: Starting camera (Windows)");
 
 var startTask = _windowsScanner.StartAsync();
 var timeoutTask = Task.Delay(20000);
 
 var completedTask = await Task.WhenAny(startTask, timeoutTask);
 
 if (completedTask == timeoutTask)
 {
 _viewModel.IsScanning = false;
 _viewModel.StatusMessage = "Camera initialization timed out";
 Debug.WriteLine("QRCodeScannerPage: Camera initialization timed out (Windows)");
 await DisplayAlert("Timeout", 
 "Camera initialization took too long. Please try restarting the scanner.", 
 "OK");
 return;
 }
 
 if (startTask.IsFaulted)
 {
 var ex = startTask.Exception?.GetBaseException();
 _viewModel.StatusMessage = $"Camera error: {ex?.Message ?? "Unknown error"}";
 Debug.WriteLine($"QRCodeScannerPage: Camera start failed (Windows): {ex?.Message}\n{ex?.StackTrace}");
 await DisplayAlert("Camera Error", 
 "Failed to initialize camera: " + (ex?.Message ?? "unknown error"), 
 "OK");
 return;
 }
 
 await startTask;
 
 _viewModel.StatusMessage = "Position QR code within frame";
 _viewModel.IsScanning = true;
 _cameraInitialized = true;
 
 Debug.WriteLine("QRCodeScannerPage: Camera initialization complete (Windows)");
 
 // Validate camera feed
 await Task.Delay(500);
 var isValid = _windowsScanner.ValidateCameraFeed();
 if (isValid)
{
 Debug.WriteLine("QRCodeScannerPage: ? Camera feed validation PASSED! (Windows)");
}
 else
 {
 Debug.WriteLine("QRCodeScannerPage: WARNING - Camera feed validation FAILED! (Windows)");
 _viewModel.StatusMessage = "Camera may not be working properly";
 }
 }
 catch (Exception ex)
 {
 _viewModel.StatusMessage = $"Camera error: {ex.Message}";
 Debug.WriteLine($"QRCodeScannerPage: InitializeCameraAsync error (Windows): {ex.Message}\n{ex.StackTrace}");
 
 await DisplayAlert("Error", 
 $"An error occurred while initializing the camera: {ex.Message}", 
 "OK");
 }
 finally
 {
 _isInitializing = false;
 }
 }
 
 private async Task PopulateCameraPickerAsync()
 {
 try
 {
 Debug.WriteLine("QRCodeScannerPage: Populating camera picker (Windows)");
 
 var cameras = await _windowsScanner!.GetAvailableCamerasAsync();
 _availableCameras = cameras;
 
 if (cameras.Count >0)
 {
 var cameraNames = cameras.Select(c => c.Name).ToList();
 CameraPicker.ItemsSource = cameraNames;
 
 // Select default camera (first camera)
 var defaultIndex = cameras.FindIndex(c => c.IsDefault);
 if (defaultIndex >=0)
 {
 CameraPicker.SelectedIndex = defaultIndex;
 }
 else
 {
 CameraPicker.SelectedIndex =0;
 }
 
 Debug.WriteLine($"QRCodeScannerPage: Found {cameras.Count} cameras, selected index {CameraPicker.SelectedIndex} (Windows)");
 
 MainThread.BeginInvokeOnMainThread(() =>
 {
 CameraSelectorBorder.IsVisible = true;
 });
 }
 else
 {
 Debug.WriteLine("QRCodeScannerPage: No cameras found (Windows)");
 CameraSelectorBorder.IsVisible = false;
 }
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: Error populating camera picker (Windows): {ex.Message}");
 if (_availableCameras == null || _availableCameras.Count ==0)
 {
 CameraSelectorBorder.IsVisible = false;
}
 }
 }
 
 private async void OnCameraSelectionChanged(object? sender, EventArgs e)
 {
 if (_windowsScanner == null || CameraPicker.SelectedIndex <0 || _availableCameras == null)
 return;
 
 try
 {
 Debug.WriteLine($"QRCodeScannerPage: Camera selection changed to index {CameraPicker.SelectedIndex} (Windows)");
 
 if (CameraPicker.SelectedIndex < _availableCameras.Count)
 {
 var selectedCamera = _availableCameras[CameraPicker.SelectedIndex];
 Debug.WriteLine($"QRCodeScannerPage: Switching to {selectedCamera.Name} (Windows)");
 
 _viewModel.StatusMessage = $"Switching to {selectedCamera.Name}...";
 
 await _windowsScanner.SwitchCameraAsync(selectedCamera.DeviceId);
 
_viewModel.StatusMessage = "Position QR code within frame";
 }
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: Error switching camera (Windows): {ex.Message}");
 _viewModel.StatusMessage = $"Failed to switch camera: {ex.Message}";
 await DisplayAlert("Error", $"Failed to switch camera: {ex.Message}", "OK");
 }
 }
#endif

#if !ANDROID && !WINDOWS
 // Stub handler for platforms without camera support (iOS, MacCatalyst)
 private void OnCameraSelectionChanged(object? sender, EventArgs e)
 {
 Debug.WriteLine("QRCodeScannerPage: Camera selection not supported on this platform");
 }
#endif

 private async void OnUploadScannedDataClicked(object? sender, EventArgs e)
 {
 try
 {
 var text = _viewModel?.ScannedText;
 if (string.IsNullOrWhiteSpace(text))
 {
 await DisplayAlert("No data", "There is no scanned QR data to upload.", "OK");
 return;
 }

 // Default safe action: copy to clipboard and notify user
 await Clipboard.SetTextAsync(text);
 _viewModel.StatusMessage = "Copied scanned data to clipboard";

 // Clear status after a short delay
 await Task.Delay(1400);
 _viewModel.StatusMessage = "Position QR code within frame";
 }
 catch (Exception ex)
 {
 Debug.WriteLine($"QRCodeScannerPage: OnUploadScannedDataClicked error: {ex.Message}");
 try { await DisplayAlert("Error", "Failed to handle scanned data: " + ex.Message, "OK"); } catch { }
 }
 }
}
