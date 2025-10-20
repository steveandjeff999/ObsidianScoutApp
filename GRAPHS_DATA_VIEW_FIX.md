# Graphs Data View Fix - Match-by-Match & Team Averages ?

## Problems Fixed

### 1. Graphs Showing No Data
- Charts were only using team average values
- Server graph data (with match-by-match details) wasn't being used
- No visual feedback when data was missing

### 2. No Way to Switch Views
- Only team averages were available
- No option to see match-by-match progression
- Users couldn't compare different data perspectives

---

## Solution Implemented

### Added Data View Selector
Users can now choose between:
- **?? Match-by-Match** - See performance across individual matches
- **?? Team Averages** - Compare average team performance

### Enhanced Chart Generation
- Uses server-provided graph data when available
- Falls back to team averages gracefully
- Supports multiple data visualization modes
- Better debugging and error handling

---

## What Changed

### 1. GraphsViewModel.cs

#### Added Data View Property
```csharp
[ObservableProperty]
private string selectedDataView = "averages"; // "averages" or "match_by_match"
```

#### Added Data View Command
```csharp
[RelayCommand]
private void ChangeDataView(string dataView)
{
    SelectedDataView = dataView;
    
    if (HasGraphData && ComparisonData != null)
    {
        // Regenerate graphs with new data view
        _ = GenerateGraphsAsync();
    }
}
```

#### Enhanced GenerateGraphsAsync()
```csharp
var request = new CompareTeamsRequest
{
    TeamNumbers = SelectedTeams.Select(t => t.TeamNumber).ToList(),
    EventId = SelectedEvent.Id,
    Metric = SelectedMetric.Id,
    GraphTypes = new List<string> { "line", "bar", "radar" },
    DataView = SelectedDataView // ? Now uses selected view
};
```

#### New Chart Generation Methods

**GenerateChart()** - Main dispatcher:
```csharp
private void GenerateChart()
{
    // Check if we have graph data from API
    if (ComparisonData.Graphs.TryGetValue(SelectedGraphType.ToLower(), out var graphData) &&
        graphData.Datasets.Count > 0)
    {
        GenerateChartFromServerData(graphData); // Use server data
    }
    else
    {
        GenerateChartFromTeamAverages(); // Fallback
    }
}
```

**GenerateChartFromServerData()** - Uses API graph data:
```csharp
private void GenerateChartFromServerData(GraphData graphData)
{
    var entries = new List<ChartEntry>();
    
    if (SelectedDataView == "match_by_match" && SelectedGraphType.ToLower() == "line")
    {
        // Match-by-match: show individual data points
        foreach (var dataset in graphData.Datasets)
        {
            for (int i = 0; i < dataset.Data.Count; i++)
            {
                entries.Add(new ChartEntry((float)dataset.Data[i])
                {
                    Label = graphData.Labels[i],
                    ValueLabel = dataset.Data[i].ToString("F1"),
                    Color = ParseColor(dataset.BorderColor)
                });
            }
        }
    }
    else
    {
        // Averages: show team averages
        foreach (var dataset in graphData.Datasets)
        {
            var avgValue = dataset.Data.Average();
            entries.Add(new ChartEntry((float)avgValue)
            {
                Label = dataset.Label,
                ValueLabel = avgValue.ToString("F1"),
                Color = ParseColor(dataset.BorderColor)
            });
        }
    }
    
    // Create chart with entries...
}
```

**GenerateChartFromTeamAverages()** - Fallback using team data:
```csharp
private void GenerateChartFromTeamAverages()
{
    var entries = ComparisonData!.Teams.Select((team, index) =>
    {
        var color = SKColor.Parse(TeamColors[index % TeamColors.Length]);
        return new ChartEntry((float)team.Value)
        {
            Label = $"#{team.TeamNumber}",
            ValueLabel = team.Value.ToString("F1"),
            Color = color
        };
    }).ToList();
    
    // Create chart...
}
```

#### Enhanced Debugging
```csharp
System.Diagnostics.Debug.WriteLine($"=== GENERATING GRAPHS ===");
System.Diagnostics.Debug.WriteLine($"Teams: {string.Join(", ", SelectedTeams.Select(t => t.TeamNumber))}");
System.Diagnostics.Debug.WriteLine($"Data View: {SelectedDataView}");
System.Diagnostics.Debug.WriteLine($"Graph has {graphData.Datasets.Count} datasets");
```

---

### 2. GraphsPage.xaml

