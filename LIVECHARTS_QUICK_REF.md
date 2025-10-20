# LiveChartsCore Quick Reference - Graphs are Fixed!

## ? DONE - You're Now Using LiveChartsCore (Free!)

### What Happened
- ? **Before**: Using Microcharts (limited, single-series charts)
- ? **Now**: Using **LiveChartsCore** (modern, free, multi-series charts!)

### What Works Now

#### 1. Multi-Team Line Charts ?
```
Select multiple teams ? Match-by-Match view ? Line Chart
Result: All teams on ONE chart with different colored lines!
```

#### 2. Team Comparison Bar Charts ?
```
Select teams ? Team Averages view ? Bar Chart
Result: Clean bar chart comparing all teams!
```

## Key Files Changed

| File | What Changed |
|------|--------------|
| `MauiProgram.cs` | Added LiveChartsCore using statements |
| `GraphsViewModel.cs` | Added LiveCharts properties and methods |
| `GraphsPage.xaml` | Added LiveChartsCore chart control |

## Important Properties

### In GraphsViewModel.cs

```csharp
// Chart data (LiveChartsCore)
LiveChartSeries  // The actual chart series to display
LiveChartXAxes   // X-axis configuration (match numbers or team numbers)
LiveChartYAxes   // Y-axis configuration (metric values)

// Control which library to use
UseLiveCharts = true;  // Use LiveChartsCore (default, recommended!)
UseLiveCharts = false; // Use Microcharts (old fallback)
```

## Common Tasks

### Want to Switch Back to Microcharts?
In `GraphsViewModel.cs` constructor:
```csharp
public GraphsViewModel(...)
{
    //...
    UseLiveCharts = false; // Use old library
}
```

### Want Different Chart Colors?
In `GraphsViewModel.cs`, update:
```csharp
private readonly SKColor[] TeamColorsSK = new[]
{
    SKColor.Parse("#FF6384"),  // Pink/Red
    SKColor.Parse("#36A2EB"),  // Blue
    SKColor.Parse("#FFCE56"),  // Yellow
    SKColor.Parse("#4BC0C0"),  // Teal
    SKColor.Parse("#9966FF"),  // Purple
    SKColor.Parse("#FF9F40")   // Orange
};
```

### Want Taller Charts?
In `GraphsPage.xaml`:
```xml
<lvc:CartesianChart HeightRequest="500" ... />
<!-- Change from 380 to whatever you want -->
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Chart is blank | Check debug output, ensure `LiveChartSeries` has data |
| Old single-series charts showing | Check `UseLiveCharts` is `true` |
| Build errors | Run `dotnet clean` then rebuild |
| Want old charts back | Set `UseLiveCharts = false` |

## Benefits of LiveChartsCore

? **Free** - MIT license, no subscription  
? **Multi-series** - Show all teams on one chart  
? **Interactive** - Click legend to show/hide teams  
? **Better looking** - Modern, professional charts  
? **Well documented** - https://livecharts.dev/  
? **Actively maintained** - Regular updates  

## Quick Test

1. Open app ? Go to Graphs page
2. Select an event
3. Select "Total Points" metric
4. Add 3-4 teams
5. Click "Match-by-Match" view
6. Click "Line Chart"
7. **See all teams on one chart!** ??

## Documentation

- **LiveChartsCore Docs**: https://livecharts.dev/
- **MAUI Examples**: https://livecharts.dev/docs/maui/2.0.0-rc3.3/overview
- **Full Migration Doc**: `LIVECHARTS_MIGRATION_COMPLETE.md`

## Status

? **Build: Successful**  
? **Integration: Complete**  
? **Charts: Working**  
? **Free: Forever!**  

**You're all set! Your graphs now use a modern, free library with better features.** ??
