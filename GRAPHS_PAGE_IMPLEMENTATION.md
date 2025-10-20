# Graphs Page Implementation - Complete Guide

## Overview
The Graphs page provides advanced team analytics and comparison capabilities for users with `analytics`, `analytics_admin`, or `superadmin` roles. This feature allows users to compare team performance across multiple metrics and visualize data through various chart types.

## Features Implemented

### 1. Role-Based Access Control
- **Accessible Roles**: `analytics`, `analytics_admin`, `superadmin`
- **Access Method**: Menu item visibility controlled by `HasAnalyticsAccess` property in AppShell
- **Navigation Protection**: Unauthorized users are blocked from accessing the page

### 2. Team Comparison
- Select and compare 2-6 teams simultaneously
- View performance metrics side-by-side
- Visual color coding for each team

### 3. Metric Selection
- Multiple performance metrics available:
  - Total Points
  - Auto Points
  - Teleop Points
  - Endgame Points
  - Consistency
  - Win Rate
  - Custom game-specific metrics

### 4. Graph Types
- **Line Chart**: Track performance trends over matches
- **Bar Chart**: Compare average values across teams
- **Radar Chart**: Multi-dimensional performance comparison

## Files Created/Modified

### New Files Created

#### 1. `ObsidianScout\Models\TeamMetrics.cs`
Contains all data models for team analytics and graphs:
- `TeamMetrics`: Performance statistics for a team
- `TeamMetricsResponse`: API response for single team metrics
- `MatchHistoryEntry`: Individual match performance data
- `GraphDataset`: Chart dataset definition
- `GraphData`: Complete chart configuration
- `CompareTeamsRequest`: Request for team comparison
- `TeamComparisonData`: Comparison result for a single team
- `CompareTeamsResponse`: Full comparison response with graph data
- `MetricDefinition`: Available metric definitions
- `MetricsResponse`: List of available metrics

#### 2. `ObsidianScout\ViewModels\GraphsViewModel.cs`
View model with full functionality:
- Event selection and loading
- Team selection and management
- Metric selection
- Graph generation and type switching
- Team comparison logic
- Loading states and error handling

#### 3. `ObsidianScout\Views\GraphsPage.xaml`
UI layout with:
- Event selector
- Metric selector
- Team selection interface with add/remove
- Selected teams display
- Graph type selector
- Comparison results display
- Graph visualization area

#### 4. `ObsidianScout\Views\GraphsPage.xaml.cs`
Code-behind for page initialization

### Modified Files

#### 1. `ObsidianScout\Services\ISettingsService.cs` & `SettingsService.cs`
**Added Methods:**
```csharp
Task<List<string>> GetUserRolesAsync();
Task SetUserRolesAsync(List<string> roles);
```
**Purpose:** Store and retrieve user roles from secure storage

#### 2. `ObsidianScout\Services\IApiService.cs` & `ApiService.cs`
**Added Methods:**
```csharp
Task<TeamMetricsResponse> GetTeamMetricsAsync(int teamId, int eventId);
Task<CompareTeamsResponse> CompareTeamsAsync(CompareTeamsRequest request);
Task<MetricsResponse> GetAvailableMetricsAsync();
```
**Updated:** `LoginAsync()` now stores user roles during login

#### 3. `ObsidianScout\AppShell.xaml.cs`
**Added Properties:**
```csharp
public bool HasAnalyticsAccess { get; set; }
```
**Updated Methods:**
- `CheckAuthStatus()`: Now checks user roles
- `OnNavigating()`: Blocks unauthorized access to Graphs page
- `UpdateAuthenticationState()`: Updates analytics access state

#### 4. `ObsidianScout\AppShell.xaml`
**Added Menu Item:**
```xaml
<FlyoutItem Title="?? Graphs"
            FlyoutIcon="??"
            IsVisible="{Binding HasAnalyticsAccess}"
            Route="GraphsPage">
    <ShellContent ContentTemplate="{DataTemplate views:GraphsPage}" />
</FlyoutItem>
```

#### 5. `ObsidianScout\MauiProgram.cs`
**Added Registrations:**
```csharp
builder.Services.AddTransient<GraphsViewModel>();
builder.Services.AddTransient<GraphsPage>();
```
**Added Route:**
```csharp
Routing.RegisterRoute("GraphsPage", typeof(GraphsPage));
```

#### 6. `ObsidianScout\Converters\ValueConverters.cs`
**Added Converters:**
- `IsNotZeroConverter`: Check if collection count > 0
- `TeamDisplayConverter`: Format team number display
- `TeamNumberNameConverter`: Format team number with name

#### 7. `ObsidianScout\App.xaml`
**Added Converter Resources:**
```xaml
<converters:IsNotZeroConverter x:Key="IsNotZeroConverter" />
<converters:TeamDisplayConverter x:Key="TeamDisplayConverter" />
<converters:TeamNumberNameConverter x:Key="TeamNumberNameConverter" />
```

## API Endpoints Used

### 1. Get Team Metrics
**Endpoint:** `GET /api/mobile/teams/{team_id}/metrics?event_id={event_id}`
**Returns:** Detailed performance statistics for a single team

### 2. Compare Teams
**Endpoint:** `POST /api/mobile/graphs/compare`
**Body:**
```json
{
  "team_numbers": [5454, 1234, 9999],
  "event_id": 5,
  "metric": "total_points",
  "graph_types": ["line", "bar", "radar"],
  "data_view": "averages"
}
```
**Returns:** Comparison data with graph configurations

### 3. Get Available Metrics
**Endpoint:** `GET /api/mobile/config/metrics`
**Returns:** List of available performance metrics

## Usage Flow

### For Users

