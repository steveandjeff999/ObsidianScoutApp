# Match Prediction Feature - Complete Implementation Guide

## Feature Overview

The Match Prediction system provides advanced statistical analysis to predict match outcomes based on historical team performance data. It leverages scouting data collected throughout an event to generate realistic predictions for upcoming matches, including:

- **Period-specific predictions** (Auto, Teleop, Endgame)
- **Alliance total scores**
- **Win probability calculations**
- **Team-by-team breakdowns**
- **Confidence indicators based on data quality**

## Architecture

### File Structure

```
ObsidianScout/
??? Models/
?   ??? MatchPrediction.cs          # Prediction data models
??? ViewModels/
?   ??? MatchPredictionViewModel.cs # Prediction logic and state
??? Views/
?   ??? MatchPredictionPage.xaml    # UI layout
?   ??? MatchPredictionPage.xaml.cs # Code-behind
??? Services/
?   ??? ApiService.cs               # Data fetching (existing)
??? MauiProgram.cs                   # DI registration
```

## Data Models

### Core Models

#### MatchPrediction
The main prediction result containing all prediction data.

```csharp
public class MatchPrediction
{
    public Match Match { get; set; }
    
    // Team predictions by alliance
    public List<TeamMatchPrediction> RedAlliance { get; set; }
    public List<TeamMatchPrediction> BlueAlliance { get; set; }
    
    // Alliance totals
    public double RedPredictedAutoPoints { get; set; }
    public double RedPredictedTeleopPoints { get; set; }
    public double RedPredictedEndgamePoints { get; set; }
    public double RedPredictedTotalPoints { get; set; }
    
    public double BluePredictedAutoPoints { get; set; }
    public double BluePredictedTeleopPoints { get; set; }
    public double BluePredictedEndgamePoints { get; set; }
    public double BluePredictedTotalPoints { get; set; }
    
    // Win probabilities (0-1 scale)
    public double RedWinProbability { get; set; }
    public double BlueWinProbability { get; set; }
    
    // Data quality
    public bool HasSufficientData { get; set; }
    public string WarningMessage { get; set; }
}
```

#### TeamMatchPrediction
Prediction data for a single team in a match.

```csharp
public class TeamMatchPrediction
{
    public int TeamNumber { get; set; }
    public string TeamName { get; set; }
    
    // Predicted points
    public double PredictedAutoPoints { get; set; }
    public double PredictedTeleopPoints { get; set; }
    public double PredictedEndgamePoints { get; set; }
    public double PredictedTotalPoints { get; set; }
    
    // Standard deviations (confidence intervals)
    public double AutoPointsStd { get; set; }
    public double TeleopPointsStd { get; set; }
    public double EndgamePointsStd { get; set; }
    public double TotalPointsStd { get; set; }
    
    // Metadata
    public int MatchCount { get; set; }
    public double Consistency { get; set; }
    public string Alliance { get; set; } // "red" or "blue"
}
```

#### TeamHistoricalStats
Internal model for storing calculated team statistics.

```csharp
public class TeamHistoricalStats
{
    public int TeamNumber { get; set; }
    public string TeamName { get; set; }
    
    // Averages
    public double AvgAutoPoints { get; set; }
    public double AvgTeleopPoints { get; set; }
    public double AvgEndgamePoints { get; set; }
    public double AvgTotalPoints { get; set; }
    
    // Standard deviations
    public double StdAutoPoints { get; set; }
    public double StdTeleopPoints { get; set; }
    public double StdEndgamePoints { get; set; }
    public double StdTotalPoints { get; set; }
    
    public int MatchCount { get; set; }
    public double Consistency { get; set; }
}
```

## ViewModel Implementation

### MatchPredictionViewModel

**Purpose**: Manages the match prediction UI state and executes prediction logic.

**Key Properties**:
```csharp
[ObservableProperty] private bool isLoading;
[ObservableProperty] private string statusMessage;
[ObservableProperty] private ObservableCollection<Event> events;
[ObservableProperty] private Event? selectedEvent;
[ObservableProperty] private ObservableCollection<Match> matches;
[ObservableProperty] private Match? selectedMatch;
[ObservableProperty] private MatchPrediction? prediction;
[ObservableProperty] private bool hasPrediction;
[ObservableProperty] private bool showInsufficientDataWarning;
[ObservableProperty] private string insufficientDataMessage;
```

