# Server-Generated Graph Images Implementation Summary

## Overview
Implemented server-generated graph image support for the Graphs page. When online and not in offline mode, the app now requests PNG images from the server's `/api/mobile/graphs` endpoint. If successful, the server image is displayed instead of locally-generated Microcharts. If the server request fails or returns no data, the app automatically falls back to local Microcharts generation.

## Files Modified

### 1. `ObsidianScout/Models/GraphImageRequest.cs`
**Changes:**
- Added `GraphType` property (single graph type for direct requests)
- Added `Mode` property to support `match_by_match` vs `averages` per API docs

```csharp
[JsonPropertyName("graph_type")]
public string? GraphType { get; set; }

[JsonPropertyName("mode")]
public string? Mode { get; set; }
```

### 2. `ObsidianScout/ViewModels/GraphsViewModel.cs`
**Changes:**
- Enhanced `GenerateGraphsAsync()` to try server image endpoint first when online
- Properly sets request parameters per API docs:
  - `GraphType` = selected graph type ("line", "bar", "radar")
  - `Mode` = "match_by_match" or "averages" based on `SelectedDataView`
  - `TeamNumbers`, `EventId`, `Metric` for filtering
- Displays server image when bytes are returned
- Falls back to local generation on failure
- Clears server image state when regenerating

**Key Code Section:**
```csharp
// If online and offline-mode NOT enabled, try server image endpoint first
var offlineMode = await _settingsService.GetOfflineModeAsync();
if (!offlineMode && _connectivityService.IsConnected)
{
    try
    {
        StatusMessage = "Requesting server-generated graph image...";

        var request = new GraphImageRequest
        {
     TeamNumbers = SelectedTeams.Select(t => t.TeamNumber).ToList(),
   EventId = SelectedEvent.Id,
         Metric = SelectedMetric.Id,
        GraphType = SelectedGraphType,
            Mode = SelectedDataView == "match_by_match" ? "match_by_match" : "averages",
      DataView = SelectedDataView
        };

        var bytes = await _apiService.GetGraphsImageAsync(request);
        if (bytes != null && bytes.Length >0)
        {
          // Server returned an image - display it
            ServerGraphImage = ImageSource.FromStream(() => new MemoryStream(bytes));
            UseServerImage = true;
         ShowMicrocharts = false;
            HasGraphData = true;
       StatusMessage = "Server graph image loaded";

          // Clear local chart data
            CurrentChart = null;
      TeamCharts.Clear();
   TeamChartsWithInfo.Clear();

       return; // done - skip local generation
        }
  }
    catch (Exception ex)
    {
   System.Diagnostics.Debug.WriteLine($"Server image request failed: {ex.Message}");
        // fall back to local generation
    }
}

// Existing offline/local processing continues here...
```

### 3. `ObsidianScout/Views/GraphsPage.xaml` (Manual changes needed)
**Changes Required:**
Add the server image display section immediately after line 210 (the "Comparison Results" label):

```xaml
<!-- Server Graph Image (when online and available) -->
<Border Style="{StaticResource ElevatedGlassCard}"
      IsVisible="{Binding UseServerImage}"
 BackgroundColor="{AppThemeBinding Light={StaticResource GlassOverlayLight}, Dark={StaticResource GlassOverlayDark}}"
        Padding="10"
        Margin="0,0,0,10">
    <VerticalStackLayout Spacing="10">
        <Label Text="?? Server-Generated Graph"
      FontSize="14"
    FontAttributes="Italic"
       HorizontalTextAlignment="Center"
        TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}" />
   
        <Image Source="{Binding ServerGraphImage}"
   Aspect="AspectFit"
               HeightRequest="400"
       HorizontalOptions="FillAndExpand" />
    </VerticalStackLayout>
</Border>
```

Then add `IsVisible="{Binding ShowMicrocharts}"` to hide Microcharts UI when server image is shown:
- Line 213: Data View Selector Border
- Line 247: Graph Type Selector Grid
- Line 260: Team Comparison Summary CollectionView

## Behavior

### When Server Images Are Used:
1. User is NOT in offline mode (checked via `ISettingsService.GetOfflineModeAsync()`)
2. Device has network connectivity (`IConnectivityService.IsConnected`)
3. Server successfully returns PNG bytes from `/api/mobile/graphs`

### Fallback to Local Generation:
1. Offline mode is enabled
2. No network connectivity
3. Server request fails (network error, timeout, etc.)
4. Server returns no data or empty bytes
5. Server endpoint not implemented (404, etc.)

### User Experience:
- **Online + Server Available:** Shows "?? Server-Generated Graph" with PNG image, hides Microcharts UI
- **Offline / Server Unavailable:** Shows local Microcharts with full UI controls (view mode, graph type selectors)
- Status messages indicate which mode is active
- Seamless fallback - user always sees graphs even if server fails

## API Endpoint Used
`POST /api/mobile/graphs`

### Request Body Example:
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

### Response:
- **Success:** HTTP 200 with `Content-Type: image/png` and PNG bytes
- **Fallback JSON:** HTTP 200 with JSON containing `fallback_plotly_json` field (for client-side rendering - not currently implemented)
- **Error:** HTTP 4xx/5xx with error message

## API Service Implementation
The existing `ApiService.GetGraphsImageAsync()` method already handles:
- JWT authentication header
- Offline mode checking
- POST request with JSON body
- Byte array response reading
- Error handling and null returns on failure

## Testing Checklist
- [ ] Verify server images display when online and endpoint available
- [ ] Verify fallback to Microcharts when offline mode enabled
- [ ] Verify fallback when no network connectivity
- [ ] Verify fallback when server endpoint returns error/404
- [ ] Verify UI switches properly between server and local modes
- [ ] Verify graph type changes (line/radar) work with server images
- [ ] Verify data view changes (match-by-match/averages) work with server images
- [ ] Verify team selection changes trigger server image refresh
- [ ] Verify metric selection changes trigger server image refresh
- [ ] Verify event selection changes trigger server image refresh

## Benefits
1. **Higher Quality Visualizations:** Server can generate more sophisticated graphs using Plotly/matplotlib
2. **Reduced Client Processing:** Offloads computation to server
3. **Consistent Styling:** Server-rendered graphs match web UI exactly
4. **Seamless Fallback:** No disruption when server unavailable
5. **Bandwidth Efficient:** PNG images typically smaller than transmitting full datasets

## Dependencies
- Existing: `IApiService`, `IConnectivityService`, `ISettingsService`
- No new NuGet packages required
- Server must implement `/api/mobile/graphs` endpoint per API docs

## Notes
- The XAML changes must be applied manually by editing `GraphsPage.xaml`
- Build succeeded with current changes
- Hot reload may apply ViewModel changes without restart if debugging
- Server image mode is opt-in (only when online and not in offline mode)
- Local Microcharts remain fully functional as fallback
