# Graphs Bug Fixes - Team Selection Persistence and Unlimited Teams

## ?? Bugs Fixed

### Bug #1: Teams Persisting Across Event Changes
**Problem**: When changing events, previously selected teams remained in the `SelectedTeams` collection, causing graphs to show teams from different events.

**Example**: 
- Select teams 16 and 5454 at Event A
- Change to Event B
- Teams 16 and 5454 still show as selected (but might not exist at Event B)
- Generating graphs would show teams 16, 4944, 5971 (mixing old and new teams)

**Root Cause**: The `OnSelectedEventChanged` method was loading new teams but not clearing the selected teams.

### Bug #2: 6-Team Limit
**Problem**: The application had a hard-coded limit of 6 teams for comparison, which was unnecessary and restrictive.

## ? Solutions Implemented

### Fix #1: Clear Selected Teams on Event Change

**File**: `ObsidianScout/ViewModels/GraphsViewModel.cs`

**Before**:
```csharp
partial void OnSelectedEventChanged(Event? value)
{
    if (value != null)
    {
        _ = LoadTeamsForEventAsync();
    }
}
```

**After**:
```csharp
partial void OnSelectedEventChanged(Event? value)
{
    if (value != null)
    {
        // Clear selected teams when event changes to avoid showing teams from previous event
        System.Diagnostics.Debug.WriteLine($"Event changed to: {value.Name}. Clearing selected teams.");
        SelectedTeams.Clear();
        ComparisonData = null;
        HasGraphData = false;
        CurrentChart = null;
        
        _ = LoadTeamsForEventAsync();
    }
}
```

**Changes**:
- ? Clears `SelectedTeams` collection
- ? Resets `ComparisonData` to null
- ? Resets `HasGraphData` to false
- ? Clears `CurrentChart`
- ? Adds debug logging for troubleshooting

### Fix #2: Remove 6-Team Limit

**File**: `ObsidianScout/ViewModels/GraphsViewModel.cs`

**Before**:
```csharp
[RelayCommand]
private void AddTeamToComparison(Team team)
{
    if (!SelectedTeams.Any(t => t.TeamNumber == team.TeamNumber) && SelectedTeams.Count < 6)
    {
        SelectedTeams.Add(team);
        UpdateAvailableTeams();
        StatusMessage = $"Added {team.TeamNumber} - {team.TeamName}";
    }
    else if (SelectedTeams.Count >= 6)
    {
        StatusMessage = "Maximum 6 teams can be compared at once";
    }
}
```

**After**:
```csharp
[RelayCommand]
private void AddTeamToComparison(Team team)
{
    // Removed the 6 team limit - allow unlimited teams
    if (!SelectedTeams.Any(t => t.TeamNumber == team.TeamNumber))
    {
        SelectedTeams.Add(team);
        UpdateAvailableTeams();
        StatusMessage = $"Added {team.TeamNumber} - {team.TeamName} ({SelectedTeams.Count} teams selected)";
    }
}
```

**Changes**:
- ? Removed `&& SelectedTeams.Count < 6` check
- ? Removed "Maximum 6 teams" error message
- ? Added team count to status message
- ? Allows unlimited team comparisons

### Fix #3: Update UI Text

**File**: `ObsidianScout/Views/GraphsPage.xaml`

**Before**:
```xaml
<Label Text="Select 2-6 teams from the list below"
       FontSize="12"
       TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}"
       HorizontalTextAlignment="Center" />
```

**After**:
```xaml
<Label Text="Select teams to compare from the list below"
       FontSize="12"
       TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}"
       HorizontalTextAlignment="Center" />
```

**Changes**:
- ? Removed "2-6" team count reference
- ? Generic message that doesn't imply a limit

## ?? Workflow Changes

### Before:
1. Select Event A ? Teams load
2. Select teams 16, 5454 ? They appear in selected list
3. Change to Event B ? **BUG**: Teams 16, 5454 still selected
4. Select team 4944 ? Now have 16, 5454, 4944 selected (mixed events!)
5. Generate graphs ? Shows wrong teams or causes errors
6. Try to add 7th team ? **BUG**: Blocked by 6-team limit

### After:
1. Select Event A ? Teams load
2. Select teams 16, 5454 ? They appear in selected list
3. Change to Event B ? ? Selected teams automatically cleared
4. Select teams 4944, 5971, ... ? Can select any number of teams
5. Generate graphs ? Shows correct teams from current event only
6. Can add 7th, 8th, 9th team ? ? No limit

## ?? Benefits

### Data Integrity
- ? Prevents mixing teams from different events
- ? Ensures graphs only show data from the selected event
- ? Eliminates confusing "team not found" errors

### User Experience
- ? Cleaner workflow when switching events
- ? More flexibility in team comparisons
- ? Better feedback with team count in status message
- ? No arbitrary limitations

### Performance
- ? Unlimited teams allows for comprehensive comparisons
- ? Efficient with proper color cycling (6 base colors that repeat)
- ? Dynamic chart generation handles any number of teams

## ?? Color Handling for Unlimited Teams

The existing color system already supports unlimited teams:

```csharp
private readonly string[] TeamColors = new[]
{
    "#FF6384", "#36A2EB", "#FFCE56", "#4BC0C0", "#9966FF", "#FF9F40"
};

// Colors cycle using modulo:
var color = TeamColors[colorIndex % TeamColors.Length];
```

- **Teams 1-6**: Each get a unique color
- **Teams 7-12**: Colors cycle back (team 7 gets color of team 1, etc.)
- **Teams 13+**: Continue cycling through the 6 base colors

## ?? Testing Scenarios

### Test Case 1: Event Switching
1. ? Select Event A
2. ? Select teams 16, 5454
3. ? Change to Event B
4. ? Verify selected teams list is empty
5. ? Verify available teams list shows Event B teams

### Test Case 2: Unlimited Teams
1. ? Select an event
2. ? Add 7 teams to selection
3. ? Verify no error message
4. ? Add 3 more teams (10 total)
5. ? Generate graphs successfully
6. ? Verify all 10 teams appear in graphs

### Test Case 3: Mixed Event Prevention
1. ? Select Event A, add team 16
2. ? Change to Event B
3. ? Verify team 16 is no longer selected
4. ? Add team 4944 from Event B
5. ? Generate graphs
6. ? Verify only Event B data appears

## ?? Debug Output

The fix includes enhanced debug logging:

```
Event changed to: Championship. Clearing selected teams.
Available teams updated: 45 teams available, 0 teams selected
Added 16 - Team Alpha (1 teams selected)
Added 5454 - Team Beta (2 teams selected)
Added 4944 - Team Gamma (3 teams selected)
...
```

## ?? Edge Cases Handled

- ? **Switching back to same event**: Team selection cleared, can select teams again
- ? **Empty team list**: Works gracefully with events that have no teams
- ? **Duplicate prevention**: Still prevents selecting same team twice
- ? **Color cycling**: Colors repeat for teams beyond 6
- ? **Graph generation**: Handles any number of teams efficiently

## ?? Future Enhancements

Possible future improvements:
- Option to "remember" team selection across event changes
- Bulk team selection (select multiple at once)
- Team selection presets/favorites
- Color customization for individual teams
- Export comparison data for selected teams

---

**Status**: ? **FIXED AND TESTED**  
**Build**: ? **SUCCESSFUL**  
**Date**: 2025-01-19  
**Impact**: High - Critical bug fix for accurate data visualization
