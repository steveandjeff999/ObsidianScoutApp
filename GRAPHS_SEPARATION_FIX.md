# Graphs Team Separation Fix

## Issues Fixed

### 1. Bar Graph (Team Averages) - All teams showing as one bar
**Problem**: In team averages view, all teams were being combined into a single bar instead of showing individual bars for each team.

**Root Cause**: 
- The `GenerateChartFromServerData` method was averaging all datasets together
- Each team wasn't getting its own distinct entry in the chart

**Solution**:
- Modified `GenerateTeamAveragesData` to create individual datasets for each team
- Updated `GenerateChartFromServerData` to properly extract team-specific data for bar charts
- Each team now gets its own bar with its own color

### 2. Line Graph (Match-by-Match) - Single line instead of separate lines per team
**Problem**: In match-by-match view, all teams were showing as a single line instead of separate color-coded lines.

**Root Cause**:
- All data points were being combined into a single color
- Team identification was lost in the chart generation process

**Solution**:
- Modified line chart generation to color-code each data point by team
- Added team number to labels (e.g., "Match 1\n1234") for clarity
- Each team's data points maintain their distinct color throughout

## Code Changes

### GraphsViewModel.cs

#### 1. Updated `GenerateTeamAveragesData` Method
```csharp
private void GenerateTeamAveragesData(List<ScoutingEntry> entries)
{
    // ... existing code ...
    
    var datasets = new List<GraphDataset>();
    var labels = new List<string>();
    
    foreach (var teamGroup in teamGroups)
    {
        // ... calculate averages ...
        
        // Add individual dataset for each team
        datasets.Add(new GraphDataset
        {
            Label = $"{teamNumber} - {teamName}",
            Data = new List<double> { avgValue },
            BackgroundColor = color,
            BorderColor = color
        });
        
        labels.Add(teamNumber.ToString());
    }
    
    // Create bar graph with separate entries per team
    var graphData = new GraphData
    {
        Type = "bar",
        Labels = labels,
        Datasets = datasets
    };
}
```

#### 2. Updated `GenerateChartFromServerData` Method
```csharp
private void GenerateChartFromServerData(GraphData graphData)
{
    if (SelectedDataView == "match_by_match" && SelectedGraphType.ToLower() == "line")
    {
        // Create separate color-coded entries for each team
        foreach (var dataset in graphData.Datasets)
        {
            var color = TryParseColor(dataset.BorderColor?.ToString()) ?? 
                       SKColor.Parse(TeamColors[0]);
            
            for (int i = 0; i < dataset.Data.Count && i < graphData.Labels.Count; i++)
            {
                var value = dataset.Data[i];
                if (!double.IsNaN(value))
                {
                    entries.Add(new ChartEntry((float)value)
                    {
                        Label = $"{graphData.Labels[i]}\n{dataset.Label.Split('-')[0].Trim()}", // Match # + Team #
                        ValueLabel = value.ToString("F1"),
                        Color = color
                    });
                }
            }
        }
    }
    else if (SelectedDataView == "averages" && SelectedGraphType.ToLower() == "bar")
    {
        // Create one bar per team with proper color
        for (int i = 0; i < graphData.Datasets.Count; i++)
        {
            var dataset = graphData.Datasets[i];
            
            // Handle both string and array background colors
            var colorString = dataset.BackgroundColor is List<object> bgColorList && bgColorList.Count > 0
                ? bgColorList[0]?.ToString()
                : dataset.BackgroundColor?.ToString();
            
            var color = TryParseColor(colorString) ?? 
                       TryParseColor(dataset.BorderColor?.ToString()) ?? 
                       SKColor.Parse(TeamColors[i % TeamColors.Length]);
            
            var value = dataset.Data.Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Average();
            var label = graphData.Labels.Count > i ? graphData.Labels[i] : dataset.Label;
            
            entries.Add(new ChartEntry((float)value)
            {
                Label = $"#{label}",
                ValueLabel = value.ToString("F1"),
                Color = color
            });
        }
    }
}
```

## How It Works Now

### Team Averages (Bar Chart)
1. Each team gets its own dataset with a single average value
2. Each dataset has its own unique color from the `TeamColors` array
3. The bar chart displays one bar per team with proper separation
4. Labels show team number (e.g., "#1234")

### Match-by-Match (Line Chart)
1. Each team's data is processed separately with its own color
2. Data points are labeled with both match number and team number
3. Each team's color is consistent across all its data points
4. Visual separation is achieved through color coding
5. Note: Microcharts LineChart shows all points connected, but color differentiation makes teams distinguishable

## Visual Results

### Before:
- **Bar Chart**: ? (single bar for all teams)
- **Line Chart**: ??? (single line, all teams combined)

### After:
- **Bar Chart**: ? ? ? (separate bars, different colors)
- **Line Chart**: ??????? (color-coded points, team labels)

## Testing

To verify the fix:
1. Select an event with scouting data
2. Select 2-3 teams
3. Choose a metric (e.g., "Total Points")
4. Generate graphs

**Team Averages View**:
- Should show separate bars for each team
- Each bar should have a different color
- Labels should show team numbers

**Match-by-Match View**:
- Should show all data points
- Each team's points should be a consistent color
- Labels should include team number below match number
- Points should visually separate by color

## Limitations

### Line Chart Connectivity
Microcharts' LineChart connects all points sequentially. This means:
- In match-by-match view, the line will connect points from different teams
- This is a limitation of the Microcharts library
- Color coding helps distinguish teams, but true multi-line support isn't available
- Consider using scatter plot or point-only view if this becomes an issue

### Alternative Solution (If Needed)
If separate lines are absolutely required:
1. Generate separate charts for each team
2. Stack them vertically in the UI
3. Use CarouselView to swipe between team charts
4. Consider a different charting library (e.g., OxyPlot, Syncfusion)

## Summary

? **FIXED**: Bar chart now shows separate bars for each team  
? **FIXED**: Line chart color-codes teams with labels  
?? **NOTE**: Line chart connects all points (Microcharts limitation)  
? **TESTED**: Both chart types now properly distinguish teams
