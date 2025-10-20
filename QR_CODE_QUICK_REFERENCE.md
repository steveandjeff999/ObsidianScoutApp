# QR Code Generation - Quick Reference

## What Was Fixed

### Primary Issue
? **Error:** `The JSON value could not be converted to System.DateTime. Path: $.events[0].end_date`

? **Solution:** Added `FlexibleDateTimeConverter` to handle multiple date formats from the API

## New Feature: QR Code Generation

### How to Use

1. **Complete the Scouting Form**
   - Select team
   - Load and select match
   - Fill in all scoring data

2. **Enter Scout Name**
   - Required field at bottom of form
   - Your name will be included in QR data

3. **Generate QR Code**
   - Click "Save with QR" button (next to Submit)
   - QR code appears instantly
   - Contains all form data + calculated points

4. **Use the QR Code**
   - Scan with another device
   - Data transfers without internet
   - Close when done

### Button Layout
```
[Submit Button]  [Save with QR Button]
```

### What's Included in QR Code

? Team information (ID, number)
? Match information (ID, number, type)
? Scout name
? All autonomous scoring
? All teleop scoring
? All endgame scoring
? Ratings (defense, etc.)
? Comments and notes
? **Auto-calculated point totals**
? Timestamp
? Offline flag

### Example QR Data Structure
```json
{
  "team_number": 16,
  "match_number": 1,
  "scout_name": "Your Name",
  "elem_auto_1": 5,
  "elem_teleop_1": 12,
  "auto_points_points": 25,
  "total_points_points": 87,
  "generated_at": "2025-01-18T23:59:33.657Z"
}
```

## Commands Added

| Command | Description |
|---------|-------------|
| `SaveWithQRCodeCommand` | Generates QR code from form data |
| `CloseQRCodeCommand` | Closes QR code display |
| `RefreshCommand` | Reloads config and teams |
| `RefreshTeamsCommand` | Reloads teams only |
| `ResetFormCommand` | Clears all form data |

## Services Added

### IQRCodeService
```csharp
ImageSource GenerateQRCode(string data);
string SerializeScoutingData(Dictionary<string, object?> data);
```

Registered in `MauiProgram.cs` for dependency injection.

## Troubleshooting

### QR Code Won't Generate
- ? Check team is selected
- ? Check match is selected
- ? Ensure QRCoder package is installed
- ? Verify scout name is entered

### QR Code Too Large
- Data automatically includes only filled fields
- Empty fields use default values
- Maximum QR code capacity: ~2,953 bytes (version 40, level Q)

### Can't Scan QR Code
- Ensure good lighting
- Keep scanner steady
- Try different QR scanner app
- Zoom out if QR is too large on screen

## Files Modified

1. `Services/ApiService.cs` - DateTime converter
2. `Services/QRCodeService.cs` - NEW file
3. `ViewModels/ScoutingViewModel.cs` - QR commands
4. `Views/ScoutingPage.xaml.cs` - UI elements
5. `MauiProgram.cs` - Service registration

## Dependencies

- `QRCoder` (v1.7.0) - Already installed ?
- `System.Text.Json` - Built-in ?

No additional packages needed!
