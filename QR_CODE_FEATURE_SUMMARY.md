# QR Code Feature Implementation Summary

## Overview
Successfully implemented QR code generation functionality for the scouting form that allows scouts to generate a QR code containing all match data for offline sharing or backup.

## Changes Made

### 1. Fixed DateTime Deserialization Issue
**File:** `ObsidianScout\Services\ApiService.cs`

Added a custom `FlexibleDateTimeConverter` class to handle various date formats from the API:
- `yyyy-MM-dd` (date only)
- `yyyy-MM-ddTHH:mm:ss`
- `yyyy-MM-ddTHH:mm:ssZ`
- `yyyy-MM-ddTHH:mm:ss.fff`
- `yyyy-MM-ddTHH:mm:ss.fffZ`
- And more formats

This fixes the error: `The JSON value could not be converted to System.DateTime`

### 2. Created QR Code Service
**File:** `ObsidianScout\Services\QRCodeService.cs`

New service that provides:
- `GenerateQRCode(string data)`: Generates a QR code image from JSON data
- `SerializeScoutingData(Dictionary<string, object?> data)`: Serializes scouting data to JSON

Uses the `QRCoder` NuGet package (already installed in the project).

### 3. Updated ScoutingViewModel
**File:** `ObsidianScout\ViewModels\ScoutingViewModel.cs`

Added new properties:
- `QrCodeImage`: Stores the generated QR code image
- `IsQRCodeVisible`: Controls QR code display visibility
- `ScoutName`: Stores the scout's name for inclusion in QR data

Added new commands:
- `SaveWithQRCodeCommand`: Generates QR code with all scouting data
- `CloseQRCodeCommand`: Closes the QR code display
- `RefreshCommand`: Refreshes configuration and teams
- `RefreshTeamsCommand`: Refreshes teams list
- `ResetFormCommand`: Resets the scouting form

### 4. Updated Scouting Page UI
**File:** `ObsidianScout\Views\ScoutingPage.xaml.cs`

Added UI elements:
- Scout name entry field
- "Save with QR" button next to Submit button
- QR code display section with:
  - Title
  - QR code image (300x300)
  - Instructions text
  - Close button

### 5. Registered QR Code Service
**File:** `ObsidianScout\MauiProgram.cs`

Registered `IQRCodeService` and `QRCodeService` in dependency injection.

## QR Code Data Format

The generated QR code contains JSON data in the following format:

```json
{
  "team_id": 5,
  "team_number": 16,
  "match_id": 528,
  "match_number": 1,
  "alliance": "unknown",
  "scout_name": "Scout Name",
  "elem_auto_1": false,
  "elem_auto_2": 0,
  "elem_teleop_1": 0,
  "elem_endgame_2": "Park",
  "defense_rating": 3,
  "general_comments": "",
  "auto_period_timer_enabled": false,
  "auto_points_points": 0,
  "teleop_points_points": 0,
  "endgame_points_points": 2,
  "total_points_points": 2,
  "generated_at": "2025-01-18T23:59:33.657Z",
  "offline_generated": true
}
```

### Data Included:
- Team and match information
- All scoring element values (auto, teleop, endgame)
- Rating elements (defense rating, etc.)
- Text elements (comments, predictions, etc.)
- Calculated point totals for each period
- Metadata (generation timestamp, offline flag)

## Usage

1. **Fill out the scouting form** with all match data
2. **Enter your name** in the "Scout Name" field
3. **Click "Save with QR"** button
4. **Scan the QR code** with another device to transfer data
5. **Click "Close"** to dismiss the QR code

## Benefits

- **Offline capability**: Generate QR codes without internet connection
- **Data backup**: QR codes serve as a physical backup of scouting data
- **Easy sharing**: Share match data by scanning QR codes
- **Point calculation**: Automatically calculates and includes point totals
- **Complete data**: Includes all form fields and metadata

## Technical Notes

- QR code uses error correction level Q (medium-high)
- Image size: 300x300 pixels
- QR code graphic scale: 20 pixels per module
- JSON is serialized with camelCase naming
- All DateTime values use ISO 8601 format with UTC timezone

## Testing

To test the QR code feature:
1. Select a team from the dropdown
2. Load matches for the current event
3. Select a match
4. Fill in some scoring data
5. Enter your name
6. Click "Save with QR"
7. Verify the QR code displays correctly
8. Use a QR code scanner to verify the data

## Future Enhancements

Potential improvements:
- Add QR code scanning capability to import data
- Save QR code image to device gallery
- Share QR code via messaging apps
- Compress data for smaller QR codes
- Add encryption for sensitive data