**Key Methods**:

#### InitializeAsync()
Called when page appears. Loads game config and events.

```csharp
public async Task InitializeAsync()
{
    await LoadGameConfigAsync();
    await LoadEventsAsync();
}
```

#### PredictMatchAsync()
Main prediction logic. Executed when user clicks "Predict Match Outcome" button.

**Algorithm**:
1. **Parse Team Numbers**: Extract team numbers from match alliance strings
2. **Fetch Historical Data**: Get scouting entries for each team at the event
3. **Calculate Statistics**: Compute averages and standard deviations per period
4. **Build Team Predictions**: Create prediction objects for each team
5. **Aggregate Alliance Totals**: Sum team predictions by alliance
6. **Calculate Win Probability**: Use statistical model to compute win chances
7. **Display Results**: Update UI with prediction data

```csharp
[RelayCommand]
private async Task PredictMatchAsync()
{
    // 1. Validate selection
    if (SelectedMatch == null || SelectedEvent == null)
        return;
    
    // 2. Parse team numbers
    var redTeams = ParseTeamNumbers(SelectedMatch.RedAlliance);
    var blueTeams = ParseTeamNumbers(SelectedMatch.BlueAlliance);
    
    // 3. Fetch historical stats
    var teamStats = new Dictionary<int, TeamHistoricalStats>();
    foreach (var teamNumber in allTeams)
    {
        var stats = await GetTeamHistoricalStatsAsync(teamNumber, SelectedEvent.Id);
        if (stats != null)
            teamStats[teamNumber] = stats;
    }
    
    // 4. Build predictions
    var matchPrediction = new MatchPrediction { Match = SelectedMatch };
    
    // 5. Predict each alliance
    foreach (var teamNumber in redTeams)
    {
        var teamPred = CreateTeamPrediction(teamNumber, teamStats, "red");
        matchPrediction.RedAlliance.Add(teamPred);
        // Aggregate totals...
    }
    
    // 6. Calculate win probability
    var scoreDiff = matchPrediction.RedPredictedTotalPoints - matchPrediction.BluePredictedTotalPoints;
    var combinedStdDev = CalculateCombinedStdDev(matchPrediction);
    matchPrediction.RedWinProbability = NormalCDF(scoreDiff / combinedStdDev);
    
    // 7. Display
    Prediction = matchPrediction;
    HasPrediction = true;
}
```

#### GetTeamHistoricalStatsAsync()
Fetches and processes historical scouting data for a team.

**Process**:
1. Call API to get scouting entries for team at event
2. Filter entries to ensure only data for the specific team
3. Calculate points for each match using game config
4. Compute statistics (mean, std dev, consistency)
5. Return `TeamHistoricalStats` object

```csharp
private async Task<TeamHistoricalStats?> GetTeamHistoricalStatsAsync(int teamNumber, int eventId)
{
    var response = await _apiService.GetAllScoutingDataAsync(
        teamNumber: teamNumber, 
        eventId: eventId,
        limit: 100
    );
    
    var entries = response.Entries.Where(e => e.TeamNumber == teamNumber).ToList();
    
    var autoPoints = new List<double>();
    var teleopPoints = new List<double>();
    var endgamePoints = new List<double>();
    var totalPoints = new List<double>();
    
    foreach (var entry in entries)
    {
        var auto = CalculateAutoPoints(entry.Data);
        var teleop = CalculateTeleopPoints(entry.Data);
        var endgame = CalculateEndgamePoints(entry.Data);
        
        autoPoints.Add(auto);
        teleopPoints.Add(teleop);
        endgamePoints.Add(endgame);
        totalPoints.Add(auto + teleop + endgame);
    }
    
    return new TeamHistoricalStats
    {
        AvgAutoPoints = autoPoints.Average(),
        StdAutoPoints = CalculateStdDev(autoPoints),
        // ... etc
    };
}
```

### Point Calculation Methods

#### CalculateAutoPoints()
Calculates autonomous period points from scouting data.