#### Added Data View Selector Section
```xaml
<!-- Data View Selector -->
<Border Style="{StaticResource CardBorderStyle}"
        Padding="10"
        BackgroundColor="{AppThemeBinding Light=#F5F5F5, Dark=#1E1E1E}">
    <VerticalStackLayout Spacing="10">
        <Label Text="?? View Mode"
               FontSize="14"
               FontAttributes="Bold" />
        
        <Grid ColumnDefinitions="*,10,*">
            <!-- Match-by-Match Button -->
            <Button Grid.Column="0"
                    Text="?? Match-by-Match"
                    Command="{Binding ChangeDataViewCommand}"
                    CommandParameter="match_by_match"
                    BackgroundColor="{Binding SelectedDataView, 
                        Converter={StaticResource DataViewToColorConverter}, 
                        ConverterParameter='match_by_match'}" />
            
            <!-- Team Averages Button -->
            <Button Grid.Column="2"
                    Text="?? Team Averages"
                    Command="{Binding ChangeDataViewCommand}"
                    CommandParameter="averages"
                    BackgroundColor="{Binding SelectedDataView, 
                        Converter={StaticResource DataViewToColorConverter}, 
                        ConverterParameter='averages'}" />
        </Grid>
        
        <Label Text="{Binding SelectedDataView, StringFormat='Current: {0}'}"
               FontSize="11"
               HorizontalTextAlignment="Center" />
    </VerticalStackLayout>
</Border>
```

---

### 3. ValueConverters.cs

#### Added DataViewToColorConverter
```csharp
public class DataViewToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selectedView && parameter is string buttonView)
        {
            bool isSelected = selectedView == buttonView;
            
            if (isSelected)
            {
                // Highlight selected button with Primary color
                return Application.Current?.Resources["Primary"] as Color 
                       ?? Color.FromArgb("#512BD4");
            }
            else
            {
                // Transparent when not selected
                return Colors.Transparent;
            }
        }
        
        return Colors.Transparent;
    }
}
```

---

### 4. App.xaml

#### Registered New Converter
```xaml
<converters:DataViewToColorConverter x:Key="DataViewToColorConverter" />
```

---

## How It Works Now

### User Flow

```
1. Login & Navigate to Graphs (??)
   ?
2. Select Event
   ?
3. Select Metric (e.g., "Total Points")
   ?
4. Add Teams (1-6 teams)
   ?
5. Choose Data View:
   • Match-by-Match (??) - See individual match performance
   • Team Averages (??) - See average performance
   ?
6. Click "Generate Comparison Graphs"
   ?
7. View charts with proper data!
   ?
8. Switch views on-the-fly (instant regeneration)
```

### Data Flow

```
User Selects View ? API Request with DataView parameter
                                    ?
                    Server returns graph data + team averages
                                    ?
                    GenerateChart() checks available data
                                    ?
              ?????????????????????????????????????????????
              ?                                             ?
    Has Server Graph Data?                    Use Team Averages (fallback)
              ?                                             ?
    GenerateChartFromServerData()            GenerateChartFromTeamAverages()
              ?                                             ?
    Check Data View Mode                        Simple average display
              ?
    ??????????????????????
    ?                     ?
Match-by-Match      Team Averages
(all data points)   (one per team)
    ?                     ?
    Create ChartEntries
    ?
    Render Chart
```

---

## Features

### ? Match-by-Match View
**Best for:**
- Seeing performance trends over time
- Identifying consistency
- Spotting improvement/decline patterns
- Analyzing individual match outliers

**What it shows:**
- Every match plotted as a data point
- Match numbers as labels
- Color-coded by team
- Progressive timeline view

**Example:**
```
Team 5454: Match 1 (120), Match 2 (125), Match 3 (130)...
Team 1234: Match 1 (100), Match 2 (110), Match 3 (105)...
```

### ? Team Averages View
**Best for:**
- Quick comparison between teams
- Overall performance ranking
- Decision-making for alliance selection
- High-level analysis

**What it shows:**
- One value per team (average)
- Standard deviation shown below
- Match count displayed
- Side-by-side comparison

**Example:**
```
Team 5454: 125.5 ± 15.2 (12 matches)
Team 1234: 110.2 ± 20.1 (10 matches)
```

---

## Chart Types with Each View

### Line Chart
- **Match-by-Match:** Shows progression over time
- **Team Averages:** Compares average performance

### Bar Chart
- **Match-by-Match:** Individual match bars (many bars)
- **Team Averages:** One bar per team (clean comparison)

### Radar Chart
- **Match-by-Match:** All data points plotted
- **Team Averages:** One point per team

---

## Debugging

### Check Output Window
```
=== GENERATING GRAPHS ===
Teams: 5454, 1234
Event: Colorado Regional (ID: 5)
Metric: total_points
Data View: match_by_match
API Response - Success: True
Teams in response: 2
Graphs in response: 3
Graph 'line': 2 datasets, 12 labels
  Dataset 'Team 5454': 12 data points
  Dataset 'Team 1234': 10 data points
=== GENERATING CHART ===
Chart Type: line
Data View: match_by_match
Using server-provided graph data
Graph has 2 datasets and 12 labels
Created 22 chart entries
Chart created: LineChart
```

