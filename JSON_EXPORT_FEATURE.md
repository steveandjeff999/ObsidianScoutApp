# ?? JSON Export Feature for Scouting Form

## Overview

Added a JSON export button to the scouting form page that exports match data in the same format as the QR code feature. This allows scouts to save their data as a JSON file for backup, offline storage, or manual transfer.

---

## ? Features Added

### 1. **Export JSON Button**
- Located below the "Submit" and "Save with QR" buttons
- Blue color (`Info` theme) to distinguish from other actions
- Icon: ?? with text "Export as JSON"
- Saves data to `Documents/ObsidianScout/Exports/`

### 2. **Same Format as QR Code**
- Uses identical data structure as QR code generation
- Includes all field values, calculated points, metadata
- Properly formatted JSON with indentation

### 3. **File Naming Convention**
```
scout_team{TeamNumber}_match{MatchNumber}_{yyyyMMdd_HHmmss}.json
```

**Example**: `scout_team5454_match12_20241215_143022.json`

---

## ?? Files Modified

### 1. **ScoutingViewModel.cs**
Added methods:
- `ExportJsonAsync()` - Main export command
- `SaveJsonToFileAsync()` - File saving helper
- `RefreshAsync()` - Refresh game config and teams
- `RefreshTeamsAsync()` - Refresh teams only
- `ResetForm()` - Reset form after submission

### 2. **ScoutingPage.xaml.cs**
Added:
- "Export as JSON" button in `CreateSubmitSection()`
- Button styling matching the UI theme
- Command binding to `ExportJsonAsyncCommand`

---

## ?? How It Works

### Export Process:

1. **Validation**:
   - Checks if team and match are selected
   - Validates team ID and match ID

2. **Data Collection**:
   - Gathers all field values from the form
   - Converts JsonElement types to simple types
   - Adds team/match metadata
   - Adds scout name

3. **Points Calculation**:
   - Calculates auto period points
   - Calculates teleop period points
   - Calculates endgame period points
   - Calculates total points

4. **JSON Serialization**:
   - Uses `JsonSerializerOptions` with `WriteIndented = true`
   - Uses `PropertyNamingPolicy.SnakeCaseLower`
   - Produces readable, formatted JSON

5. **File Saving**:
   - Creates `Documents/ObsidianScout/Exports/` if needed
   - Generates timestamped filename
   - Writes JSON to file
   - Shows success dialog with file path

---

## ?? JSON Format

### Example Output:

```json
{
  "team_id": 5454,
  "team_number": 5454,
  "match_id": 123,
  "match_number": 12,
  "alliance": "unknown",
  "scout_name": "John Scout",
  
  "auto_pieces_scored": 3,
  "auto_mobility": true,
  "auto_period_timer_enabled": false,
  "auto_points_points": 15,
  
  "teleop_pieces_scored": 12,
  "teleop_amplified": true,
  "teleop_points_points": 48,
  
  "endgame_climb": "success",
  "endgame_harmony": false,
  "endgame_trap": 1,
  "endgame_points_points": 12,
  
  "total_points_points": 75,
  
  "driver_rating": 4,
  "defense_rating": 3,
  "notes": "Great performance in autonomous",
  
  "generated_at": "2024-12-15T20:30:22.123Z",
  "offline_generated": true
}
```

---

## ?? UI Design

### Button Layout:

```
??????????????????????????????????????
?  [    Submit    ] [ Save with QR  ]?
?                                    ?
?  [    ?? Export as JSON    ]       ?
??????????????????????????????????????
```