```csharp
private double CalculateAutoPoints(Dictionary<string, object> data)
{
    if (_gameConfig?.AutoPeriod == null) return 0;
    
    double points = 0;
    foreach (var element in _gameConfig.AutoPeriod.ScoringElements)
    {
        if (data.TryGetValue(element.Id, out var value))
        {
            var count = ConvertToDouble(value);
            points += count * element.Points;
        }
    }
    return points;
}
```

**Similar methods**:
- `CalculateTeleopPoints()`: For teleop period
- `CalculateEndgamePoints()`: For endgame period (handles counters, booleans, multiple choice)

### Statistical Functions

#### CalculateStdDev()
Calculates standard deviation using the sample formula.

```csharp
private double CalculateStdDev(List<double> values)
{
    if (values.Count < 2) return 0;
    
    var avg = values.Average();
    var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
    return Math.Sqrt(sumOfSquares / (values.Count - 1));
}
```

#### CalculateConsistency()
Computes consistency as inverse of coefficient of variation.

```csharp
private double CalculateConsistency(List<double> values)
{
    var avg = values.Average();
    if (avg == 0) return 0;
    
    var stdDev = CalculateStdDev(values);
    var cv = stdDev / avg; // Coefficient of variation
    
    // Lower CV = higher consistency
    return Math.Max(0, 1.0 - cv);
}
```

#### NormalCDF()
Cumulative distribution function for normal distribution.

```csharp
private double NormalCDF(double x)
{
    return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
}

private double Erf(double x)
{
    // Abramowitz and Stegun approximation
    const double a1 = 0.254829592;
    const double a2 = -0.284496736;
    const double a3 = 1.421413741;
    const double a4 = -1.453152027;
    const double a5 = 1.061405429;
    const double p = 0.3275911;
    
    int sign = x < 0 ? -1 : 1;
    x = Math.Abs(x);
    
    double t = 1.0 / (1.0 + p * x);
    double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
    
    return sign * y;
}
```

## Win Probability Model

### Statistical Approach

The win probability is calculated using a **normal distribution model** based on the score difference and combined variance of both alliances.

**Assumptions**:
1. Team performances follow normal distributions
2. Team performances are independent
3. Alliance score = sum of team scores
4. Alliance variance = sum of team variances

**Formula**:
```
scoreDiff = redTotal - blueTotal
combinedStdDev = sqrt(redStdDev² + blueStdDev²)
zScore = scoreDiff / combinedStdDev
P(redWin) = NormalCDF(zScore)
P(blueWin) = 1 - P(redWin)
```

**Interpretation**:
- Z-score > 0: Red favored
- Z-score < 0: Blue favored
- Z-score = 0: Even match
- |Z-score| > 2: Strong favorite (>97.7% win probability)
- |Z-score| < 0.5: Close match (~69% vs 31%)

### Fallback Logic

If `combinedStdDev = 0` (no variance in data):
```csharp
if (scoreDiff > 0)
{
    RedWinProbability = 0.75;
    BlueWinProbability = 0.25;
}
else if (scoreDiff < 0)
{
    RedWinProbability = 0.25;
    BlueWinProbability = 0.75;
}
else
{
    RedWinProbability = 0.5;
    BlueWinProbability = 0.5;
}
```

## UI Implementation

### Page Layout Structure

