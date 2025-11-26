using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ObsidianScout.ViewModels;

public partial class QRCodeScannerViewModel : ObservableObject
{
    [ObservableProperty]
    private string scannedText = string.Empty;

    [ObservableProperty]
    private bool isScanning = true;

    [ObservableProperty]
    private bool isFlashlightOn = false;

    [ObservableProperty]
    private string statusMessage = "Position QR code within frame";

    public QRCodeScannerViewModel()
    {
    }

    [RelayCommand]
    private void ToggleFlashlight()
    {
        this.isFlashlightOn = !this.isFlashlightOn;
        OnPropertyChanged(nameof(IsFlashlightOn));
    }

    [RelayCommand]
    private void ToggleScanning()
    {
        this.isScanning = !this.isScanning;
        OnPropertyChanged(nameof(IsScanning));
    this.statusMessage = this.isScanning ? "Scanning..." : "Scanning paused";
      OnPropertyChanged(nameof(StatusMessage));
    }

    [RelayCommand]
    private void ClearText()
    {
 this.scannedText = string.Empty;
  OnPropertyChanged(nameof(ScannedText));
      this.statusMessage = "Position QR code within frame";
  OnPropertyChanged(nameof(StatusMessage));
    }

    [RelayCommand]
    private async Task CopyToClipboard()
    {
  if (!string.IsNullOrWhiteSpace(this.scannedText))
        {
   await Clipboard.SetTextAsync(this.scannedText);
            this.statusMessage = "? Copied to clipboard";
 OnPropertyChanged(nameof(StatusMessage));
         await Task.Delay(2000);
      this.statusMessage = "Position QR code within frame";
  OnPropertyChanged(nameof(StatusMessage));
        }
    }

    public void OnQRCodeDetected(string text)
    {
      this.scannedText = text;
 OnPropertyChanged(nameof(ScannedText));
this.statusMessage = "? QR Code detected";
  OnPropertyChanged(nameof(StatusMessage));
        // Optionally pause scanning after detection
        // this.isScanning = false;
   // OnPropertyChanged(nameof(IsScanning));
    }
}
