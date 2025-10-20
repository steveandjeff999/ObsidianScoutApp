# Graph Team Separation - Technical Deep Dive

## Problem Analysis

### Issue 1: Single Bar for All Teams

#### Symptom
When viewing team averages with bar chart, all selected teams appeared as a single combined bar instead of individual bars.

#### Root Cause
```csharp
// BEFORE: Single dataset containing aggregated data
var graphData = new GraphData
{
    Type = "bar",
    Labels = teamDataList.Select(t => t.TeamNumber.ToString()).ToList(),
    Datasets = new List<GraphDataset>
    {
        new GraphDataset
        {
            Label = SelectedMetric!.Name,
            Data = teamDataList.Select(t => t.Value).ToList(),  // ? All teams in one dataset
            BackgroundColor = teamDataList.Select(t => t.Color).ToList()  // ?? Color array not used correctly
        }
    }
};
```

**Why This Failed:**
1. Microcharts BarChart expects one dataset per series
2. When all teams are in one dataset, they're treated as a single series
3. Even with color array, only first color is used
4. Multiple data points in one dataset = single combined bar

#### Solution
```csharp
// AFTER: Individual dataset per team
var datasets = new List<GraphDataset>();
foreach (var teamGroup in teamGroups)
{
    datasets.Add(new GraphDataset
    {
        Label = $"{teamNumber} - {teamName}",
        Data = new List<double> { avgValue },  // ? Single value per dataset
        BackgroundColor = color,                // ? One color per dataset
        BorderColor = color
    });
}

var graphData = new GraphData
{
    Type = "bar",
    Labels = labels,
    Datasets = datasets  // ? Multiple datasets = multiple bars
};
```

**Why This Works:**
1. Each team = separate dataset
2. Microcharts treats each dataset as separate bar
3. Each dataset has its own color
4. Result: Individual colored bars

### Issue 2: Single Line for All Teams

#### Symptom
When viewing match-by-match data with line chart, all teams appeared as a single connected line instead of separate color-coded lines.

#### Root Cause
```csharp
// BEFORE: All points combined with single color
int colorIndex = 0;
foreach (var teamGroup in teamGroups)
{
    // Process team data
    foreach (var matchNum in allMatchNumbers)
    {
        var value = GetMatchValue(matchNum);
        entries.Add(new ChartEntry((float)value)
        {
            Label = $"Match {matchNum}",
            Color = SKColor.Parse(TeamColors[0])  // ? Same color for all
        });
    }
    colorIndex++;  // ?? Incremented but not used
}
```

**Why This Failed:**
1. Color selection outside the entry loop
2. All entries got same default color
3. Team identity lost in flat list of entries
4. Result: Single-colored connected line

#### Solution
```csharp
// AFTER: Team-specific color for each point
foreach (var dataset in graphData.Datasets)
{
    var color = TryParseColor(dataset.BorderColor?.ToString()) ?? 
               SKColor.Parse(TeamColors[0]);  // ? Team color from dataset
    
    for (int i = 0; i < dataset.Data.Count && i < graphData.Labels.Count; i++)
    {
        var value = dataset.Data[i];
        if (!double.IsNaN(value))
        {
            entries.Add(new ChartEntry((float)value)
            {
                Label = $"{graphData.Labels[i]}\n{dataset.Label.Split('-')[0].Trim()}",  // ? Team in label
                ValueLabel = value.ToString("F1"),
                Color = color  // ? Consistent color per team
            });
        }
    }
}
```

**Why This Works:**
1. Color extracted from dataset (unique per team)
2. Each team's points get consistent color
3. Team number added to label
4. Result: Color-coded points distinguishing teams

## Microcharts Limitations

### BarChart Behavior
- **Single Dataset**: All values combined into one bar
- **Multiple Datasets**: Each dataset = separate bar
- **Color**: One color per dataset, not per data point
- **Limitation**: Cannot have grouped bars (all bars are side-by-side)

### LineChart Behavior
- **All Points Connected**: Draws single continuous line through all entries
- **No Multi-Series Support**: Cannot draw separate lines natively
- **Color Per Point**: Each entry can have different color
- **Limitation**: Line connects points sequentially regardless of color

### Workarounds Applied

#### Bar Chart
? **Solution**: One dataset per team
- Natural fit for Microcharts design
- Each team = separate bar
- Full color control

#### Line Chart
?? **Workaround**: Color-coded points with team labels
- Points colored by team
- Labels show team number
- Visual separation through color
- Still one connected line (library limitation)

### Alternative Approaches

If separate lines are required:

#### Option 1: Multiple Chart Instances
```csharp
foreach (var team in teams)
{
    var chart = new LineChart
    {
        Entries = CreateEntriesForTeam(team),
        // ... configuration
    };
    // Display in stacked layout
}
```
**Pros**: True separation  
**Cons**: Vertical space, hard to compare

#### Option 2: Different Library
```csharp
// OxyPlot example
var plotModel = new PlotModel();
foreach (var team in teams)
{
    var series = new LineSeries
    {
        Title = team.Name,
        ItemsSource = team.DataPoints
    };
    plotModel.Series.Add(series);
}
```
**Pros**: True multi-line support, better features  
**Cons**: More complex, larger package, platform compatibility

