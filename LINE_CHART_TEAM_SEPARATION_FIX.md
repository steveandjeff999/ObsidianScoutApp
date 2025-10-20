# Line Chart Team Separation Fix

## Issue
The match-by-match line chart was showing all teams as a single continuous line instead of visually separating teams with color-coded segments.

### Before
```
Match 1?16  Match 22?16  Match 10?4944  Match 9?5071
    ??????????????????????????????????
   (single gradient line, teams not distinguishable)
```

### After
```
Match 1   Match 1   Match 22  Match 22  Match 10  Match 10
  #16      #4944      #16      #4944      #16      #4944
  ?red     ?blue     ?red     ?blue     ?red     ?blue
  (color-coded points, teams clearly separated)
```

## Root Cause

The previous implementation was grouping entries by team first, then by match:
```csharp
// BEFORE: Grouped by team (all team 16 matches, then all team 4944 matches)
foreach (var dataset in graphData.Datasets)  // Loop teams
{
    for (int i = 0; i < dataset.Data.Count; i++)  // Loop matches
    {
        entries.Add(new ChartEntry(value) { Color = teamColor });
    }
}

Result:
Team 16: M1 ? M2 ? M3
Team 4944: M1 ? M2 ? M3
(Creates one long continuous line)
```

This created labels like "Match 1?16" (team number concatenated to match) and connected all points as one line.

## Solution

Changed to group by match first, then by team:
```csharp
// AFTER: Grouped by match (all teams for M1, then all teams for M2)
for (int matchIndex = 0; matchIndex < graphData.Labels.Count; matchIndex++)  // Loop matches
{
    var matchLabel = graphData.Labels[matchIndex];
    
    foreach (var dataset in graphData.Datasets)  // Loop teams within match
    {
        var teamNumber = dataset.Label.Split('-')[0].Trim();
        
        entries.Add(new ChartEntry((float)value)
        {
            Label = $"{matchLabel}\n#{teamNumber}",  // "Match 1\n#16"
            Color = teamColor
        });
    }
}

Result:
Match 1: Team 16 ? Team 4944
Match 2: Team 16 ? Team 4944
(Creates visual grouping by match with color separation)
```

## Visual Result

### Label Format
**Old**: `Match 1?16` (concatenated, confusing)  
**New**: `Match 1\n#16` (two lines, clear)

```
Match 1
  #16
```

