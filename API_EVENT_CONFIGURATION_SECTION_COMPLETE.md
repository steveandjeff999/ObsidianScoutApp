# Game Config Editor - API & Event Configuration Section Added

## Summary

Added a new **"API & Event Configuration"** section to the Game Config Editor that appears below Game Info and displays all non-game-period configuration fields including:
- Current Event Code
- Config Version
- Match Types (comma-separated)
- Placeholders for future FIRST API and TBA API credentials

## Changes Made

### 1. **XAML (GameConfigEditorPage.xaml)** ?
Added new Frame section with ?? icon containing:
- **Current Event Code** field (binds to `CurrentConfig.CurrentEventCode`)
- **Config Version** field (binds to `CurrentConfig.Version`)
- **Match Types** field (binds to `MatchTypesString` - comma-separated)
- **FIRST API Key** placeholder (disabled, opacity 0.5)
- **TBA API Key** placeholder (disabled, opacity 0.5)

### 2. **ViewModel (GameConfigEditorViewModel.cs)** ?
Added `MatchTypesString` property with:
- **Get**: Joins List<string> with commas for display
- **Set**: Splits comma-separated string back to List<string>
- **Auto-initialization**: Populated when config is loaded in `PopulateCollections()`
- **Bi-directional binding**: Changes in UI update the model automatically

## Implementation Details

###  **Match Types String Handling**

```csharp
// Property in ViewModel
private string _matchTypesString = string.Empty;
public string MatchTypesString
{
    get => _matchTypesString;
    set
    {
        if (SetProperty(ref _matchTypesString, value) && CurrentConfig != null)
        {
            // Parse comma-separated string back to list
        CurrentConfig.MatchTypes = value
      .Split(',')
     .Select(s => s.Trim())
      .Where(s => !string.IsNullOrEmpty(s))
     .ToList();
        }
    }
}

// Updated in PopulateCollections
private void PopulateCollections(GameConfig cfg)
{
    // Update match types string for UI display
    MatchTypesString = string.Join(", ", cfg.MatchTypes ?? new List<string>());
    
    // ... rest of method
}
```

### **XAML Binding**

```xaml
<!-- API & Event Configuration Section -->
<Frame Padding="12" CornerRadius="12"
    BackgroundColor="{AppThemeBinding Light={StaticResource LightSurfaceVariant}, Dark={StaticResource DarkSurfaceVariant}}">
 <VerticalStackLayout Spacing="10">
        <Label Text="?? API &amp; Event Configuration" FontSize="18" FontAttributes="Bold" />

      <!-- Current Event Code -->
        <VerticalStackLayout Spacing="4">
            <Label Text="Current Event Code" FontSize="12" FontAttributes="Bold" />
 <Entry Text="{Binding CurrentConfig.CurrentEventCode}" 
       Placeholder="e.g., EXREG, 2025CAVE" />
        </VerticalStackLayout>

        <!-- Version -->
  <VerticalStackLayout Spacing="4">
            <Label Text="Config Version" FontSize="12" FontAttributes="Bold" />
            <Entry Text="{Binding CurrentConfig.Version}" 
     Placeholder="e.g., 1.0.2" />
        </VerticalStackLayout>

  <!-- Match Types (Comma-separated display) -->
        <VerticalStackLayout Spacing="4">
  <Label Text="Match Types (comma-separated)" FontSize="12" FontAttributes="Bold" />
  <Entry Text="{Binding MatchTypesString}" 
        Placeholder="Practice, Qualification, Playoff" />
       <Label Text="Separate match types with commas" FontSize="10" FontAttributes="Italic" />
        </VerticalStackLayout>

        <!-- API Credentials (Future Feature) -->
        <BoxView HeightRequest="1" Color="{AppThemeBinding Light=#E0E0E0, Dark=#424242}" Margin="0,8" />
        
        <Label Text="API Credentials (Future Feature)" FontSize="14" FontAttributes="Bold" />
        
        <VerticalStackLayout Spacing="4" Opacity="0.5">
        <Label Text="FIRST API Key" FontSize="12" />
 <Entry Placeholder="Not yet implemented" IsEnabled="False" />
  </VerticalStackLayout>

  <VerticalStackLayout Spacing="4" Opacity="0.5">
    <Label Text="TBA API Key" FontSize="12" />
 <Entry Placeholder="Not yet implemented" IsEnabled="False" />
    </VerticalStackLayout>
    </VerticalStackLayout>
</Frame>
```