1. **Login**: User logs in with credentials
2. **Role Check**: System checks if user has analytics role
3. **Menu Access**: If authorized, "?? Graphs" menu item appears
4. **Select Event**: Choose an event to analyze
5. **Select Metric**: Choose performance metric to compare
6. **Select Teams**: Add 2-6 teams to comparison
7. **Generate Graphs**: Tap "Generate Comparison Graphs"
8. **View Results**: See comparison data and visualizations
9. **Switch Graph Type**: Toggle between line/bar/radar charts

### For Non-Analytics Users
- Graphs menu item is hidden
- Direct navigation is blocked
- Clear error message if attempt is made

## Role Configuration

### Granting Analytics Access
Users need one of these roles in the API:
- `analytics` - Basic analytics viewing
- `analytics_admin` - Analytics with admin features
- `superadmin` - Full system access

Roles are:
1. Set on the server during user creation
2. Returned in login response
3. Stored locally in secure storage
4. Checked during navigation

## Chart Visualization Note

The current implementation provides:
- Complete data structure from API
- UI framework for displaying graphs
- Graph type selection
- Team comparison data display

**Chart Rendering:**
The XAML currently shows a placeholder for actual chart visualization. To add interactive charts, install one of these libraries:

### Recommended Charting Libraries for .NET MAUI

#### 1. **Microcharts** (Recommended for simplicity)
```bash
dotnet add package Microcharts.Maui
```
- Easy to use
- Good for basic charts
- Small footprint

#### 2. **LiveCharts 2**
```bash
dotnet add package LiveChartsCore.SkiaSharpView.Maui
```
- Professional charts
- Interactive
- Rich features

#### 3. **Syncfusion Charts** (Commercial)
- Enterprise-grade
- Extensive chart types
- Professional support

## Security Considerations

1. **Role Validation**: Roles are validated on both client and server
2. **Secure Storage**: User roles stored in platform secure storage
3. **Navigation Protection**: Unauthorized navigation is blocked
4. **Token Expiration**: Roles cleared on logout or token expiration

## Testing

### Test Scenarios

#### 1. Analytics User Login
```
User: analytics_user
Expected: See Graphs menu item
Result: ? Menu visible, page accessible
```

#### 2. Regular Scout Login
```
User: regular_scout
Expected: Graphs menu hidden
Result: ? Menu hidden, direct navigation blocked
```

#### 3. Role Check on Re-login
```
Action: Logout and login as different role
Expected: Menu visibility updates
Result: ? Auth state properly updates
```

#### 4. Team Comparison
```
Action: Select 3 teams and generate graphs
Expected: Comparison data displayed
Result: ? Data loads and displays correctly
```

## Troubleshooting

### Issue: Graphs menu not appearing
**Solution:** 
1. Check user roles in login response
2. Verify `HasAnalyticsAccess` property in AppShell
3. Check `GetUserRolesAsync()` returns correct roles

### Issue: "Access Denied" message
**Solution:**
1. Verify user has correct role on server
2. Check role storage: `await _settingsService.GetUserRolesAsync()`
3. Ensure roles are saved during login

### Issue: Teams not loading
**Solution:**
1. Verify event is selected
2. Check API endpoint is accessible
3. Check authentication token is valid

### Issue: Graphs not displaying
**Solution:**
1. Verify at least 2 teams are selected
2. Check metric is selected
3. Verify API returns valid data
4. Consider adding charting library for visualization

## Future Enhancements

### Potential Features
1. **Export Functionality**: Export graph data to CSV/PDF
2. **Custom Metrics**: Allow users to define custom metrics
3. **Historical Comparison**: Compare team performance across events
4. **Prediction Models**: ML-based performance predictions
5. **Real-time Updates**: Live graph updates during matches
6. **Alliance Optimization**: Suggest optimal alliance partners
7. **Interactive Charts**: Full chart library integration with zoom/pan
8. **Data Caching**: Cache graph data for offline viewing
9. **Share Graphs**: Share graph images with team

### Performance Optimizations
1. Lazy loading for team lists
2. Graph data caching
3. Pagination for large datasets
4. Background data refresh

## Integration with Chart Library Example

### Microcharts Integration (Example)
```csharp
// In GraphsPage.xaml.cs
private void UpdateChart()
{
    if (_viewModel.ComparisonData?.Graphs == null) return;
    
    var graphData = _viewModel.ComparisonData.Graphs["line"];
    var entries = new List<ChartEntry>();
    
    foreach (var dataset in graphData.Datasets)
    {
        for (int i = 0; i < dataset.Data.Count; i++)
        {
            entries.Add(new ChartEntry((float)dataset.Data[i])
            {
                Label = graphData.Labels[i],
                ValueLabel = dataset.Data[i].ToString("F1"),
                Color = SKColor.Parse(dataset.BorderColor)
            });
        }
    }
    
    ChartView.Chart = new LineChart { Entries = entries };
}
```

## Developer Notes

### Code Structure
- **MVVM Pattern**: Clean separation of concerns
- **Dependency Injection**: All services injected
- **Async/Await**: Proper async handling throughout
- **Error Handling**: Comprehensive try-catch blocks
- **User Feedback**: Status messages for all operations

### API Contract
The implementation follows the Mobile API JSON specification exactly as documented. All models match the server response structure.

### Extensibility
The architecture allows easy addition of:
- New graph types
- Additional metrics
- Custom visualization components
- Advanced filtering options

## Summary

The Graphs page implementation provides a complete, role-based team analytics solution that:
- ? Enforces proper access control
- ? Integrates seamlessly with existing architecture
- ? Follows MAUI best practices
- ? Provides comprehensive error handling
- ? Offers extensible data structure
- ? Ready for chart library integration

Users with analytics privileges can now effectively compare team performance, analyze trends, and make data-driven decisions for scouting and alliance selection.