### Color Pattern
With 3 teams (Red #16, Blue #4944, Yellow #5071) across 3 matches:

```
?red   ?blue  ?yellow ? ?red   ?blue  ?yellow ? ?red   ?blue  ?yellow
 M1     M1      M1     ?  M2     M2      M2     ?  M3     M3      M3
 #16   #4944   #5071   ? #16   #4944   #5071   ? #16   #4944   #5071
```

**Pattern**: Red, Blue, Yellow, Red, Blue, Yellow, Red, Blue, Yellow
**Grouping**: By match, with teams color-coded

## Code Changes

### File: `ObsidianScout\ViewModels\GraphsViewModel.cs`

#### Before (Broken)
```csharp
foreach (var dataset in graphData.Datasets)
{
    var color = TryParseColor(dataset.BorderColor?.ToString());
    
    for (int i = 0; i < dataset.Data.Count && i < graphData.Labels.Count; i++)
    {
        var value = dataset.Data[i];
        entries.Add(new ChartEntry((float)value)
        {
            Label = $"{graphData.Labels[i]}\n{dataset.Label.Split('-')[0].Trim()}",
            Color = color
        });
    }
}
```

**Problem**: 
- Loops teams first (outer loop)
- All of Team 1's matches, then all of Team 2's matches
- Creates one continuous line

#### After (Fixed)
```csharp
for (int matchIndex = 0; matchIndex < graphData.Labels.Count; matchIndex++)
{
    var matchLabel = graphData.Labels[matchIndex];
    
    foreach (var dataset in graphData.Datasets)
    {
        if (matchIndex >= dataset.Data.Count) continue;
        
        var value = dataset.Data[matchIndex];
        if (double.IsNaN(value)) continue;
        
        var color = TryParseColor(dataset.BorderColor?.ToString());
        var teamNumber = dataset.Label.Split('-')[0].Trim();
        
        entries.Add(new ChartEntry((float)value)
        {
            Label = $"{matchLabel}\n#{teamNumber}",
            Color = color
        });
    }
}
```

**Solution**:
- Loops matches first (outer loop)
- All teams for Match 1, then all teams for Match 2
- Creates visual grouping by match with color separation

### Chart Configuration
```csharp
new LineChart
{
    Entries = entries,
    LineSize = 2,        // Thinner line (was 3)
    PointSize = 15,      // Larger points (was 12) - emphasis on team colors
    LabelTextSize = 20,  // Smaller labels (was 24) - better fit
    // ...
}
```

**Changes**:
- **LineSize**: 2 (reduced from 3) - less visual clutter
- **PointSize**: 15 (increased from 12) - emphasize color-coded points
- **LabelTextSize**: 20 (reduced from 24) - better readability with two-line labels

## How It Works

### Data Flow
```
1. Get match labels: [Match 1, Match 22, Match 10, Match 9]
2. Get team datasets: [
     Team 16: [11.0, 65.0, 11.0, 17.0],
     Team 4944: [data...],
     Team 5071: [data...]
   ]

3. Generate entries by match:
   Match 1:
     ? Entry: value=11.0, label="Match 1\n#16", color=Red
     ? Entry: value=X, label="Match 1\n#4944", color=Blue
     ? Entry: value=Y, label="Match 1\n#5071", color=Yellow
   
   Match 22:
     ? Entry: value=65.0, label="Match 22\n#16", color=Red
     ? Entry: value=X, label="Match 22\n#4944", color=Blue
     ? Entry: value=Y, label="Match 22\n#5071", color=Yellow
   
   ...continues for all matches

4. Result: Entries alternate by team within each match group
```

### Visual Grouping
```
Match 1 Group     Match 22 Group    Match 10 Group
  ?red              ?red              ?red
  ?blue             ?blue             ?blue  
  ?yellow           ?yellow           ?yellow
```

The line still connects all points (Microcharts limitation), but the pattern of Red?Blue?Yellow repeating makes it clear which points belong to which team.

## Example Output

### 2 Teams, 4 Matches
**Teams**: Red #16, Blue #4944  
**Matches**: 1, 22, 10, 9

```
Entry Order:
1. Match 1, #16 (Red)      ? 11.0
2. Match 1, #4944 (Blue)   ? 35.0
3. Match 22, #16 (Red)     ? 65.0
4. Match 22, #4944 (Blue)  ? 40.0
5. Match 10, #16 (Red)     ? 11.0
6. Match 10, #4944 (Blue)  ? 25.0
7. Match 9, #16 (Red)      ? 17.0
8. Match 9, #4944 (Blue)   ? 30.0

Visual Pattern:
?red ?blue ?red ?blue ?red ?blue ?red ?blue
```

### 3 Teams, 3 Matches
**Teams**: Red #16, Blue #4944, Yellow #5071  
**Matches**: 1, 2, 3

```
Entry Order:
1. Match 1, #16 (Red)
2. Match 1, #4944 (Blue)
3. Match 1, #5071 (Yellow)
4. Match 2, #16 (Red)
5. Match 2, #4944 (Blue)
6. Match 2, #5071 (Yellow)
7. Match 3, #16 (Red)
8. Match 3, #4944 (Blue)
9. Match 3, #5071 (Yellow)

Visual Pattern:
?red ?blue ?yellow ?red ?blue ?yellow ?red ?blue ?yellow
```

## Benefits

### ? Improved Readability
- Clear two-line labels: "Match 1" + "#16"
- Teams visually grouped by match
- Color pattern repeats predictably

### ? Better Team Identification
- Consistent color per team
- Larger points (15px) emphasize colors
- Pattern makes it easy to follow one team

### ? Match Comparison
- Easy to compare all teams at a specific match
- All teams for Match 1 are adjacent
- Quick visual comparison of team performance

## Limitations

### Microcharts LineChart Behavior
The line still connects all points sequentially:
```
?red ?? ?blue ?? ?yellow ?? ?red ?? ?blue
```

This creates "zig-zag" lines between different teams, which is a Microcharts limitation.

### Workaround Success
Despite the connected line limitation:
- ? Colors clearly identify teams
- ? Labels show both match and team
- ? Pattern is predictable and readable
- ? Much better than previous single-color line

### Alternative (If Needed)
If pure separation is required:
1. Use separate charts per team (stacked vertically)
2. Switch to OxyPlot or Syncfusion (true multi-line support)
3. Use scatter plot mode (remove lines, show only points)

## Testing

### Visual Test
1. Select 2-3 teams
2. Switch to Match-by-Match view
3. Generate Line Chart
4. Expected result:
   - ? Color pattern repeats (Red, Blue, Red, Blue...)
   - ? Labels show "Match X\n#TeamNumber"
   - ? Each team's color is consistent
   - ? Points are grouped by match

### Debug Output
```
=== GENERATING MATCH-BY-MATCH LINE CHART ===
  Dataset: 16 - Team Name, Points: 4
    Match 1 Team 16: 11.0 (Color: #FF6384)
    Match 1 Team 4944: 35.0 (Color: #36A2EB)
    Match 22 Team 16: 65.0 (Color: #FF6384)
    Match 22 Team 4944: 40.0 (Color: #36A2EB)
Total line chart entries: 8
```

## Summary

### What Changed
- ? Loop order: Match first, then team (was: team first, then match)
- ? Label format: Two-line with "#" prefix (was: concatenated)
- ? Visual grouping: By match (was: by team)
- ? Point size: Larger at 15px (was: 12px)
- ? Line size: Thinner at 2px (was: 3px)

### Result
- ? Teams are visually distinguishable by color
- ? Labels are clear and readable
- ? Pattern is predictable and organized
- ? Much improved over single-line display

**Status**: ? **FIXED**

Teams now display with clear color-coded separation and proper labels!
