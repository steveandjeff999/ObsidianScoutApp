# Dynamic Scouting Form - Implementation Guide

## Overview

The OBSIDIAN Scout Mobile App now features a **dynamic scouting form** that automatically generates its UI based on the game configuration retrieved from the server. This means the app can adapt to different FRC game seasons without requiring code changes or app updates.

## How It Works

### 1. Game Configuration Loading

When the scouting page is opened, the app:
1. Fetches the game configuration from `/api/mobile/config/game`
2. Parses the JSON response containing all scoring elements
3. Dynamically builds the UI based on the configuration

### 2. Supported Element Types

The dynamic form supports the following element types from the API:

#### Counter (Integer Input)
```json
{
  "id": "elem_auto_2",
  "name": "CORAL (L1)",
  "type": "counter",
  "default": 0,
  "min": 0,
  "max": 10,
  "points": 3.0
}
```

**UI Rendering:**
- Label with element name
- Decrement button (-)
- Numeric value display
- Increment button (+)
- Respects `min` and `max` values

#### Boolean (Checkbox)
```json
{
  "id": "elem_auto_1",
  "name": "Leave Starting Line",
  "type": "boolean",
  "default": false,
  "points": 3.0
}
```

**UI Rendering:**
- Label with element name
- Checkbox control
- Uses `default` value as initial state

#### Multiple Choice (Picker/Dropdown)
```json
{
  "id": "elem_endgame_2",
  "name": "Climb",
  "type": "multiple_choice",
  "default": "Park",
  "options": [
    { "name": "Park", "points": 2.0 },
    { "name": "Shallow", "points": 6.0 },
    { "name": "Deep", "points": 12.0 },
    { "name": "None", "points": 0.0 }
  ]
}
```

**UI Rendering:**
- Label with element name
- Picker (dropdown) with all options
- Selected option stored as string value

#### Rating (Slider)
```json
{
  "id": "defense_rating",
  "name": "Defense Rating",
  "type": "rating",
  "default": 3,
  "min": 1,
  "max": 5
}
```

**UI Rendering:**
- Label with element name
- Slider control with min/max range
- Current value display
- Stores integer value

#### Text (Single Line)
```json
{
  "id": "quick_notes",
  "name": "Quick Notes",
  "type": "text",
  "multiline": false
}
```

**UI Rendering:**
- Label with element name
- Single-line Entry control
- Stores string value

#### Text (Multi-line)
```json
{
  "id": "general_comments",
  "name": "General Comments",
  "type": "text",
  "multiline": true
}
```

**UI Rendering:**
- Label with element name
- Multi-line Editor control (100px height)
- Stores string value

### 3. Form Sections

The form is organized into sections based on game periods:

1. **Match Info** (Always present)
   - Team ID input
   - Match ID input

2. **Autonomous Period** (`auto_period`)
   - All elements from `auto_period.scoring_elements`

3. **Teleop Period** (`teleop_period`)
   - All elements from `teleop_period.scoring_elements`

4. **Endgame Period** (`endgame_period`)
   - All elements from `endgame_period.scoring_elements`

5. **Post Match** (`post_match`)
   - Rating elements from `post_match.rating_elements`
   - Text elements from `post_match.text_elements`

6. **Submit Section** (Always present)
   - Status message display
   - Submit button
   - Loading indicator
   - Reset button

### 4. Data Submission Format

When the form is submitted, all field values are sent in a dictionary format:

```json
{
  "team_id": 5454,
  "match_id": 12,
  "data": {
    "elem_auto_1": true,
    "elem_auto_2": 3,
    "elem_auto_3": 2,
    "elem_teleop_1": 8,
    "elem_teleop_2": 5,
    "elem_endgame_2": "Shallow",
    "defense_rating": 4,
    "general_comments": "Great performance!"
  },
  "offline_id": "550e8400-e29b-41d4-a716-446655440000"
}
```

The keys in the `data` object correspond to the `id` field from each scoring element.

## Code Architecture

### Models

**GameConfig.cs**
- `GameConfig` - Main configuration object
- `GamePeriod` - Represents a game period (auto/teleop/endgame)
- `ScoringElement` - Represents a scoreable field
- `ScoringOption` - Option for multiple choice fields
- `PostMatch` - Post-match section configuration
- `RatingElement` - Rating field configuration
- `TextElement` - Text field configuration

### ViewModel

**ScoutingViewModel.cs**
- Loads game configuration on initialization
- Manages field values in a `Dictionary<string, object?>`
- Provides methods for counter increment/decrement
- Handles form submission with dynamic data
- Supports form reset

**Key Properties:**
- `GameConfig` - The loaded game configuration
- `AutoElements` - Observable collection of auto period elements
- `TeleopElements` - Observable collection of teleop period elements
- `EndgameElements` - Observable collection of endgame period elements
- `RatingElements` - Observable collection of rating elements
- `TextElements` - Observable collection of text elements

**Key Methods:**
- `GetFieldValue(id)` - Get current value of a field
- `SetFieldValue(id, value)` - Set value of a field
- `IncrementCounter(id)` - Increment a counter field
- `DecrementCounter(id)` - Decrement a counter field

### View

**ScoutingPage.xaml.cs**
- Dynamically builds UI in code-behind
- Creates appropriate controls based on element type
- Binds controls to ViewModel methods
- Updates on configuration changes

**Key Methods:**
- `BuildDynamicForm()` - Builds entire form structure
- `CreateMatchInfoSection()` - Creates match info inputs
- `CreatePeriodSection()` - Creates section for a game period
- `CreateElementView()` - Routes to specific element creator
- `CreateCounterView()` - Creates counter UI
- `CreateBooleanView()` - Creates checkbox UI
- `CreateMultipleChoiceView()` - Creates picker UI
- `CreateRatingView()` - Creates slider UI
- `CreateTextView()` - Creates text input UI

