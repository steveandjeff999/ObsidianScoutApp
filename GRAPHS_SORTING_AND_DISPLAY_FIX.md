# Graphs Page Improvements - Complete

## ? Fixes Applied

### 1. **Teams Sorted by Team Number** ?
- Teams are now sorted numerically in both available and selected lists
- **Changes:**
  - `LoadTeamsForEventAsync()` - Sorts teams when loading from API
  - `UpdateAvailableTeams()` - Maintains sort order when filtering

### 2. **Bar Graph Option Removed** ?
- Bar graphs have been removed from the interface
- Default graph type is now "Line Chart"
- **Changes:**
  - Removed "Bar Chart" button from UI
  - Changed default `selectedGraphType` from "bar" to "line"
  - Only Line Chart and Radar Chart options remain

### 3. **Match-by-Match Shows ALL Teams** ?
- Fixed: Now displays separate line chart for EVERY selected team
- Previously only showed the team with the lowest team number
- **Changes:**
  - Added `CollectionView` to display all team charts from `TeamChartsWithInfo`
  - Each team gets its own dedicated line chart
  - Shows team header with color indicator for each chart

## Files Modified

### 1. `ObsidianScout/ViewModels/GraphsViewModel.cs`

#### Changed Default Graph Type
```csharp
// BEFORE
private string selectedGraphType = "bar";

// AFTER  
private string selectedGraphType = "line";
```

#### Added Team Sorting
```csharp
[RelayCommand]
private async Task LoadTeamsForEventAsync()
{
    //...existing code...
    
    // Sort teams by team number
    var sortedTeams = response.Teams.OrderBy(t => t.TeamNumber).ToList();
    Teams = new ObservableCollection<Team>(sortedTeams);
    
    //...existing code...
}

private void UpdateAvailableTeams()
{
    // Filter out teams that are already selected and sort by team number
    var available = Teams
        .Where(t => !SelectedTeams.Any(st => st.TeamNumber == t.TeamNumber))
        .OrderBy(t => t.TeamNumber)  // ? ADDED SORTING
        .ToList();
    
    //...existing code...
}
```

### 2. `ObsidianScout/Views/GraphsPage.xaml`

#### Removed Bar Chart Button
```xml
<!-- BEFORE: 3 buttons -->
<Grid ColumnDefinitions="*,*,*" ColumnSpacing="10">
    <Button Text="Line Chart" ... />
    <Button Text="Bar Chart" ... />  <!-- ? REMOVED -->
    <Button Text="Radar Chart" ... />
</Grid>

<!-- AFTER: 2 buttons -->
<Grid ColumnDefinitions="*,*" ColumnSpacing="10">
    <Button Text="?? Line Chart" ... />
    <Button Text="?? Radar Chart" ... />
</Grid>
```

#### Added Multi-Team Chart Display
```xml
<!-- NEW: Shows ALL team charts for match-by-match -->
<CollectionView ItemsSource="{Binding TeamChartsWithInfo}"
               IsVisible="{Binding TeamChartsWithInfo.Count, Converter={StaticResource IsNotZeroConverter}}">
    <CollectionView.ItemTemplate>
        <DataTemplate x:DataType="vm:TeamChartInfo">
            <VerticalStackLayout Spacing="10" Margin="0,10">
                <!-- Team Header with color -->
                <Grid ColumnDefinitions="Auto,*" ColumnSpacing="10">
                    <BoxView Color="{Binding Color}" ... />
                    <Label>
                        <Label.FormattedText>
                            <FormattedString>
                                <Span Text="Team #" />
                                <Span Text="{Binding TeamNumber}" />
                                <Span Text=" - " />
                                <Span Text="{Binding TeamName}" />
                            </FormattedString>
                        </Label.FormattedText>
                    </Label>
                </Grid>

                <!-- Individual Team Chart -->
                <Border>
                    <microcharts:ChartView Chart="{Binding Chart}" />
                </Border>
            </VerticalStackLayout>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>

<!-- Single Chart Display (for non-match-by-match views) -->
<Border IsVisible="{Binding TeamChartsWithInfo.Count, Converter={StaticResource IsZeroConverter}}">
    <microcharts:ChartView Chart="{Binding CurrentChart}" />
</Border>
```

### 3. `ObsidianScout/Converters/ValueConverters.cs`

#### Added IsZeroConverter
```csharp
public class IsZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue == 0;
        return true;
    }
    
    //...
}
```

## How It Works Now

### Team Sorting
1. When event is selected ? Teams loaded from API
2. Teams sorted by `TeamNumber` ascending (e.g., 254, 1023, 2056, 5805)
3. Available teams list always shows teams in numerical order
4. Selected teams maintain order as added

### Chart Display Logic

#### Match-by-Match View (Line Charts)
- **TeamChartsWithInfo.Count > 0** ? Shows multiple charts
  - One chart per team
  - Each with team header (color + name)
  - Displays ALL selected teams

#### Team Averages View (Line/Radar)
- **TeamChartsWithInfo.Count = 0** ? Shows single chart
  - Uses `CurrentChart` property
  - Shows aggregate view

## Visual Flow

```
User selects teams ? Generate Graphs
                          ?
                   Match-by-Match?
                  ?              ?
              YES                  NO
               ?                    ?
   TeamChartsWithInfo filled    CurrentChart filled
               ?                    ?
   Shows ALL team charts      Shows single chart
   (one per team)             (all teams combined)
```

## Debug Output

When generating charts, you'll see in debug:
```
=== GENERATING SEPARATE LINE CHARTS PER TEAM ===
Creating chart 1 for Team 254 - Cheesy Poofs
  ? Added chart for Team 254 with 8 points
Creating chart 2 for Team 1023 - Bedford Express
  ? Added chart for Team 1023 with 6 points
Creating chart 3 for Team 5805 - Short Circuits
  ? Added chart for Team 5805 with 7 points
=== Created 3 separate team charts ===
TeamCharts collection count: 3
TeamChartsWithInfo collection count: 3
```

## Testing Checklist

### ? Test Team Sorting
1. Select an event
2. Check available teams list
3. **Expected**: Teams shown in numerical order (lowest to highest)

### ? Test Bar Graph Removal
1. Generate graphs
2. Look at graph type buttons
3. **Expected**: Only "Line Chart" and "Radar Chart" buttons visible

### ? Test Match-by-Match Display
1. Select 3-5 teams
2. Choose "Match-by-Match" view
3. Click "Generate Comparison Graphs"
4. **Expected**: 
   - See multiple chart sections
   - One chart per team
   - Each with team header showing team number and name
   - All teams displayed (not just the lowest number)

### ? Test Team Averages Display
1. Select teams
2. Choose "Team Averages" view
3. Generate graphs
4. **Expected**: Single chart showing all teams together

## Benefits

### 1. Better Organization
- Teams listed in logical numerical order
- Easier to find specific team numbers

### 2. Clearer Chart Types
- Removed confusing bar chart option
- Line charts for match-by-match
- Radar charts for multi-dimensional comparison

### 3. Complete Data Visibility
- **ALL selected teams** now visible in match-by-match
- No more missing teams
- Each team clearly identified with colored header

## Summary

? **Teams sorted by number** - Easy to find teams  
? **Bar charts removed** - Cleaner interface  
? **All teams displayed** - No more missing data in match-by-match  

**The graphs page now shows complete data for all selected teams!** ??
