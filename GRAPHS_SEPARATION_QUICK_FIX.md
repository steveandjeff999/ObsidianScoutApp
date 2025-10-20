# Graphs Team Separation - Quick Fix Reference

## What Was Fixed

### Bar Graph (Team Averages)
- **Before**: All teams combined into one bar ?
- **After**: Each team gets its own bar ? ? ?
- **How**: Each team now has its own dataset with unique color

### Line Graph (Match by Match)
- **Before**: Single line for all teams ???
- **After**: Color-coded points per team ???????
- **How**: Each team's data points maintain consistent color

## Key Changes

### 1. Team Averages Data Generation
```csharp
// OLD: Single dataset with all team data
var graphData = new GraphData {
    Datasets = new List<GraphDataset> {
        new GraphDataset { Data = allTeamAverages }
    }
};

// NEW: One dataset per team
foreach (var team in teams) {
    datasets.Add(new GraphDataset {
        Label = $"{teamNumber} - {teamName}",
        Data = new List<double> { avgValue },
        BackgroundColor = color,
        BorderColor = color
    });
}
```

### 2. Bar Chart Entry Generation
```csharp
// OLD: Average all datasets together
var value = allDatasets.Average();
entries.Add(new ChartEntry((float)value) { ... });

// NEW: One entry per dataset (team)
foreach (var dataset in graphData.Datasets) {
    var value = dataset.Data.Average();
    entries.Add(new ChartEntry((float)value) {
        Label = $"#{teamNumber}",
        Color = teamColor
    });
}
```

### 3. Line Chart Entry Generation
```csharp
// OLD: Single color for all points
foreach (var point in allPoints) {
    entries.Add(new ChartEntry(point) { Color = defaultColor });
}

// NEW: Team-specific color for each point
foreach (var dataset in graphData.Datasets) {
    var teamColor = GetTeamColor(dataset);
    foreach (var point in dataset.Data) {
        entries.Add(new ChartEntry(point) {
            Label = $"{matchNumber}\n{teamNumber}",
            Color = teamColor
        });
    }
}
```

## Color Handling

### Team Color Array
```csharp
private readonly string[] TeamColors = new[]
{
    "#FF6384", // Pink
    "#36A2EB", // Blue
    "#FFCE56", // Yellow
    "#4BC0C0", // Teal
    "#9966FF", // Purple
    "#FF9F40"  // Orange
};
```

### Color Assignment
- **Team 1**: Pink (#FF6384)
- **Team 2**: Blue (#36A2EB)
- **Team 3**: Yellow (#FFCE56)
- **Team 4**: Teal (#4BC0C0)
- **Team 5**: Purple (#9966FF)
- **Team 6**: Orange (#FF9F40)

## Chart Configuration

### Bar Chart
```csharp
new BarChart {
    Entries = entries,              // One entry per team
    LabelTextSize = 32,
    ValueLabelTextSize = 18,
    BarAreaAlpha = 255,            // Solid bars, no transparency
    IsAnimated = false             // Clean rendering
}
```

### Line Chart
```csharp
new LineChart {
    Entries = entries,              // All points, color-coded by team
    LineMode = LineMode.Straight,   // Straight lines
    PointMode = PointMode.Circle,   // Show data points
    PointSize = 12,                 // Visible points
    LabelTextSize = 24,
    IsAnimated = false,             // No animation overlap
    EnableYFadeOutGradient = false  // No gradient fill
}
```

## Label Format

### Bar Chart Labels
- Format: `#{teamNumber}`
- Example: `#1234`, `#5678`

### Line Chart Labels
- Format: `{matchNumber}\n{teamNumber}`
- Example: `Match 1\n1234`
- Shows both match and team on two lines

## Data View Modes

### Team Averages (averages)
- One data point per team
- Shows average performance across all matches
- Best viewed as: **Bar Chart**

### Match-by-Match (match_by_match)
- Multiple data points per team (one per match)
- Shows performance trend over matches
- Best viewed as: **Line Chart**

## Troubleshooting

### All bars same color?
- Check that each dataset has its own BackgroundColor
- Verify color assignment in GenerateTeamAveragesData

### Single bar appearing?
- Ensure one dataset per team (not all teams in one dataset)
- Check that Datasets list has multiple entries

### Line chart all one color?
- Verify each ChartEntry has unique Color based on dataset
- Check color is extracted from dataset.BorderColor

### Teams not labeled?
- Verify Labels list matches Datasets list length
- Check label format in entry generation

## Quick Test

1. Select event with data
2. Select 3 teams (e.g., 1234, 5678, 9012)
3. Choose "Total Points" metric
4. Generate graphs

**Expected Result**:
- **Team Averages**: 3 separate bars (pink, blue, yellow)
- **Match by Match**: Multiple colored points with team labels

## File Modified

- `ObsidianScout\ViewModels\GraphsViewModel.cs`
  - `GenerateTeamAveragesData()` - Line ~235
  - `GenerateChartFromServerData()` - Line ~620

## Debug Output

Enable debug mode to see:
```
=== GENERATING TEAM AVERAGES DATA ===
Team 1234: 5 matches
Team 5678: 4 matches
Created averages for 2 teams with 2 datasets

=== GENERATING TEAM AVERAGES BAR CHART ===
  Team 1234: Avg = 45.2, Color = #FF6384
  Team 5678: Avg = 38.7, Color = #36A2EB
Total bar chart entries: 2
```

## Summary

? Bar graphs now show separate bars for each team  
? Line graphs color-code points by team  
? Each team maintains consistent color  
? Labels clearly identify teams  
? Both view modes work correctly  

**Status**: FIXED and TESTED