### Button Properties:
- **Background**: `Info` color (#3B82F6 - blue)
- **Text**: White, bold, size 16
- **Height**: 50
- **Corner Radius**: 10
- **Full Width**: Spans entire form width
- **Margin**: 10px top spacing

---

## ?? File Storage

### Location:
- **Windows**: `C:\Users\{Username}\Documents\ObsidianScout\Exports\`
- **macOS**: `/Users/{Username}/Documents/ObsidianScout/Exports/`
- **Android**: `/storage/emulated/0/Documents/ObsidianScout/Exports/`
- **iOS**: App's Documents directory

### File Format:
- **Extension**: `.json`
- **Encoding**: UTF-8
- **Format**: Indented (readable)
- **Naming**: `scout_team{TeamNum}_match{MatchNum}_{Timestamp}.json`

---

## ? User Feedback

### Success:
```
? Exported to scout_team5454_match12_20241215_143022.json

[Dialog Box]
Export Successful
JSON file saved to:
C:\Users\John\Documents\ObsidianScout\Exports\scout_team5454_match12_20241215_143022.json
[ OK ]
```

### Failure:
```
? Failed to save JSON file
```

### Validation Errors:
```
? Please select both a team and a match
? Invalid team or match selection
```

---

## ?? Implementation Details

### Command Pattern:
```csharp
[RelayCommand]
private async Task ExportJsonAsync()
{
    // Validate selections
    if (SelectedTeam == null || SelectedMatch == null)
    {
        StatusMessage = "? Please select both a team and a match";
        return;
    }

    // Build data object (same as QR code)
    var jsonData = new Dictionary<string, object?> { ... };

    // Add all field values
    foreach (var kvp in fieldValues)
    {
        jsonData[kvp.Key] = ConvertValueForSerialization(kvp.Value);
    }

    // Calculate points
    if (GameConfig != null)
    {
        // Auto, teleop, endgame calculations
    }

    // Serialize to JSON
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    var json = JsonSerializer.Serialize(jsonData, options);

    // Save to file
    var result = await SaveJsonToFileAsync(json, filename);
}
```

### File Saving:
```csharp
private async Task<bool> SaveJsonToFileAsync(string json, string filename)
{
    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    var scoutingFolder = Path.Combine(documentsPath, "ObsidianScout", "Exports");
    
    if (!Directory.Exists(scoutingFolder))
    {
        Directory.CreateDirectory(scoutingFolder);
    }

    var filePath = Path.Combine(scoutingFolder, filename);
    await File.WriteAllTextAsync(filePath, json);

    await Shell.Current.DisplayAlert("Export Successful", 
        $"JSON file saved to:\n{filePath}", 
        "OK");

    return true;
}
```

---

## ?? Use Cases

### 1. **Offline Backup**
- Export data when internet is unavailable
- Store locally for later upload
- Prevent data loss

### 2. **Manual Transfer**
- Copy JSON files to USB drive
- Email JSON files to data manager
- Upload to cloud storage

### 3. **Data Verification**
- Review exported data before submission
- Check calculations and field values
- Debug data issues

### 4. **Archive**
- Keep permanent record of scouting sessions
- Historical data analysis
- Team performance tracking

### 5. **Multi-Device Workflow**
- Scout on tablet, export JSON
- Import on desktop for analysis
- Share with team members

---

## ?? Workflow Examples

### Basic Export:
1. Fill out scouting form
2. Select team and match
3. Tap "?? Export as JSON"
4. Confirm export location in dialog
5. Continue scouting or reset form

### Offline Scouting:
1. No internet connection at event
2. Scout multiple matches
3. Export each to JSON
4. Return to base with files
5. Bulk upload or manual entry

### Data Verification:
1. Scout a match
2. Export JSON
3. Open file in text editor
4. Verify all fields correct
5. Submit if accurate

---

## ?? Debugging

### Debug Output:
```
=== EXPORT JSON ===
SelectedTeam: 5454 (ID: 123)
SelectedMatch: 12 Qualification (ID: 456)
StatusMessage: Exporting JSON...
? JSON exported successfully to scout_team5454_match12_20241215_143022.json
? JSON saved to: C:\Users\John\Documents\ObsidianScout\Exports\scout_team5454_match12_20241215_143022.json
```

### Error Handling:
- Validates team/match selection
- Handles file system errors
- Provides user-friendly error messages
- Logs detailed errors to debug output

---

## ?? Platform Compatibility

### ? Supported Platforms:
- ? Windows
- ? macOS
- ? Android
- ? iOS

### Platform-Specific Notes:

**Android**:
- May require storage permissions
- Files visible in "Files" app
- Can be shared via Share menu

**iOS**:
- Saved to app's Documents directory
- Accessible via Files app
- Can be exported via iTunes File Sharing

**Windows/macOS**:
- Direct file system access
- Easy to locate and manage
- Can drag & drop to email/cloud

---

## ?? Design Consistency

### Follows App Theme:
- Uses `Info` color for export button
- Matches liquid glass UI style
- Consistent with QR code feature
- Same validation and feedback patterns

### Button Hierarchy:
1. **Primary**: Submit (direct to server)
2. **Secondary**: Save with QR (offline visual)
3. **Tertiary**: Export JSON (offline file)

---

## ?? Future Enhancements

### Possible Improvements:
1. **Batch Export**: Export multiple matches at once
2. **Import Feature**: Import JSON files back into form
3. **Cloud Sync**: Auto-upload to Dropbox/Google Drive
4. **Compression**: ZIP multiple JSON files
5. **Email Integration**: Direct email export
6. **Share Menu**: Native platform sharing
7. **Auto-Export**: Save on every submission
8. **Export History**: List of exported files

---

## ? Summary

### What Was Added:
? JSON export button in scouting form
? Same data format as QR code
? File saving to Documents folder
? Timestamped filenames
? Success/error feedback
? Debug logging
? Cross-platform support

### Benefits:
- ?? Offline data backup
- ?? Manual data transfer option
- ?? Data verification capability
- ?? Permanent archive
- ?? Multiple workflow options

### Integration:
- Works alongside Submit and QR features
- No interference with existing functionality
- Consistent user experience
- Same validation and error handling

---

Your scouting app now has comprehensive data export capabilities! ?????
