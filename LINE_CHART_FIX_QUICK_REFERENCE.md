# Line Chart Fix - Quick Reference

## Issue Fixed
Line chart showed all teams as one continuous line with bad labels like "Match 1?16".

## Solution
Changed entry generation to group by match first, then by team.

## Visual Result

### Before
```
Match 1?16  Match 22?16  Match 10?4944  Match 9?5071
    ??????????????????????????????????
   (single line, teams not visible)
```

### After
```
Match 1   Match 1   Match 22  Match 22
  #16      #4944      #16      #4944
  ?red     ?blue     ?red     ?blue
  (color-coded, teams clearly separated)
```

## Code Change

### Location
File: `ObsidianScout\ViewModels\GraphsViewModel.cs`  
Method: `GenerateChartFromServerData()`  
Section: Match-by-match line chart generation

### Before (Broken)
```csharp
// Teams first ? creates one long line
foreach (var dataset in graphData.Datasets)  // Teams
{
    for (int i = 0; i < dataset.Data.Count; i++)  // Matches
    {
        entries.Add(entry);
    }
}

Pattern: Team1-M1, Team1-M2, Team1-M3, Team2-M1, Team2-M2...
Result: One continuous line
```

### After (Fixed)
```csharp
// Matches first ? creates visual grouping
for (int matchIndex = 0; matchIndex < graphData.Labels.Count; matchIndex++)  // Matches
{
    foreach (var dataset in graphData.Datasets)  // Teams
    {
        var teamNumber = dataset.Label.Split('-')[0].Trim();
        entries.Add(new ChartEntry((float)value)
        {
            Label = $"{matchLabel}\n#{teamNumber}",  // Two-line label
            Color = color
        });
    }
}

Pattern: M1-Team1, M1-Team2, M2-Team1, M2-Team2...
Result: Color-coded repeating pattern
```

## Label Format

### Old
`Match 1?16` - Concatenated, confusing

### New
```
Match 1
  #16
```
Two lines, clear

## Color Pattern

### 2 Teams Example
```
?red ?blue ?red ?blue ?red ?blue
 M1   M1    M2   M2    M3   M3
 #16  #4944 #16  #4944 #16  #4944
```

### 3 Teams Example
```
?red ?blue ?yellow ?red ?blue ?yellow
 M1   M1    M1     M2   M2    M2
```

## Chart Settings

```csharp
new LineChart
{
    LineSize = 2,        // Thinner (was 3)
    PointSize = 15,      // Larger (was 12) - emphasizes colors
    LabelTextSize = 20,  // Smaller (was 24) - better fit
    // ...
}
```

## Benefits

? **Clear Labels**: Two-line format with team number  
? **Visual Grouping**: Teams grouped by match  
? **Color Coded**: Each team maintains consistent color  
? **Predictable**: Pattern repeats (Red, Blue, Red, Blue...)  
? **Readable**: Easier to follow individual teams  

## Testing

1. Select 2-3 teams
2. Switch to "Match-by-Match" view
3. Choose "Line Chart"
4. Generate graphs

**Expected**:
- Color pattern repeats clearly
- Labels show "Match X\n#TeamNumber"
- Each team's points are consistent color
- Easy to identify which points belong to which team

## Known Limitation

?? **Line Still Connects All Points**  
Microcharts connects points sequentially, so you'll see:
```
?red ?? ?blue ?? ?red ?? ?blue
```

The line zig-zags between teams, but **colors make teams clear**.

## Alternative (If Needed)

If you need true separate lines per team:
- Use OxyPlot or Syncfusion libraries
- Create separate charts per team
- Use scatter mode (points only, no lines)

## Quick Comparison

| Aspect | Before | After |
|--------|--------|-------|
| Label | Match 1?16 | Match 1<br>#16 |
| Pattern | Team1-All-Matches, Team2-All-Matches | M1-All-Teams, M2-All-Teams |
| Visual | One continuous line | Color-coded repeating pattern |
| Readability | ? Poor | ? Good |
| Team ID | ? Hard | ? Easy |

## Summary

**Fixed**: Line chart now shows color-coded points grouped by match  
**Labels**: Clear two-line format  
**Pattern**: Red, Blue, Red, Blue (repeating)  
**Result**: Teams are visually distinguishable  

**Status**: ? **WORKING**