## Example Game Configurations

### 2025 REEFSCAPE Example

The provided example shows the 2025 REBUILT game:

**Auto Period:**
- Leave Starting Line (boolean) - 3 points
- CORAL (L1-L4) - counters with different point values
- ALGAE scoring - multiple counters for different targets

**Teleop Period:**
- CORAL scoring levels - counters
- ALGAE scoring - counters
- Defense tracking - counter

**Endgame:**
- Climb status - multiple choice (Park/Shallow/Deep/None)

**Post Match:**
- Defense Rating - slider (1-5)
- General Comments - multi-line text

## Advantages of Dynamic Forms

1. **Season Flexibility** - App works across different game seasons
2. **No Code Changes** - Server controls form structure
3. **Instant Updates** - Configuration changes reflect immediately
4. **Consistent Data** - Same structure as web application
5. **Easy Testing** - Can test different configurations without rebuilding

## Limitations

### Current Limitations

1. **Supported Types Only** - Only handles: counter, boolean, multiple_choice, rating, text
2. **No Conditional Visibility** - All fields always visible
3. **No Field Groups** - Nested groups not supported yet
4. **No Image Upload** - Image type not implemented
5. **No Custom Validation** - Only basic min/max enforcement

### Future Enhancements

- [ ] Support for image capture/upload
- [ ] Conditional field visibility based on other fields
- [ ] Field grouping and nesting
- [ ] Custom validation rules
- [ ] Field dependencies
- [ ] Real-time validation feedback
- [ ] Field help text display
- [ ] Required field indicators

## Troubleshooting

### Form Doesn't Appear

**Problem:** Scouting page is blank

**Solution:**
1. Check internet connection
2. Verify authentication token is valid
3. Check server URL configuration
4. Look at status message for errors

### Fields Not Updating

**Problem:** Counter buttons don't work

**Solution:**
1. Ensure you're logged in
2. Check that game config loaded successfully
3. Try refreshing the form
4. Restart the app

### Submission Fails

**Problem:** "Submission failed" error

**Solution:**
1. Verify Team ID and Match ID are entered
2. Check all required fields are filled
3. Ensure server is reachable
4. Check network connection

### Missing Elements

**Problem:** Some fields don't appear

**Solution:**
1. Check game configuration on server
2. Verify element types are supported
3. Check for JSON parsing errors in logs
4. Refresh game configuration

## API Requirements

The mobile app expects the following from `/api/mobile/config/game`:

```json
{
  "success": true,
  "config": {
    "season": 2025,
    "game_name": "REBUILT",
    "auto_period": {
      "scoring_elements": [...]
    },
    "teleop_period": {
      "scoring_elements": [...]
    },
    "endgame_period": {
      "scoring_elements": [...]
    },
    "post_match": {
      "rating_elements": [...],
      "text_elements": [...]
    }
  }
}
```

**Required Fields for Each Element:**
- `id` - Unique identifier
- `name` - Display name
- `type` - Element type
- `default` - Default value (optional but recommended)

**Type-Specific Requirements:**
- Counter: `min`, `max` (optional)
- Multiple Choice: `options` array
- Rating: `min`, `max`, `default`
- Text: `multiline` boolean

## Testing

### Manual Testing Steps

1. **Load Form**
   - Open app
   - Navigate to Scouting page
   - Verify all sections appear
   - Check all element names are correct

2. **Test Counters**
   - Tap + button, value increases
   - Tap - button, value decreases
   - Verify min/max limits work
   - Check value displays correctly

3. **Test Booleans**
   - Toggle checkbox
   - Verify state changes

4. **Test Multiple Choice**
   - Open picker
   - Select each option
   - Verify selection saves

5. **Test Ratings**
   - Move slider
   - Check value updates
   - Verify min/max range

6. **Test Text**
   - Enter text in single-line fields
   - Enter text in multi-line fields
   - Verify text saves

7. **Test Submission**
   - Fill out form completely
   - Enter Team ID and Match ID
   - Tap Submit
   - Verify success message
   - Check data on server

8. **Test Reset**
   - Fill out some fields
   - Tap Reset Form
   - Verify all fields return to defaults

### Automated Testing (Future)

```csharp
[Test]
public async Task GameConfig_LoadsSuccessfully()
{
    var viewModel = new ScoutingViewModel(apiService);
    await Task.Delay(1000); // Wait for load
    
    Assert.IsNotNull(viewModel.GameConfig);
    Assert.IsTrue(viewModel.AutoElements.Count > 0);
}

[Test]
public void Counter_IncrementDecrement_Works()
{
    var viewModel = new ScoutingViewModel(apiService);
    viewModel.SetFieldValue("test_counter", 5);
    
    viewModel.IncrementCounter("test_counter");
    Assert.AreEqual(6, viewModel.GetFieldValue("test_counter"));
    
    viewModel.DecrementCounter("test_counter");
    Assert.AreEqual(5, viewModel.GetFieldValue("test_counter"));
}
```

## Summary

The dynamic scouting form provides a flexible, future-proof solution for FRC scouting. By loading the form structure from the server, the mobile app can adapt to any game without requiring updates. This ensures consistency between the web and mobile apps and allows for rapid iteration during competition season.

---

**Status:** ? **IMPLEMENTED**  
**Supported Types:** counter, boolean, multiple_choice, rating, text  
**Build Status:** ? **SUCCESSFUL**  
**API Integration:** ? **COMPLETE**

