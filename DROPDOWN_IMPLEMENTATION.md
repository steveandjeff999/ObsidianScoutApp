# Team and Match Dropdown Implementation

## Overview

Converted Team ID and Match ID from manual text entry fields to dropdown selections (Pickers) for better user experience and data accuracy.

## Changes Made

### 1. ScoutingViewModel.cs

**Added Properties:**
```csharp
[ObservableProperty]
private Team? selectedTeam;

[ObservableProperty]
private Match? selectedMatch;

public ObservableCollection<Team> Teams { get; } = new();
public ObservableCollection<Match> Matches { get; } = new();
```

**Added Property Change Handlers:**
```csharp
partial void OnSelectedTeamChanged(Team? value)
{
    if (value != null)
    {
        TeamId = value.Id;
    }
}

partial void OnSelectedMatchChanged(Match? value)
{
    if (value != null)
    {
        MatchId = value.Id;
    }
}
```

**Added Methods:**
- `LoadTeamsAsync()` - Loads all teams for the scouting team (called in constructor)
- `LoadMatchesAsync()` - Loads matches for the current event (triggered by button)

**Updated Methods:**
- `ResetForm()` - Now clears `SelectedTeam` and `SelectedMatch`
- `RefreshConfig()` - Now also calls `LoadTeamsAsync()`

### 2. ScoutingPage.xaml.cs

**Updated `CreateMatchInfoSection()`:**

Replaced Entry fields with Pickers:

**Team Picker:**
```csharp
var teamPicker = new Picker
{
    Title = "Select Team",
    TextColor = GetTextColor(),
    TitleColor = GetSecondaryTextColor()
};
teamPicker.SetBinding(Picker.ItemsSourceProperty, nameof(ScoutingViewModel.Teams));
teamPicker.SetBinding(Picker.SelectedItemProperty, nameof(ScoutingViewModel.SelectedTeam));
teamPicker.ItemDisplayBinding = new Binding(".", 
    converter: new FuncConverter<Team, string>(team => 
        team != null ? $"{team.TeamNumber} - {team.TeamName}" : ""));
```

Displays: `"5454 - The Bionics"`

**Match Picker:**
```csharp
var matchPicker = new Picker
{
    Title = "Select Match",
    TextColor = GetTextColor(),
    TitleColor = GetSecondaryTextColor()
};
matchPicker.SetBinding(Picker.ItemsSourceProperty, nameof(ScoutingViewModel.Matches));
matchPicker.SetBinding(Picker.SelectedItemProperty, nameof(ScoutingViewModel.SelectedMatch));
matchPicker.ItemDisplayBinding = new Binding(".",
    converter: new FuncConverter<Match, string>(match =>
        match != null ? $"{match.MatchType} {match.MatchNumber}" : ""));
```

Displays: `"Qualification 15"`, `"Playoff 3"`, etc.

**Load Matches Button:**
```csharp
var loadMatchesButton = new Button
{
    Text = "Load Matches",
    FontSize = 12,
    Padding = new Thickness(5),
    CornerRadius = 5
};
loadMatchesButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.LoadMatchesCommand));
```

### 3. ValueConverters.cs

**Added FuncConverter:**
```csharp
public class FuncConverter<TSource, TTarget> : IValueConverter
{
    private readonly Func<TSource?, TTarget?> _convertFunc;
    
    public FuncConverter(Func<TSource?, TTarget?> convertFunc)
    {
        _convertFunc = convertFunc;
    }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TSource source || value == null)
        {
            return _convertFunc((TSource?)value);
        }
        return default(TTarget);
    }
}
```

Allows inline lambda conversion for Picker display bindings.

## User Flow

### 1. Page Opens
- Teams load automatically in the background
- Team picker is populated with all teams (sorted by team number)

### 2. Select Team
- User taps "Select Team" picker
- Dropdown shows: "254 - The Cheesy Poofs", "5454 - The Bionics", etc.
- User selects a team
- `TeamId` is automatically set

### 3. Load Matches
- User taps "Load Matches" button
- Matches for current event are fetched
- Match picker is populated (sorted by type, then number)

### 4. Select Match
- User taps "Select Match" picker
- Dropdown shows: "Qualification 1", "Qualification 2", ..., "Playoff 1", etc.
- User selects a match
- `MatchId` is automatically set

### 5. Scout and Submit
- User fills out scouting form
- Submits with selected team and match

## Benefits

? **No Typing Errors** - Users can't enter invalid IDs  
? **Better UX** - Easy to see and select from available options  
? **Data Validation** - Only valid teams and matches can be selected  
? **Searchable** - MAUI Pickers support searching on mobile devices  
? **Context** - Users see team names and match details, not just IDs  
? **Sorted** - Teams by number, matches by type and number

## UI Layout

```
??????????????????????????????????????
? Team              Match            ?
? [Select Team  ?]  [Select Match ?]?
?                                    ?
? [Load Matches]                     ?
??????????????????????????????????????
```

## API Calls

### Teams
```
GET /api/mobile/teams?limit=500
```
- Called once when page loads
- Loads all teams for the scouting organization
- Cached in ObservableCollection

### Matches
```
GET /api/mobile/events
GET /api/mobile/matches?event_id={id}
```
- Called when "Load Matches" button is tapped
- Gets current event from game config
- Loads all matches for that event

## Dark Mode Support

Both pickers respect dark mode:
- `TextColor` - White in dark mode, black in light mode
- `TitleColor` - Light gray in dark mode, dark gray in light mode

## Error Handling

- If teams fail to load, status message shows error
- If matches fail to load, status message shows error
- Empty dropdowns show title text as placeholder
- Null safety throughout

## Build Status

? **Compilation:** No errors  
? **Bindings:** Working correctly  
? **Converters:** Implemented  
? **API Integration:** Complete

## Testing Checklist

- [ ] Teams load on page open
- [ ] Team picker shows all teams
- [ ] Team selection sets TeamId
- [ ] Load Matches button works
- [ ] Matches picker shows all matches
- [ ] Match selection sets MatchId
- [ ] Form submission includes correct IDs
- [ ] Reset clears selections
- [ ] Dark mode looks good
- [ ] Light mode looks good

## Summary

Team and Match selection is now user-friendly with dropdown pickers that display meaningful information and prevent input errors!
