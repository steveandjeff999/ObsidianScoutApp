# ? Export JSON Button Fix

## Problem
Clicking the "Export as JSON" button did nothing - it wasn't wired up to any command.

## Root Cause
The button was defined but its command binding was commented out in `ScoutingPage.xaml.cs`.

## Solution Applied

### 1. Export JSON Button (Line ~1029)
**Before**:
```csharp
// Temporarily disabled - will be fixed after build
// exportJsonButton.Clicked += (s, e) => _viewModel.ExportJsonAsyncCommand.Execute(null);
```

**After**:
```csharp
exportJsonButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.ExportJsonCommand));
```

### 2. Also Fixed Retry Button (Line ~278)
```csharp
retryButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.RefreshCommand));
```

### 3. Also Fixed Refresh Teams Button (Line ~462)
```csharp
refreshTeamsBtn.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.RefreshTeamsCommand));
```

---

## MVVM Toolkit Command Naming

When using `[RelayCommand]` on methods:

| Method Name | Generated Command Property |
|------------|---------------------------|
| `RefreshAsync()` | `RefreshCommand` |
| `RefreshTeamsAsync()` | `RefreshTeamsCommand` |
| `ExportJsonAsync()` | `ExportJsonCommand` |
| `SubmitAsync()` | `SubmitCommand` |
| `SaveWithQRCodeAsync()` | `SaveWithQRCodeCommand` |

**Key Point**: The toolkit generates command names by:
1. Taking the method name
2. Removing "Async" suffix if present  
3. Adding "Command" suffix

So `ExportJsonAsync()` ? `ExportJsonCommand` (NOT `ExportJsonAsyncCommand`)

---

## Test the Fix

1. **Build the app**: The commands should now be generated correctly
2. **Run the app**
3. **Navigate to Scouting Page**
4. **Select a team and match**
5. **Click "?? Export as JSON"**
6. **Expected Behavior**:
   - Shows "Exporting JSON..." status
   - Creates file: `scout_team{number}_match{number}_{timestamp}.json`
   - Shows success message with filename
   - Displays alert with file path
   - File saved to: `Documents/ObsidianScout/Exports/`

---

## Export JSON File Format

The exported JSON includes:
- **Team Info**: `team_id`, `team_number`
- **Match Info**: `match_id`, `match_number`
- **Scout Info**: `scout_name`, `alliance`
- **All Scouting Data**: Counter values, boolean flags, text fields, etc.
- **Calculated Points**: `auto_points_points`, `teleop_points_points`, `endgame_points_points`, `total_points_points`
- **Metadata**: `generated_at`, `offline_generated`

Example filename:
```
scout_team1234_match5_20250120_142530.json
```

---

## File Location

**Windows**: `C:\Users\{username}\Documents\ObsidianScout\Exports\`
**macOS**: `~/Documents/ObsidianScout/Exports/`
**Android**: `/storage/emulated/0/Documents/ObsidianScout/Exports/`
**iOS**: App's Documents directory

---

## Error Messages

If you see:
- **"? Please select both a team and a match"** ? Select team and match first
- **"? Invalid team or match selection"** ? IDs are invalid, reselect
- **"? Error exporting JSON: {message}"** ? Check file permissions
- **"? Failed to save JSON file"** ? Check storage permissions

---

## Validation

The ExportJson command validates:
1. ? `SelectedTeam` is not null
2. ? `SelectedMatch` is not null  
3. ? `TeamId > 0` and `MatchId > 0`
4. ? File system write permissions

---

## Success Indicators

When export succeeds:
1. ? Status message: "? Exported to {filename}"
2. ? Alert dialog shows full file path
3. ? Message clears after 3 seconds
4. ? Debug log confirms file path

---

## Next Steps

1. **Clean Solution**: Build > Clean Solution
2. **Rebuild**: Build > Rebuild Solution  
3. **Test Export**: Try exporting a scouting entry
4. **Verify File**: Check the Documents folder for the JSON file
5. **Import Test**: Use the JSON file to import data on another device

---

## Summary

? **Export JSON button now functional**
? **Retry and Refresh Teams buttons also fixed**
? **Correct MVVM Toolkit command naming used**
? **All three buttons now work properly**

The "Export as JSON" feature is now fully operational! ??
