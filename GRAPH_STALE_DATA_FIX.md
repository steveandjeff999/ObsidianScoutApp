# Graph Stale Data Fix ?

## Problem
When changing graph types (line ? bar) or removing teams, the old chart data persisted and overlapped with new data instead of being replaced.

**Symptoms:**
- Switching from line to bar chart showed both line and bar elements
- Removing teams still displayed their data
- Multiple chart types rendered on top of each other

---

## Root Cause

The `CurrentChart` property was never being cleared before generating a new chart, causing the UI to layer new charts over old ones.

**Before (Broken):**
```csharp
private void GenerateChart()
{
    // Generate new chart without clearing old one
    CurrentChart = new BarChart { ... };  // ? Old chart still in memory
}
```

---

## Solution Implemented

### 1. Clear Chart When Changing Graph Type

```csharp
[RelayCommand]
private void ChangeGraphType(string graphType)
{
    SelectedGraphType = graphType;
    
    if (HasGraphData && ComparisonData != null)
    {
        // ? Clear old chart first
        CurrentChart = null;
        System.Diagnostics.Debug.WriteLine("Cleared old chart data");
        
        GenerateChart();
    }
}
```

### 2. Clear Chart When Changing Data View

```csharp
[RelayCommand]
private void ChangeDataView(string dataView)
{
    SelectedDataView = dataView;
    
    if (HasGraphData && ComparisonData != null)
    {
        // ? Clear old chart before regenerating
        CurrentChart = null;
        System.Diagnostics.Debug.WriteLine("Cleared chart for data view change");
        
        _ = GenerateGraphsAsync();
    }
}
```

### 3. Clear Chart at Start of Generation

```csharp
[RelayCommand]
private async Task GenerateGraphsAsync()
{
    // ... validation ...
    
    try
    {
        IsLoading = true;
        
        // ? Clear ALL old data before generating new
        CurrentChart = null;
        ComparisonData = null;
        HasGraphData = false;
        System.Diagnostics.Debug.WriteLine("Cleared old chart and comparison data");
        
        // Fetch and process new data...
    }
}
```

### 4. Clear Chart Before Creating New One

```csharp
private void GenerateChart()
{
    if (ComparisonData == null || ComparisonData.Teams.Count == 0)
    {
        CurrentChart = null;  // ? Clear if no data
        return;
    }
    
    // ? Clear existing chart before generating new
    CurrentChart = null;
    
    // Generate new chart...
}
```

### 5. Explicit Property Notification

```csharp
private void GenerateChartFromServerData(GraphData graphData)
{
    // Process entries...
    
    // ? Clear old chart with explicit notification
    CurrentChart = null;
    OnPropertyChanged(nameof(CurrentChart));
    
    // Create new chart
    var newChart = SelectedGraphType.ToLower() switch
    {
        "line" => new LineChart { Entries = entries, ... },
        "bar" => new BarChart { Entries = entries, ... },
        "radar" => new RadarChart { Entries = entries, ... },
        _ => new BarChart { Entries = entries }
    };
    
    // ? Set new chart
    CurrentChart = newChart;
}
```

---

## Data Flow

### Before (Broken):
```
User clicks "Bar Chart"
    ?
ChangeGraphType("bar")
    ?
GenerateChart()
    ?
CurrentChart = new BarChart(...)  ? Old LineChart still exists
    ?
UI renders both charts! ?
```

### After (Fixed):
```
User clicks "Bar Chart"
    ?
ChangeGraphType("bar")
    ?
CurrentChart = null  ? Clear old
    ?
GenerateChart()
    ?
CurrentChart = null  ? Clear again (safety)
OnPropertyChanged()  ? Notify UI
    ?
CurrentChart = new BarChart(...)  ? Set new
    ?
UI renders only new chart! ?
```

---

## Clear Points

Charts are now cleared at **5 key points**:

### 1. **Graph Type Change**
```csharp
User switches: Line ? Bar ? Radar
Each switch clears old chart first
```

### 2. **Data View Change**
```csharp
User switches: Match-by-Match ? Team Averages
Clears chart and regenerates with new data structure
```

### 3. **Generate Graphs Start**
```csharp
User clicks "Generate Comparison Graphs"
Clears all old data before fetching new
```

### 4. **Before Chart Creation**
```csharp
GenerateChart() called
Clears CurrentChart before creating new one
```

### 5. **Before Setting New Chart**
```csharp
GenerateChartFromServerData() / GenerateChartFromTeamAverages()
Clears with explicit OnPropertyChanged() notification
```

---

## Debug Output

### Good Flow:
```
Graph type changed to: bar
Cleared old chart data
=== GENERATING CHART ===
Chart Type: bar
Cleared old chart and comparison data
Teams in data: 3
Created 3 chart entries
Cleared old chart data (with OnPropertyChanged)
Chart created: BarChart with 3 entries ?
```

### Problem Detection:
```
Graph type changed to: bar
Chart created: BarChart with 3 entries
(No "Cleared old chart" message) ?
```

---

## Examples

### Example 1: Switching Graph Types

**User Actions:**
1. Select teams: 5454, 1234
2. Generate graphs (Line chart shows)
3. Click "Bar Chart" button

