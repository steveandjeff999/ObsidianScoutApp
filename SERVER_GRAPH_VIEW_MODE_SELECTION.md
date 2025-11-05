# Server Graph Image View Mode Selection - Implementation Complete

## Summary
Implemented the ability to switch between "Team Averages" and "Match-by-Match" views for server-generated graph images. When you change the view mode, the app now requests a new server image with the appropriate mode parameter.

## Changes Made

### 1. **ChangeDataView Method** (Updated)
**Location:** `ObsidianScout/ViewModels/GraphsViewModel.cs`

**New Behavior:**
- Clears both local charts AND server images when view mode changes
- Triggers a full regeneration via `GenerateGraphsAsync()`
- Server image request will use the new view mode automatically

```csharp
[RelayCommand]
private void ChangeDataView(string dataView)
{
    SelectedDataView = dataView;
    System.Diagnostics.Debug.WriteLine($"Data view changed to: {dataView}");
    
if (HasGraphData)
    {
        // Clear everything and regenerate with new view mode
 CurrentChart = null;
        ServerGraphImage = null;
        UseServerImage = false;
        ShowMicrocharts = true;
        System.Diagnostics.Debug.WriteLine("Cleared chart and server image for data view change");
        
        // Regenerate graphs with new data view (will try server first if online)
    _ = GenerateGraphsAsync();
    }
}
```

### 2. **GenerateGraphsAsync Method** (Enhanced)
**Location:** `ObsidianScout/ViewModels/GraphsViewModel.cs`

**New Features:**
- Added `GraphType` parameter to the server request (was missing before)
- Properly sets `Mode` based on `SelectedDataView`:
  - `"match_by_match"` ? `Mode = "match_by_match"`
  - `"averages"` ? `Mode = "averages"`
- Added debug logging for the request parameters
- Status message now shows which view mode the server image was loaded for

```csharp
var request = new GraphImageRequest
{
 TeamNumbers = SelectedTeams.Select(t => t.TeamNumber).ToList(),
    EventId = SelectedEvent.Id,
    Metric = SelectedMetric.Id,
    GraphType = SelectedGraphType,  // NEW: Pass the selected graph type
    Mode = SelectedDataView == "match_by_match" ? "match_by_match" : "averages",  // NEW: Explicit mode
    DataView = SelectedDataView == "match_by_match" ? "matches" : SelectedDataView
};

System.Diagnostics.Debug.WriteLine($"Server image request: GraphType={request.GraphType}, Mode={request.Mode}, DataView={request.DataView}");
```

## How It Works Now

### User Workflow:
1. **Select Teams** ? Generate graphs (server image loads if online)
2. **Click "Team Averages" button** ? Clears image, requests new server image with `mode=averages`
3. **Click "Match-by-Match" button** ? Clears image, requests new server image with `mode=match_by_match`
4. **Click "Line Chart" or "Radar Chart"** ? Works the same way (was already regenerating)

### Request Parameters Sent to Server:

**For Team Averages:**
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

**For Match-by-Match:**
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

## Visual Behavior

### When Online (Server Available):
1. **Initial Load:** Server image displays
2. **Switch View Mode:** Image briefly disappears ? Loading indicator ? New server image appears
3. **Status Message:** "Server graph image loaded (averages)" or "Server graph image loaded (match_by_match)"

### When Offline or Server Unavailable:
1. **Fallback to Local:** Microcharts generate locally
2. **View Mode Buttons:** Still work, regenerate local charts
3. **Status Message:** "Generating graphs locally..."

## Debug Logging

Enhanced debug output shows:
- When view mode changes
- Clearing of images
- Server request parameters
- Success/failure of server image load
- Fallback to local generation

Example log output:
```
Data view changed to: match_by_match
Cleared chart and server image for data view change
Cleared old chart and comparison data
Requesting server-generated graph image...
Server image request: GraphType=line, Mode=match_by_match, DataView=matches
Received 245678 bytes for graph image
? Server image loaded successfully for match_by_match view
```

## API Compliance

The implementation follows the Mobile API documentation:
- Uses `graph_type` for the chart type (`line`, `bar`, `radar`)
- Uses `mode` for the data aggregation (`match_by_match`, `averages`)
- Uses `data_view` for backward compatibility (`matches`, `averages`)
- Passes `team_numbers`, `event_id`, and `metric` as documented

## Testing Checklist

- [x] Build succeeds without errors
- [ ] Switching to "Team Averages" requests new server image
- [ ] Switching to "Match-by-Match" requests new server image
- [ ] Server image displays for both view modes when online
- [ ] Fallback to Microcharts works when offline
- [ ] Graph type changes (Line/Radar) still work with both view modes
- [ ] Status messages show correct mode
- [ ] Debug logs show correct parameters

## Benefits

1. **Full Server Control:** Server can generate different visualizations for averages vs. match-by-match
2. **Consistent with Local:** Both server and local modes respect the view mode selection
3. **Better UX:** Users can quickly switch between views and see server-optimized graphs
4. **Efficient:** Only requests new images when actually needed (view mode or graph type changes)

## Notes

- Hot reload should apply these changes automatically if debugging
- If not, stop debugging (Shift+F5) and restart (F5)
- The view mode selector buttons are already in the XAML (no UI changes needed)
- Server must implement the `/api/mobile/graphs` endpoint with `mode` and `graph_type` support

## Future Enhancements

Possible improvements:
- Cache server images per view mode to avoid re-requesting
- Show loading spinner specifically for view mode switches
- Add "Refresh" button to manually request new server image
- Support more view modes (per-match breakdown, trends, etc.)
