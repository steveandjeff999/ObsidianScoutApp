# Complete Graph View Mode Controls Fix - Final ?

## Problem Summary
1. View Mode and Graph Type buttons were **hidden** when server images displayed
2. Graph Type changes didn't trigger **server image regeneration**
3. Users couldn't switch between Match-by-Match / Team Averages or Line / Radar when viewing server graphs

## Complete Solution Applied

### 1. XAML Changes (GraphsPage.xaml)

**Removed `IsVisible="{Binding ShowMicrocharts}"` from:**
- Data View Selector Border (Match-by-Match / Team Averages buttons)
- Graph Type Selector Grid (Line Chart / Radar Chart buttons)
- Team Comparison Summary CollectionView

**Result:** These controls are now **always visible** regardless of server/local graph mode.

**Kept `IsVisible="{Binding ShowMicrocharts}"` on:**
- TeamChartsWithInfo CollectionView (local Microcharts for match-by-match)
- Single Chart Border (local Microcharts for averages)
- Metric label showing "Using Microcharts"

**Result:** Local charts only show when server image is NOT displayed (no duplication).

### 2. ViewModel Changes (GraphsViewModel.cs)

#### A. `ChangeDataView` Method - Already Working ?
- Clears `ServerGraphImage`, `CurrentChart`, `ComparisonData`
- Sets `UseServerImage = false`, `ShowMicrocharts = true`
- Calls `GenerateGraphsAsync()` to regenerate with new mode

#### B. `ChangeGraphType` Method - **NOW FIXED** ?
**Before Fix:**
```csharp
// Only regenerated local Microcharts
CurrentChart = null;
OnPropertyChanged(nameof(CurrentChart));
Task.Delay(50).ContinueWith(_ => { ... GenerateChart(); ... });
```

**After Fix:**
```csharp
// Now regenerates BOTH server images AND local charts
CurrentChart = null;
ServerGraphImage = null;
UseServerImage = false;
ShowMicrocharts = true;

OnPropertyChanged(nameof(CurrentChart));
OnPropertyChanged(nameof(ServerGraphImage));
OnPropertyChanged(nameof(UseServerImage));
OnPropertyChanged(nameof(ShowMicrocharts));

_ = GenerateGraphsAsync();  // Full regeneration including server request
```

#### C. `GenerateGraphsAsync` Method - Handles All Requests
- Checks if online and offline mode disabled
- Builds `GraphImageRequest` with:
  - `GraphType = SelectedGraphType` (line/radar)
  - `Mode = SelectedDataView == "match_by_match" ? "match_by_match" : "averages"`
  - `DataView = SelectedDataView == "match_by_match" ? "matches" : SelectedDataView`
- Sends request to `/api/mobile/graphs`
- On success: displays server PNG
- On failure: falls back to local Microcharts

## How It Works Now

### User Experience Flow:

```
1. Generate Graphs (online)
   ?
?? Server image appears
? View Mode buttons visible (Match-by-Match / Team Averages)
? Graph Type buttons visible (Line Chart / Radar Chart)
? Team summary visible
? Local Microcharts hidden

2. Click "Match-by-Match"
   ?
?? Clears server image
?? Requests new server PNG with mode=match_by_match
?? New server image appears with match-by-match data

3. Click "Team Averages"
   ?
?? Clears server image
?? Requests new server PNG with mode=averages
?? New server image appears with average data

4. Click "Line Chart"
   ?
?? Clears server image
?? Requests new server PNG with graph_type=line
?? New server image appears as line chart

5. Click "Radar Chart"
   ?
?? Clears server image
?? Requests new server PNG with graph_type=radar
?? New server image appears as radar chart
```

### When Offline or Server Unavailable:

```
1. Generate Graphs (offline)
   ?
?? Local Microcharts appear
? View Mode buttons visible
? Graph Type buttons visible
? Team summary visible
? Microcharts visible

2. Click "Match-by-Match"
   ?
?? Clears local charts
?? Regenerates local Microcharts with match data

3. Click "Team Averages"
   ?
?? Clears local charts
?? Regenerates local Microcharts with averages

(Graph type changes work the same way)
```

## Server Request Parameters

### Match-by-Match + Line Chart:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
  "metric": "total_points",
  "graph_type": "line",
  "mode": "match_by_match",
  "data_view": "matches"
}
```

### Team Averages + Line Chart:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
"metric": "total_points",
  "graph_type": "line",
  "mode": "averages",
  "data_view": "averages"
}
```

### Match-by-Match + Radar Chart:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
  "metric": "total_points",
  "graph_type": "radar",
  "mode": "match_by_match",
  "data_view": "matches"
}
```

### Team Averages + Radar Chart:
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 7,
  "metric": "total_points",
  "graph_type": "radar",
  "mode": "averages",
  "data_view": "averages"
}
```

## Debug Logging

The code now outputs clear debug messages:

```
=== CHANGING GRAPH TYPE ===
From: line ? To: radar
Cleared chart and server image for graph type change
Cleared old chart and comparison data
Requesting server-generated graph image...
Server image request: GraphType=radar, Mode=match_by_match, DataView=matches
? Server image loaded successfully for match_by_match view
```

```
Data view changed to: averages
Cleared chart and server image for data view change
Cleared old chart and comparison data
Requesting server-generated graph image...
Server image request: GraphType=line, Mode=averages, DataView=averages
? Server image loaded successfully for averages view
```

## Testing Checklist

To verify the fix works:

- [ ] Build succeeds without errors ?
- [ ] Generate graphs with server online
- [ ] Server image displays
- [ ] View Mode buttons visible below image
- [ ] Graph Type buttons visible below image
- [ ] Team summary visible
- [ ] Click "Match-by-Match" ? new server image loads with match data
- [ ] Click "Team Averages" ? new server image loads with averages
- [ ] Click "Line Chart" ? new server image loads as line
- [ ] Click "Radar Chart" ? new server image loads as radar
- [ ] Generate graphs offline ? Microcharts display
- [ ] Buttons still work with Microcharts
- [ ] Status message shows correct mode

## Files Modified

1. **ObsidianScout/Views/GraphsPage.xaml**
   - Removed `IsVisible="{Binding ShowMicrocharts}"` from 3 controls
   - Added comments "ALWAYS VISIBLE" for clarity

2. **ObsidianScout/ViewModels/GraphsViewModel.cs**
 - Updated `ChangeGraphType` to regenerate server images
   - Matches behavior of `ChangeDataView` method
   - Properly clears state and calls `GenerateGraphsAsync()`

## Build Status

? Build successful
? Hot reload enabled - changes may apply automatically

## Next Steps

If debugging:
1. **Changes should apply automatically** via hot reload
2. If not, **stop debugging** (Shift+F5)
3. **Rebuild** if needed
4. **Start debugging** again (F5)

You should now see the buttons and be able to switch between view modes and graph types! The server will generate new images with the correct parameters each time. ??

## API Compliance

The implementation follows the Mobile API documentation:
- ? Uses `graph_type` for chart type (`line`, `radar`)
- ? Uses `mode` for data aggregation (`match_by_match`, `averages`)
- ? Uses `data_view` for backward compatibility
- ? Passes all required parameters correctly
- ? Handles server responses and fallback gracefully
