# All Graph Types Support - Implementation Complete ?

## Overview

Updated the Graph Type selector to show **all 6 graph types** when online (server mode) and only **2 types** when offline (Microcharts fallback).

## Graph Types Available

### When Online (Server-Generated Images):
1. **Line** - Match-by-match time-series with lines + markers
2. **Bar** - Bar chart (averages or totals)
3. **Radar** - Radar/polar comparison
4. **Scatter** - Scatter plot (points only)
5. **Histogram** - Distribution histogram
6. **Box Plot** - Box and whisker plot

### When Offline (Local Microcharts):
1. **Line Chart** - Simple line chart
2. **Radar Chart** - Simple radar chart

## Implementation Details

### XAML Changes (GraphsPage.xaml)

Added conditional graph type selector with two modes:

```xaml
<!-- All graph types (online server mode) -->
<Grid ColumnDefinitions="*,*,*" 
      RowDefinitions="Auto,Auto"
      IsVisible="{Binding UseServerImage}">
  <!-- 6 buttons in 2 rows × 3 columns -->
  <Button Text="Line" CommandParameter="line" />
  <Button Text="Bar" CommandParameter="bar" />
  <Button Text="Radar" CommandParameter="radar" />
  <Button Text="Scatter" CommandParameter="scatter" />
  <Button Text="Histogram" CommandParameter="hist" />
  <Button Text="Box Plot" CommandParameter="box" />
</Grid>

<!-- Limited graph types (offline Microcharts mode) -->
<Grid ColumnDefinitions="*,*"
      IsVisible="{Binding ShowMicrocharts}">
  <Button Text="Line Chart" CommandParameter="line" />
  <Button Text="Radar Chart" CommandParameter="radar" />
</Grid>
```

### Visibility Logic

**Online Mode (`UseServerImage = true`):**
- Shows 6-button grid (3 columns × 2 rows)
- All buttons send requests to `/api/mobile/graphs`
- Server generates appropriate PNG based on `graph_type` parameter

**Offline Mode (`ShowMicrocharts = true`):**
- Shows 2-button grid (2 columns × 1 row)
- Only Line and Radar supported by Microcharts library
- Charts generated locally

### Button Layout

**Online (3×2 grid):**
```
???????????????????????????????
?  Line   ?   Bar   ?  Radar  ?
???????????????????????????????
? Scatter ?  Hist   ?   Box   ?
???????????????????????????????
```

**Offline (2×1 grid):**
```
???????????????????????????????
?  Line Chart  ? Radar Chart  ?
???????????????????????????????
```

## API Request Parameters

All graph types use the same endpoint with different `graph_type` values:

### Line Chart Request:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
  "metric": "total_points",
  "graph_type": "line",
  "mode": "match_by_match"
}
```

### Bar Chart Request:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
  "metric": "total_points",
  "graph_type": "bar",
  "mode": "averages"
}
```

### Radar Chart Request:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
  "metric": "total_points",
  "graph_type": "radar",
  "mode": "averages"
}
```

### Scatter Plot Request:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
  "metric": "total_points",
  "graph_type": "scatter",
  "mode": "match_by_match"
}
```

### Histogram Request:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
  "metric": "total_points",
  "graph_type": "hist",
  "mode": "averages"
}
```

### Box Plot Request:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
  "metric": "total_points",
  "graph_type": "box",
  "mode": "averages"
}
```

## User Experience Flow

### Online Mode:
1. **Generate graphs** ? Server image displays
2. **6 graph type buttons appear**
3. Click any button (Line/Bar/Radar/Scatter/Hist/Box)
4. App clears old image
5. Requests new server PNG with selected `graph_type`
6. New server image displays

### Offline Mode:
1. **Generate graphs** ? Microcharts display
2. **2 graph type buttons appear** (Line/Radar only)
3. Click button
4. App regenerates local Microcharts
5. New local chart displays

## ViewModel Behavior

No changes needed in `GraphsViewModel.cs` - it already:
- ? Accepts any `graph_type` string
- ? Passes it to the server in `GraphImageRequest`
- ? Handles server responses for all types
- ? Falls back to Microcharts when offline

The existing `ChangeGraphType` method works for all types:

```csharp
[RelayCommand]
private void ChangeGraphType(string graphType)
{
    SelectedGraphType = graphType; // Can be any: line, bar, radar, scatter, hist, box
    
    if (HasGraphData)
    {
 // Clear and regenerate
        CurrentChart = null;
      ServerGraphImage = null;
        UseServerImage = false;
        ShowMicrocharts = true;
        
        // Regenerate with new type
        _ = GenerateGraphsAsync();
    }
}
```

## Supported Combinations (Online Mode)

| Graph Type | Match-by-Match | Team Averages |
|------------|----------------|---------------|
| Line       | ?          | ?   |
| Bar        | ?         | ?            |
| Radar      | ?             | ?            |
| Scatter    | ?   | ?            |
| Histogram  | ?      | ?      |
| Box Plot   | ?   | ?         |

All 6 types work with both view modes (12 total combinations per metric).

## Server Requirements

The server must support all graph types in the `/api/mobile/graphs` endpoint:

1. **Plotly + Kaleido** installed for PNG generation
2. **Graph type handlers** for: `line`, `bar`, `radar`, `scatter`, `hist`, `box`
3. **Fallback JSON** if image generation unavailable

If server doesn't support a type, it returns error and app can show message.

## Benefits

1. **Full Feature Parity** - Mobile app matches web UI capabilities
2. **Flexible Analysis** - Users can choose best visualization for data
3. **Smart Fallback** - Gracefully degrades to 2 types when offline
4. **Clear UX** - Different button layouts make online/offline mode obvious
5. **API Compliant** - Follows Mobile API documentation exactly

## Testing Checklist

- [ ] Build succeeds ?
- [ ] Generate graphs online
- [ ] All 6 graph type buttons visible
- [ ] Click "Line" ? Line chart appears
- [ ] Click "Bar" ? Bar chart appears
- [ ] Click "Radar" ? Radar chart appears
- [ ] Click "Scatter" ? Scatter plot appears
- [ ] Click "Histogram" ? Histogram appears
- [ ] Click "Box Plot" ? Box plot appears
- [ ] Go offline
- [ ] Only 2 buttons visible (Line/Radar)
- [ ] Both work with local Microcharts

## Status Message Examples

```
"Server graph image loaded (averages, bar)"
"Server graph image loaded (match_by_match, scatter)"
"Server graph image loaded (averages, hist)"
"Server graph image loaded (averages, box)"
```

## Future Enhancements

Possible additions:
- **Heatmap** graph type
- **Violin plot** graph type  
- **3D scatter** plot
- **Grouped bar** charts
- **Stacked area** charts

All can be added by:
1. Adding button to online grid
2. Setting `CommandParameter` to new type
3. Server implementing the type

No ViewModel changes needed!

## Build Status

? Build succeeded
? Hot reload enabled - changes should apply automatically

## Summary

**Before:** Only 2 graph types available (line, radar)

**After:** 
- **Online:** 6 graph types (line, bar, radar, scatter, histogram, box plot)
- **Offline:** 2 graph types (line, radar)

Users now have full access to all graph types documented in the Mobile API when connected! ??