## User Experience

### **Loading Config**
1. User clicks **Load** button
2. JSON is loaded from API
3. Form fields populate automatically:
   - **Current Event Code**: "EXREG" (from JSON)
   - **Config Version**: "1.0.2" (from JSON)
   - **Match Types**: "Practice, Qualification, Playoff" (auto-joined from array)

### **Editing Config**
1. User switches to **Form Editor**
2. Can edit all fields directly:
   - Type in new event code
   - Change version number
   - Add/remove match types by editing comma-separated list

### **Saving Config**
1. User clicks **Save**
2. MatchTypesString is automatically parsed back to array
3. JSON is serialized with correct structure:
```json
{
  "current_event_code": "EXREG",
  "version": "1.0.2",
  "match_types": [
    "Practice",
    "Qualification",
    "Playoff"
  ]
}
```

## Example JSON Structure

```json
{
  "season": 2026,
  "game_name": "REBUILT™ presented by Haas",
  "alliance_size": 3,
  "current_event_code": "EXREG",
  "version": "1.0.2",
  "match_types": [
    "Practice",
    "Qualification",
    "Playoff"
  ],
  "auto_period": { ... },
  "teleop_period": { ... },
  "endgame_period": { ... },
  "post_match": { ... }
}
```

## Future API Credentials Support

The section includes placeholders for:
- **FIRST API Key**: For official FIRST Robotics Competition API access
- **TBA API Key**: For The Blue Alliance API integration

These fields are currently:
- ? Visually present in UI
- ? Greyed out (Opacity 0.5)
- ? Disabled (IsEnabled="False")
- ? Placeholder text: "Not yet implemented"
- ? Separated by divider line

### To Implement Later:
1. Add properties to `GameConfig` model:
   ```csharp
   [JsonPropertyName("first_api_key")]
   public string? FirstApiKey { get; set; }
   
   [JsonPropertyName("tba_api_key")]
   public string? TbaApiKey { get; set; }
   ```

2. Update XAML bindings:
   ```xaml
   <Entry Text="{Binding CurrentConfig.FirstApiKey}" IsEnabled="True" Opacity="1.0" />
   <Entry Text="{Binding CurrentConfig.TbaApiKey}" IsEnabled="True" Opacity="1.0" />
   ```

3. Add Password entry mode for security:
   ```xaml
   <Entry Text="{Binding CurrentConfig.FirstApiKey}" IsPassword="True" />
   ```

## Field Descriptions

| Field | Purpose | Format | Example |
|-------|---------|--------|---------|
| **Current Event Code** | Active competition event identifier | String (uppercase) | "EXREG", "2025CAVE", "2025TXHOU" |
| **Config Version** | Configuration schema version for tracking changes | Semantic versioning | "1.0.2", "2.1.0" |
| **Match Types** | Types of matches available in the competition | Comma-separated list | "Practice, Qualification, Playoff" |
| **FIRST API Key** | Authentication key for FIRST API (future) | String (secret) | *(not yet implemented)* |
| **TBA API Key** | Authentication key for TBA API (future) | String (secret) | *(not yet implemented)* |

## Benefits

? **Centralized Configuration**: All non-gameplay config in one section
? **User-Friendly**: Simple text fields instead of JSON editing
? **Bi-Directional Sync**: Changes in Form Editor update JSON automatically
? **Future-Proof**: Ready for API credential fields when needed
? **Clean Separation**: Clear visual separation from game periods
? **Validation-Ready**: Easy to add input validation later

## Testing Checklist

- [ ] Load config - verify fields populate correctly
- [ ] Edit Event Code - verify JSON updates
- [ ] Edit Version - verify JSON updates
- [ ] Edit Match Types - verify array parsing works
- [ ] Add new match type (e.g., "Semifinals") - verify it appears in JSON
- [ ] Remove match type - verify it's removed from JSON
- [ ] Save config - verify JSON structure is correct
- [ ] Reload config - verify Round-trip preserves all values

## Files Modified

1. ? `ObsidianScout/Views/GameConfigEditorPage.xaml` - Added API & Event Configuration Frame
2. ? `ObsidianScout/ViewModels/GameConfigEditorViewModel.cs` - Added MatchTypesString property
3. ? Build successful - No compilation errors

---

**Status**: ? **COMPLETE**
**Build**: ? **SUCCESSFUL**
**Impact**: Users can now edit event code, version, and match types through form editor
