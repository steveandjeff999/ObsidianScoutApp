# QR Scanner Usage Guide

## Quick Start

The QR Scanner is now fully functional on Android. Here's what you need to know:

## User Flow

1. **Navigate to Scanner**
   - User opens the QR Scanner page from your app
   - App automatically requests camera permission if not granted

2. **Permission Handling**
   - If **granted**: Camera initializes automatically
   - If **denied**: Fallback to manual paste mode with helpful message

3. **Scanning**
 - Camera preview appears with targeting frame
   - Point camera at QR code (within the frame is best)
   - Detected QR codes appear in text box below
   - Scan throttled to 500ms to prevent duplicate readings

4. **Controls**
   - **Flashlight button**: Toggle camera flash (Android/iOS only)
   - **Stop/Resume button**: Pause/resume scanning (Android/iOS only)
   - **Copy button**: Copy scanned text to clipboard
   - **Clear button**: Clear the scanned text
   - **Manual paste**: Users can always paste QR data manually in the text box

## Features

### ? Working Features
- Real-time QR code scanning
- Camera preview with overlay frame
- Flashlight toggle (on devices with flash)
- Scan throttling (prevents duplicate reads)
- Manual text paste fallback
- Copy to clipboard
- Editable scanned text
- Graceful permission handling
- Comprehensive error messages

### ?? Technical Features
- Proper resource disposal
- Thread-safe initialization
- Memory-efficient (cleans up on page navigation)
- Debug logging for troubleshooting
- 15-second initialization timeout
- Automatic retry on transient failures

## Platform-Specific Behavior

### Android (? Fully Working)
- Live camera preview with CameraX
- Real-time QR scanning with ZXing
- Flashlight control
- Stop/Resume scanning

### Windows (?? Limited)
- Opens Windows Camera app
- User scans QR code
- User manually pastes result into app

### iOS/MacCatalyst (?? Not Implemented Yet)
- Permissions configured in Info.plist
- Implementation pending (would be similar to Android)
- Falls back to manual paste mode

## Error Messages

| Message | Meaning | User Action |
|---------|---------|-------------|
| "Position QR code within frame" | Camera ready | Scan normally |
| "Camera permission denied - Paste QR data manually" | Permission not granted | Use manual paste |
| "Camera initialization timed out" | Camera slow to start | Restart page or device |
| "Camera error: [details]" | Technical issue | Check Debug Output logs |
| "Camera not available" | No activity context | Shouldn't occur in normal use |

## Debugging

### Enable Debug Logging
Debug output is automatically enabled. View in Visual Studio Output window (Debug pane):

```
CameraQRScanner: Starting camera initialization
CameraQRScanner: Camera provider obtained  
CameraQRScanner: Binding camera
CameraQRScanner: Camera bound to lifecycle successfully
QRCodeScannerPage: Camera permission granted
QRCodeScannerPage: Camera preview added successfully
CameraQRScanner: QR code detected: {"team":1234...
```

### Common Debug Patterns

**Successful Scan:**
```
CameraQRScanner: QR code detected: [data]...
QRCodeScannerPage: QR code detected, length: 157
```

**Permission Denied:**
```
QRCodeScannerPage: Camera permission denied
```

**Timeout:**
```
QRCodeScannerPage: Camera initialization timed out
```

## Performance

- **Initialization time**: 1-3 seconds typical
- **Scan rate**: Up to 2 scans/second (500ms throttle)
- **Memory usage**: Low (proper cleanup on navigation)
- **Battery impact**: Moderate while camera active (stops on page navigation)

## Best Practices

### For Developers
1. Always test camera functionality on physical device
2. Check Debug Output for detailed error information
3. Test with various QR code sizes and lighting conditions
4. Verify cleanup by navigating away and back

### For Users
1. Ensure adequate lighting
2. Hold QR code steady within the frame
3. Keep QR code flat and unobstructed
4. If scanning fails, use manual paste option
5. Grant camera permission when prompted

## Testing Checklist

- [ ] Camera permission request appears
- [ ] Camera permission granted successfully
- [ ] Camera preview appears within 3 seconds
- [ ] QR code detected and text appears below
- [ ] Flashlight toggles on/off
- [ ] Copy button copies text to clipboard
- [ ] Clear button clears text
- [ ] Manual paste works in text box
- [ ] Navigation away stops camera
- [ ] Navigation back restarts camera
- [ ] Error messages are user-friendly
- [ ] No crashes or freezes

## Troubleshooting

### Camera Won't Start
1. Check camera permission in device settings
2. Restart the app
3. Check Debug Output for errors
4. Try on different device

### QR Code Not Detected
1. Improve lighting
2. Clean camera lens
3. Ensure QR code is within frame
4. Try manual paste as fallback

### App Crashes
1. Check Debug Output for stack trace
2. Verify all NuGet packages are restored
3. Clean and rebuild solution
4. Check device Android version (min API 21)

### Performance Issues
1. Close other apps using camera
2. Restart device
3. Check available device storage
4. Update Android System WebView

## Code Integration

### Subscribe to QR Code Detection
```csharp
_viewModel.PropertyChanged += (s, e) =>
{
    if (e.PropertyName == nameof(_viewModel.ScannedText))
    {
    // Handle scanned QR code
      var qrData = _viewModel.ScannedText;
        ProcessQRCode(qrData);
    }
};
```

### Manual Trigger Scan
```csharp
// Scanning starts automatically on page appearing
// To manually control:
protected override void OnAppearing()
{
 base.OnAppearing();
    // Scanner initializes automatically
}

protected override void OnDisappearing()
{
    base.OnDisappearing();
    // Scanner stops automatically
}
```

## Future Enhancements

### Planned
- [ ] iOS/MacCatalyst native camera implementation
- [ ] QR code generation feature
- [ ] Scanning history
- [ ] Barcode format detection beyond QR

### Possible
- [ ] Zoom controls
- [ ] QR code boundary highlighting
- [ ] Batch scanning mode
- [ ] Export scan history
- [ ] Custom scan sound/vibration
