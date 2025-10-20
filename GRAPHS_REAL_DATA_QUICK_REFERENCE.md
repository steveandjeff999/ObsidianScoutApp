# Graphs Real Data - Quick Reference ?

## What Changed

### Before ?
- Used `/api/mobile/graphs/compare` endpoint
- Server endpoint not implemented
- Only showed averages
- No real data

### After ?
- Uses `/api/mobile/scouting/all` endpoint
- Works with existing server
- Shows match-by-match details
- Real scouting data

---

## How It Works Now

```
1. Select event + metric + teams
2. Click "Generate Graphs"
3. App fetches scouting data for each team
4. Processes data locally (match-by-match OR averages)
5. Generates interactive charts
6. Displays with statistics
```

---

## Data Flow

```
User ? Select Teams
         ?
API ? GET /scouting/all?team_number=5454&event_id=5
         ?
Process ? Extract metrics, calculate stats
         ?
Generate ? Create chart with Microcharts
         ?
Display ? Show interactive visualization
```

---

## View Modes

### ?? Match-by-Match
- **Shows:** Individual match performance
- **Chart:** Line chart with all data points
- **Use For:** Tracking improvement, spotting trends

### ?? Team Averages
- **Shows:** Overall average performance
- **Chart:** Bar chart with team comparisons
- **Use For:** Quick rankings, alliance selection

---

## Key Methods

### API Method
```csharp
GetAllScoutingDataAsync(teamNumber, eventId, limit)
? Returns ScoutingListResponse with entries
```

### Processing Methods
```csharp
GenerateMatchByMatchData(entries)
? Creates line chart with all matches

GenerateTeamAveragesData(entries)
? Creates bar chart with averages
```

### Helper Methods
```csharp
ExtractMetricValue(data, metricId)
? Extracts specific metric from scouting data

ConvertToDouble(value)
? Safely converts any type to double

CalculateStdDev(values)
? Calculates standard deviation
```

---

## Example Output

### Team Averages View:
```
Team 5454: 125.5 ± 15.2 (12 matches)
Team 1234: 98.3 ± 18.5 (10 matches)

  ?????
  ?????  ????
  5454   1234
```

### Match-by-Match View:
```
Team 5454: ??????????????????????
           M1   M2  M3  M4  M5
           120  130 125 128 132
```

---

## Debug Checks

### Good Output:
```
Fetching data for team 5454...
  Found 12 entries
Total entries fetched: 22
Created 2 team datasets with 12 match points
Chart created: LineChart ?
```

### No Data:
```
No scouting data found for selected teams
```
? Teams have no data at this event

---

## Testing

1. **Login**
2. **Go to Graphs** (??)
3. **Select:** Event + Metric + Teams
4. **Choose:** Match-by-Match
5. **Generate** graphs
6. **Verify:** Line chart with multiple points ?
7. **Switch:** To Team Averages
8. **Verify:** Bar chart with averages ?

---

## Status

| Component | Status |
|-----------|--------|
| API method | ? Added |
| Models | ? Added |
| Data fetching | ? Working |
| Match-by-match | ? Working |
| Team averages | ? Working |
| Chart generation | ? Working |
| Build | ? Successful |

**Deploy and test with real scouting data! ???**

See `GRAPHS_REAL_DATA_INTEGRATION.md` for complete details.