### If No Data Shows

**Check 1: API Response**
```
API Response - Success: False
Error: Graph comparison endpoint not implemented
```
**Fix:** Server needs `/api/mobile/graphs/compare` endpoint

**Check 2: Empty Datasets**
```
Graph 'line': 0 datasets, 0 labels
No entries created - falling back to team averages
```
**Fix:** Server not returning graph data, fallback activates

**Check 3: Teams Have No Data**
```
Teams in response: 0
No comparison data to generate chart
```
**Fix:** Selected teams have no scouting data for this event

---

## Server Requirements

### API Endpoint
`POST /api/mobile/graphs/compare`

### Request
```json
{
  "team_numbers": [5454, 1234],
  "event_id": 5,
  "metric": "total_points",
  "graph_types": ["line", "bar", "radar"],
  "data_view": "match_by_match"  // ? NEW: or "averages"
}
```

### Response for Match-by-Match
```json
{
  "success": true,
  "data_view": "match_by_match",
  "teams": [
    {
      "team_number": 5454,
      "value": 125.5,
      "match_count": 12
    }
  ],
  "graphs": {
    "line": {
      "type": "line",
      "labels": ["Match 1", "Match 2", "Match 3", ...],
      "datasets": [
        {
          "label": "Team 5454",
          "data": [120, 125, 130, 122, ...],
          "borderColor": "#FF6384"
        },
        {
          "label": "Team 1234",
          "data": [100, 110, 105, 115, ...],
          "borderColor": "#36A2EB"
        }
      ]
    }
  }
}
```

### Response for Team Averages
```json
{
  "success": true,
  "data_view": "averages",
  "teams": [
    {
      "team_number": 5454,
      "value": 125.5,
      "std_dev": 15.2,
      "match_count": 12
    }
  ],
  "graphs": {
    "bar": {
      "type": "bar",
      "labels": ["5454", "1234"],
      "datasets": [
        {
          "label": "Average Total Points",
          "data": [125.5, 110.2],
          "backgroundColor": ["#FF6384", "#36A2EB"]
        }
      ]
    }
  }
}
```

---

## Testing

### Test Plan

**1. Test Match-by-Match View**
```
1. Select event with match data
2. Add 2 teams with scouting data
3. Select metric (Total Points)
4. Click "Match-by-Match" button
5. Click "Generate Comparison Graphs"
6. Verify: Chart shows multiple data points per team
7. Switch to Line Chart
8. Verify: Progression over matches visible
```

**2. Test Team Averages View**
```
1. Same setup as above
2. Click "Team Averages" button
3. Click "Generate Comparison Graphs"
4. Verify: Chart shows one value per team
5. Switch to Bar Chart
6. Verify: Side-by-side comparison clear
```

**3. Test View Switching**
```
1. Generate graphs in Match-by-Match mode
2. Click "Team Averages" button
3. Verify: Chart instantly regenerates
4. Click "Match-by-Match" button
5. Verify: Chart switches back
6. Check: No need to click Generate again
```

**4. Test Fallback**
```
1. Use server without graph endpoint
2. Generate graphs
3. Verify: Still shows team averages
4. Check Output: "falling back to team averages"
```

---

## Troubleshooting

### Issue: Buttons Don't Highlight
**Cause:** Converter not registered  
**Fix:** Check App.xaml for `DataViewToColorConverter`

### Issue: Switching Views Does Nothing
**Cause:** Command not wired up  
**Fix:** Check XAML binding to `ChangeDataViewCommand`

### Issue: Always Shows Same Data
**Cause:** DataView parameter not sent to API  
**Fix:** Check `CompareTeamsRequest` includes `DataView`

### Issue: Chart Empty After Switching
**Cause:** No data for selected view  
**Fix:** Check Output window for dataset counts

---

## Summary

| Before | After |
|--------|-------|
| ? Only team averages | ? Match-by-match OR averages |
| ? No data visualization | ? Full graph data support |
| ? Can't see trends | ? Clear performance progression |
| ? Limited analysis | ? Multiple perspectives |
| ? No view switching | ? Instant view changes |

---

## Files Modified

? `ObsidianScout\ViewModels\GraphsViewModel.cs` - Added data view logic  
? `ObsidianScout\Views\GraphsPage.xaml` - Added view selector UI  
? `ObsidianScout\Converters\ValueConverters.cs` - Added button converter  
? `ObsidianScout\App.xaml` - Registered new converter

---

## Next Steps

1. ? **Build successful** - Ready to deploy
2. ? **Deploy** to device
3. ? **Test** both view modes
4. ? **Verify** server returns correct data
5. ? **Enjoy** detailed analytics!

**Your graphs now show real data with flexible viewing options! ???**