#### Option 3: Scatter Plot
```csharp
// Remove line, show only points
new LineChart
{
    Entries = entries,
    LineSize = 0,  // No line
    PointMode = PointMode.Circle,
    PointSize = 15  // Larger points
}
```
**Pros**: Clear team separation  
**Cons**: No trend line, harder to follow progression

## Data Flow

### Team Averages Flow
```
ScoutingEntries (raw data)
    ?
GroupBy TeamNumber
    ?
Calculate averages per team
    ?
Create GraphDataset per team {
    Label: "TeamNumber - TeamName"
    Data: [averageValue]
    Color: uniqueColor
}
    ?
Generate BarChart entries (one per dataset)
    ?
Display: Separate bars
```

### Match-by-Match Flow
```
ScoutingEntries (raw data)
    ?
GroupBy TeamNumber
    ?
For each team, create dataset {
    Label: "TeamNumber - TeamName"
    Data: [match1, match2, match3, ...]
    Color: teamColor
}
    ?
For each dataset:
    For each data point:
        Create ChartEntry with team color
    ?
Display: Color-coded connected line
```

## Color Management

### Color Assignment
```csharp
private readonly string[] TeamColors = new[]
{
    "#FF6384",  // Pink    - Team 1
    "#36A2EB",  // Blue    - Team 2
    "#FFCE56",  // Yellow  - Team 3
    "#4BC0C0",  // Teal    - Team 4
    "#9966FF",  // Purple  - Team 5
    "#FF9F40"   // Orange  - Team 6
};
```

### Color Extraction
```csharp
private SKColor? TryParseColor(string? colorString)
{
    if (string.IsNullOrEmpty(colorString)) return null;
    
    try
    {
        return SKColor.Parse(colorString);  // SkiaSharp color parsing
    }
    catch
    {
        return null;
    }
}
```

### Color Application
```csharp
// For datasets (bar chart)
var color = TeamColors[colorIndex % TeamColors.Length];
dataset.BackgroundColor = color;
dataset.BorderColor = color;

// For entries (all charts)
var skColor = TryParseColor(colorString) ?? 
              SKColor.Parse(TeamColors[fallbackIndex % TeamColors.Length]);
entry.Color = skColor;
```

## Testing Scenarios

### Test Case 1: Two Teams, Team Averages
**Setup**: Team 1234 (avg: 45.2), Team 5678 (avg: 38.7)  
**Expected**: 2 bars, different heights, different colors  
**Bar 1**: Height ~45, Pink  
**Bar 2**: Height ~39, Blue  

### Test Case 2: Three Teams, Match-by-Match
**Setup**: Teams 1234, 5678, 9012 with 5 matches each  
**Expected**: 15 colored points (5 per team)  
**Points 1-5**: Pink (Team 1234)  
**Points 6-10**: Blue (Team 5678)  
**Points 11-15**: Yellow (Team 9012)  

### Test Case 3: Single Team
**Setup**: Team 1234 only  
**Expected**: 1 bar (averages) or connected points (match-by-match)  
**Color**: Pink  

### Test Case 4: Six Teams (Max)
**Setup**: Teams 1-6  
**Expected**: All 6 colors used  
**Colors**: Pink, Blue, Yellow, Teal, Purple, Orange  

## Performance Considerations

### Entry Generation
```csharp
// INEFFICIENT (creates many small lists)
foreach (var team in teams)
{
    var list = new List<ChartEntry>();
    list.Add(entry);
    entries.AddRange(list);
}

// EFFICIENT (single list)
var entries = new List<ChartEntry>();
foreach (var team in teams)
{
    entries.Add(entry);
}
```

### Color Parsing
```csharp
// CACHE parsed colors
private readonly Dictionary<string, SKColor> _colorCache = new();

private SKColor GetCachedColor(string colorString)
{
    if (_colorCache.TryGetValue(colorString, out var cached))
        return cached;
    
    var color = SKColor.Parse(colorString);
    _colorCache[colorString] = color;
    return color;
}
```

## Debug Output

### Useful Debug Points
```csharp
// Data generation
System.Diagnostics.Debug.WriteLine($"Created {datasets.Count} datasets");
foreach (var ds in datasets)
{
    System.Diagnostics.Debug.WriteLine($"  {ds.Label}: {ds.Data.Count} points, color: {ds.BackgroundColor}");
}

// Chart creation
System.Diagnostics.Debug.WriteLine($"Creating {entries.Count} chart entries");
foreach (var entry in entries)
{
    System.Diagnostics.Debug.WriteLine($"  {entry.Label}: {entry.Value}, color: {entry.Color}");
}
```

## Summary

### What Changed
1. ? Bar chart: One dataset per team (was: all teams in one dataset)
2. ? Line chart: Color per team in entries (was: single color for all)
3. ? Labels: Include team identification
4. ? Colors: Consistent per team across views

### What Works
- ? Separate bars for each team in averages
- ? Color-coded points for each team in match-by-match
- ? Up to 6 teams with distinct colors
- ? Both view modes display correctly

### Known Limitations
- ?? Line chart connects all points (Microcharts design)
- ?? No true multi-line chart (library limitation)
- ?? Limited to 6 colors (repeats after that)

### Future Improvements
- Consider OxyPlot or Syncfusion for true multi-line support
- Add color picker for custom team colors
- Implement chart zoom/pan for better detail view
- Add data point tooltips on tap