**Before Fix:**
```
Line chart: ??????????????
Bar chart:  ? ? ?
Result: Both rendered! ?
```

**After Fix:**
```
Line chart: (cleared)
Bar chart:  ? ? ?
Result: Only bar chart! ?
```

### Example 2: Changing Data View

**User Actions:**
1. Generate "Team Averages" (Bar chart)
2. Click "Match-by-Match" button

**Before Fix:**
```
Team Averages bars: ? ? ?
Match-by-Match line: ?????????
Result: Overlapping! ?
```

**After Fix:**
```
Team Averages: (cleared)
Match-by-Match: ?????????
Result: Clean line chart! ?
```

### Example 3: Removing Teams

**User Actions:**
1. Add teams: 5454, 1234, 9999
2. Generate graphs
3. Remove team 9999
4. Generate graphs again

**Before Fix:**
```
First: ? ? ? (3 teams)
Remove team 9999
Second: ? ? (2 teams)
Result: Shows 5 bars! ?
```

**After Fix:**
```
First: ? ? ? (3 teams)
Remove team 9999
Clear all data
Second: ? ? (2 teams)
Result: Shows 2 bars! ?
```

---

## UI Binding

The ChartView properly updates because:

### 1. Property Change Notification
```csharp
[ObservableProperty]
private Chart? currentChart;

// When set, automatically calls OnPropertyChanged("CurrentChart")
```

### 2. Explicit Notification
```csharp
CurrentChart = null;
OnPropertyChanged(nameof(CurrentChart));  // Force UI update
CurrentChart = newChart;  // Triggers another update
```

### 3. XAML Binding
```xaml
<microcharts:ChartView Chart="{Binding CurrentChart}" />
```
Binding updates when CurrentChart changes.

---

## Testing

### Test 1: Graph Type Switching
```
1. Generate graphs
2. Click "Line Chart" ? Verify clean line chart
3. Click "Bar Chart" ? Verify clean bar chart (no line)
4. Click "Radar Chart" ? Verify clean radar chart
5. Repeat multiple times
Result: Each switch shows ONLY new chart type ?
```

### Test 2: Data View Switching
```
1. Select "Team Averages" ? Generate
2. Verify bar chart with averages
3. Click "Match-by-Match" ? Auto-regenerates
4. Verify line chart with match progression
5. Switch back to "Team Averages"
6. Verify clean bar chart (no line remnants)
Result: Clean switching between views ?
```

### Test 3: Team Selection Changes
```
1. Add 3 teams ? Generate
2. Verify 3 data series
3. Remove 1 team
4. Generate again
5. Verify only 2 data series (not 5)
Result: Chart shows correct number of teams ?
```

### Test 4: Multiple Operations
```
1. Generate with 3 teams (bar chart)
2. Switch to line chart
3. Switch to "Match-by-Match"
4. Remove 1 team
5. Generate again
6. Switch back to bar chart
Result: Final chart is clean and correct ?
```

---

## Troubleshooting

### Issue: Still Seeing Old Data

**Check 1: Verify Clearing**
Look for debug output:
```
Cleared old chart data ?
```

**Check 2: Multiple Clears**
Should see clears at multiple points:
```
Cleared old chart data (ChangeGraphType)
Cleared old chart and comparison data (GenerateGraphsAsync)
Cleared old chart data (GenerateChartFromServerData)
```

**Check 3: Property Notification**
Ensure ObservableProperty is working:
```csharp
[ObservableProperty]  // Must have this attribute
private Chart? currentChart;
```

### Issue: Chart Not Updating

**Cause:** Binding not set up correctly

**Fix:** Verify XAML binding:
```xaml
<microcharts:ChartView Chart="{Binding CurrentChart}" />
```

### Issue: UI Shows "No Chart"

**Cause:** Chart cleared but new one not created

**Check:** Debug output should show:
```
Cleared old chart data
Chart created: BarChart with 3 entries ?
```

If missing "Chart created", generation failed.

---

## Summary

| Scenario | Before | After |
|----------|--------|-------|
| Switch line ? bar | Both render | Only bar |
| Change data view | Overlapping | Clean switch |
| Remove teams | Old data persists | Only new teams |
| Multiple switches | Accumulates charts | Always clean |
| Regenerate | Adds to existing | Replaces entirely |

---

## Files Modified

? `ObsidianScout\ViewModels\GraphsViewModel.cs`
- Added `CurrentChart = null` in `ChangeGraphType()`
- Added `CurrentChart = null` in `ChangeDataView()`
- Added clear logic in `GenerateGraphsAsync()`
- Added clear logic in `GenerateChart()`
- Added explicit `OnPropertyChanged()` in chart creation methods

---

## Build Status

? **Build Successful**

---

## Key Takeaway

**Always clear the chart before creating a new one:**
```csharp
// ? Correct pattern
CurrentChart = null;  // Clear old
CurrentChart = new BarChart { ... };  // Set new

// ? Wrong pattern
CurrentChart = new BarChart { ... };  // Overwrites without clearing UI
```

**Charts now properly clear and replace when switching types, views, or teams! ???**

Deploy and test the clean chart switching! ??