```xml
<ScrollView>
  <VerticalStackLayout>
    <!-- Header Card -->
    <Border Style="GlassCard">
      <Label Text="Match Prediction" />
    </Border>
    
    <!-- Event Selection -->
    <Border Style="GlassCard">
      <Picker ItemsSource="{Binding Events}" />
    </Border>
    
    <!-- Match Selection -->
    <Border Style="GlassCard">
      <Picker ItemsSource="{Binding Matches}" />
      <VerticalStackLayout> <!-- Match preview -->
        <Label Text="Red Alliance" />
        <Label Text="Blue Alliance" />
      </VerticalStackLayout>
    </Border>
    
    <!-- Predict Button -->
    <Button Command="{Binding PredictMatchCommand}" />
    
    <!-- Loading Indicator -->
    <ActivityIndicator IsRunning="{Binding IsLoading}" />
    
    <!-- Results (Visible when HasPrediction=true) -->
    <VerticalStackLayout IsVisible="{Binding HasPrediction}">
      <!-- Win Probability Card -->
      <Border Style="GlassCard">
        <ProgressBar Progress="{Binding Prediction.RedWinProbability}" />
        <ProgressBar Progress="{Binding Prediction.BlueWinProbability}" />
      </Border>
      
      <!-- Predicted Scores Card -->
      <Border Style="GlassCard">
        <Grid> <!-- Score table --> </Grid>
      </Border>
      
      <!-- Red Alliance Breakdown -->
      <CollectionView ItemsSource="{Binding Prediction.RedAlliance}" />
      
      <!-- Blue Alliance Breakdown -->
      <CollectionView ItemsSource="{Binding Prediction.BlueAlliance}" />
    </VerticalStackLayout>
  </VerticalStackLayout>
</ScrollView>
```

### Key UI Features

#### Match Preview
Shows teams in selected match before prediction:
```xml
<HorizontalStackLayout>
  <Label Text="?? Red:" TextColor="#FF6B6B" />
  <Label Text="{Binding SelectedMatch.RedAlliance}" />
</HorizontalStackLayout>
```

#### Win Probability Display
Visual representation with progress bars:
```xml
<ProgressBar Progress="{Binding Prediction.RedWinProbability}"
             ProgressColor="#FF6B6B"
             HeightRequest="12" />
```

#### Team Breakdown Card
CollectionView with DataTemplate:
```xml
<CollectionView ItemsSource="{Binding Prediction.RedAlliance}">
  <CollectionView.ItemTemplate>
    <DataTemplate x:DataType="models:TeamMatchPrediction">
      <Border Style="GlassCard">
        <Grid>
          <!-- Team stats grid -->
        </Grid>
      </Border>
    </DataTemplate>
  </CollectionView.ItemTemplate>
</CollectionView>
```

## Dependency Injection

### Registration in MauiProgram.cs

```csharp
// ViewModels
builder.Services.AddTransient<MatchPredictionViewModel>();

// Pages
builder.Services.AddTransient<MatchPredictionPage>();
```

### Navigation

Added to AppShell.xaml:
```xml
<FlyoutItem Title="?? Match Prediction"
            FlyoutIcon="??"
            IsVisible="{Binding IsLoggedIn}"
            Route="MatchPredictionPage">
    <ShellContent ContentTemplate="{DataTemplate views:MatchPredictionPage}" />
</FlyoutItem>
```

## Data Flow

### Prediction Workflow

```
User Action ? ViewModel ? API Service ? Backend
     ?
Select Event
     ?
Load Matches ????????????? GET /api/mobile/matches?event_id={id}
     ?                                    ?
Select Match                         Return matches
     ?
Click Predict
     ?
Parse Team Numbers
     ?
For each team:
  GetTeamHistoricalStatsAsync ?? GET /api/mobile/scouting/all?team_number={num}&event_id={id}
          ?                                         ?
    Calculate Stats                          Return entries
          ?
    Store in Dictionary
     ?
Build MatchPrediction
     ?
Calculate Win Probability
     ?
Display Results
```

### API Endpoints Used

1. **GET /api/mobile/config/game**
   - Purpose: Load game configuration for point calculations
   - Called: On page initialization

2. **GET /api/mobile/events**
   - Purpose: Load available events
   - Called: On page initialization

3. **GET /api/mobile/matches?event_id={id}**
   - Purpose: Load matches for selected event
   - Called: When event is selected

4. **GET /api/mobile/scouting/all?team_number={num}&event_id={id}**
   - Purpose: Fetch historical scouting data for a team
   - Called: During prediction for each team (up to 6 calls per prediction)

## Error Handling

### Validation Checks

```csharp
// Match selection validation
if (SelectedMatch == null)
{
    StatusMessage = "Please select a match";
    return;
}

// Alliance data validation
if (redTeams.Count == 0 || blueTeams.Count == 0)
{
    StatusMessage = "Invalid match - missing team data";
    return;
}
```

### Data Quality Warnings

```csharp
// Track teams without data
if (stats == null || stats.MatchCount == 0)
{
    teamsWithoutData.Add(teamNumber);
}

// Show warning if any teams lack data
if (teamsWithoutData.Count > 0)
{
    ShowInsufficientDataWarning = true;
    InsufficientDataMessage = $"?? {teamsWithoutData.Count} team(s) have no historical data...";
}
```

### Exception Handling

All async operations wrapped in try-catch:
```csharp
try
{
    IsLoading = true;
    // ... prediction logic ...
}
catch (Exception ex)
{
    StatusMessage = $"Error predicting match: {ex.Message}";
    System.Diagnostics.Debug.WriteLine($"Exception: {ex}");
}
finally
{
    IsLoading = false;
}
```

## Testing

### Manual Test Cases

1. **Happy Path**
   - Select event with scouting data
   - Select match with all teams scouted
   - Click predict
   - Verify predictions appear with reasonable values

2. **Insufficient Data**
   - Select match with some teams not scouted
   - Verify warning appears
   - Verify teams with no data show 0 points
   - Verify prediction still completes

3. **No Data**
   - Select match with no scouted teams
   - Verify all teams show 0 points
   - Verify prediction shows 50/50 probability

4. **Edge Cases**
   - Match with bye (2 teams vs 3 teams)
   - First match of event (no prior data)
   - Teams with single match of data

### Debug Logging

Extensive debug output for troubleshooting:
```csharp
System.Diagnostics.Debug.WriteLine($"=== PREDICTING MATCH {SelectedMatch.MatchNumber} ===");
System.Diagnostics.Debug.WriteLine($"Red Alliance: {string.Join(", ", redTeams)}");
System.Diagnostics.Debug.WriteLine($"Team {teamNumber}: {stats.MatchCount} matches, Avg={stats.AvgTotalPoints:F1}");
```

## Performance Considerations

### API Calls
- **Event Selection**: 1 API call to load matches
- **Prediction**: Up to 6 API calls (one per team)
- **Total per prediction**: ~7 API calls

### Optimization Opportunities
1. **Batch API endpoint**: Fetch all team stats in single call
2. **Caching**: Cache team stats during event to avoid repeated fetches
3. **Prefetching**: Load stats for all teams when event is selected
4. **Local calculation**: Store raw scouting data and calculate locally

### Memory Usage
- Typical prediction: ~50KB of data (6 teams × ~100 entries each)
- UI bindings: Minimal overhead with MVVM Toolkit
- No image data or large objects

## Accessibility

### Screen Reader Support
All UI elements include semantic descriptions:
- Labels for all interactive controls
- Meaningful button text
- Status messages for state changes

### Visual Indicators
- Color coding with text labels (not color-only)
- Progress bars with percentage labels
- Large touch targets for buttons/pickers

## Future Server-Side Implementation

The models include API request/response types for potential server-side prediction:

```csharp
public class MatchPredictionRequest
{
    public int MatchId { get; set; }
    public int EventId { get; set; }
}

public class MatchPredictionResponse
{
    public bool Success { get; set; }
    public Match? Match { get; set; }
    public AlliancePrediction? RedAlliance { get; set; }
    public AlliancePrediction? BlueAlliance { get; set; }
    public double RedWinProbability { get; set; }
    public double BlueWinProbability { get; set; }
}
```

**Benefits of server-side**:
- Faster predictions (no multiple API calls)
- More sophisticated models (machine learning)
- Historical accuracy tracking
- Caching and optimization at scale

**Migration path**:
1. Implement `/api/mobile/matches/{id}/predict` endpoint
2. Add method to ApiService
3. Update ViewModel to call server endpoint
4. Keep client-side logic as fallback

## Conclusion

The Match Prediction feature provides a powerful tool for strategic planning during FRC events. By leveraging historical scouting data and statistical modeling, it gives teams actionable insights for match preparation, alliance selection, and competitive strategy.

**Key Benefits**:
- ? Real-time predictions based on actual event data
- ? Period-specific breakdowns for detailed analysis
- ? Statistical confidence indicators
- ? User-friendly visual presentation
- ? Handles missing data gracefully
- ? Extensible architecture for future enhancements

For questions or feature requests, refer to the project documentation or contact the development team.
