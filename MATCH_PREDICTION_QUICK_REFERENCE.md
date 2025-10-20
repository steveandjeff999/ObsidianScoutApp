# Match Prediction Feature - Quick Reference

## Overview

The Match Prediction feature allows users to predict match outcomes based on historical team performance data. It analyzes each team's Auto, Teleop, and Endgame performance from previous matches at the current event to predict scores and win probabilities for upcoming matches.

## Features

### ?? Match Prediction
- **Select Event**: Choose the event you want to predict matches for
- **Select Match**: Pick any match from the event's schedule
- **Automatic Analysis**: The app automatically:
  - Fetches historical scouting data for all 6 teams in the match
  - Calculates average points for Auto, Teleop, and Endgame periods
  - Computes standard deviations for confidence intervals
  - Predicts total alliance scores
  - Calculates win probabilities using statistical modeling

### ?? Prediction Display

#### Win Probability
- **Red Alliance**: Displays percentage chance of red alliance winning
- **Blue Alliance**: Displays percentage chance of blue alliance winning
- **Visual Progress Bars**: Shows relative win probabilities

#### Predicted Scores
Shows predicted scores broken down by period:
- **Auto Points**: Predicted autonomous period contribution
- **Teleop Points**: Predicted teleoperated period contribution
- **Endgame Points**: Predicted endgame period contribution
- **Total Points**: Sum of all periods

#### Team Breakdown
For each team in both alliances:
- Team number and name
- Predicted points per period (Auto, Teleop, Endgame, Total)
- Match count (number of matches with historical data)
- Consistency rating (0-100%)

### ?? Data Quality Indicators

**Insufficient Data Warning**
- Shows when one or more teams have no historical data
- Lists which teams are missing data
- Predictions still generated using available data

**Team Statistics**
- **Match Count**: More matches = more reliable prediction
- **Consistency**: Higher percentage = more predictable performance

## How Predictions Work

### Data Collection
1. Fetches all scouting entries for each team at the selected event
2. Filters data to ensure only relevant team matches are used
3. Calculates points using the game config scoring rules

### Point Calculation
Points are calculated using the game configuration:
- **Auto Period**: Sums all auto scoring element values × points per element
- **Teleop Period**: Sums all teleop scoring element values × points per element
- **Endgame Period**: 
  - Counters: value × points
  - Booleans: points if true
  - Multiple choice: points based on selected option

### Statistical Model

**Team Prediction**
- **Average**: Mean of all historical match performances
- **Standard Deviation**: Measure of variability in performance
- **Consistency**: 1 - (StdDev / Average), converted to 0-100% scale

**Alliance Prediction**
- **Total Score**: Sum of all team predicted totals
- **Combined Variance**: Square root of sum of squared standard deviations

**Win Probability**
Uses normal distribution CDF (Cumulative Distribution Function):
1. Calculate score difference: Red Total - Blue Total
2. Calculate combined standard deviation of both alliances
3. Compute Z-score: scoreDiff / combinedStdDev
4. Apply normal CDF to get probability

**Fallback Logic**
If no variance (all teams have StdDev = 0):
- Score difference > 0: 75% / 25% split
- Score difference < 0: 25% / 75% split
- Score difference = 0: 50% / 50% split

## Usage Tips

### Best Practices
1. **Wait for Data**: Predictions are most accurate after teams have played 3+ matches
2. **Check Consistency**: Teams with high consistency (>80%) are more predictable
3. **Review Team Breakdown**: Identify which teams are predicted to contribute most
4. **Consider Warnings**: Teams without data get 0 points in prediction

### Strategic Applications
- **Alliance Selection**: Identify which teams complement each other well
- **Match Strategy**: Understand expected scoring distribution across periods
- **Scouting Priority**: Focus on teams with insufficient data
- **Drive Team Prep**: Set realistic score goals based on predictions

## Technical Details

### Models
- `MatchPrediction`: Complete match prediction with both alliances
- `TeamMatchPrediction`: Individual team prediction data
- `TeamHistoricalStats`: Historical statistics for a team
- `AlliancePrediction`: Alliance-level prediction data

### ViewModel
**MatchPredictionViewModel** handles:
- Event and match selection
- Data fetching from API
- Point calculations using game config
- Statistical computations
- Win probability calculations

### Key Methods
- `PredictMatchAsync()`: Main prediction logic
- `GetTeamHistoricalStatsAsync()`: Fetches and processes team data
- `CreateTeamPrediction()`: Builds team prediction object
- `CalculateAutoPoints()`, `CalculateTeleopPoints()`, `CalculateEndgamePoints()`: Point calculation
- `NormalCDF()`, `Erf()`: Statistical functions for probability calculation

## Navigation

### Access
Main Menu ? ?? Match Prediction

### Page Route
`MatchPredictionPage`

## UI Components

### Selection Controls
- **Event Picker**: Dropdown of available events
- **Match Picker**: Dropdown showing "Qualification Match 1", "Playoff Match 2", etc.

### Match Preview
Shows teams in the selected match:
- ?? Red: Team numbers (comma-separated)
- ?? Blue: Team numbers (comma-separated)

### Predict Button
- Enabled when match is selected
- Shows "?? Predict Match Outcome"
- Triggers prediction analysis

### Results Cards
- **Win Probability Card**: Progress bars with percentages
- **Predicted Scores Card**: Table showing period breakdowns
- **Red Alliance Breakdown**: CollectionView of team predictions
- **Blue Alliance Breakdown**: CollectionView of team predictions

## Styling

Uses the Liquid Glass UI theme:
- `GlassCardStyle`: Semi-transparent cards with blur effect
- `PrimaryGlassButton`: Primary action button style
- Color scheme:
  - Red Alliance: `#FF6B6B`
  - Blue Alliance: `#4ECDC4`
  - Warning: `#FFB700`

## Error Handling

### No Data Available
- Shows warning banner
- Lists teams without data
- Continues prediction with 0 points for those teams

### API Errors
- Displays error in status message
- Logs detailed error to debug console
- Allows retry by selecting match again

### Invalid Match Data
- Checks for empty alliance strings
- Validates team number parsing
- Shows appropriate error message

## Performance Considerations

### Data Fetching
- Fetches up to 100 scouting entries per team
- Caches game config to avoid repeated API calls
- Reuses existing event/match data when possible

### Calculations
- Point calculations use optimized lookups
- Statistical computations use efficient algorithms
- Results are computed once per prediction

## Future Enhancements

Possible improvements:
- **Historical Win Rate**: Factor in team's actual win/loss record
- **Defense Rating**: Include defensive capabilities in prediction
- **Opponent Analysis**: Consider matchup-specific factors
- **Monte Carlo Simulation**: Run multiple scenarios for confidence intervals
- **Export Results**: Save predictions for later comparison
- **Prediction Accuracy Tracking**: Compare predictions to actual results

## Example Prediction

### Scenario
**Qualification Match 15**
- Red Alliance: 5454, 1234, 5678
- Blue Alliance: 9999, 2222, 3333

### Predicted Scores
| Alliance | Auto | Teleop | Endgame | Total |
|----------|------|--------|---------|-------|
| Red      | 25   | 85     | 30      | 140   |
| Blue     | 30   | 75     | 28      | 133   |

### Win Probability
- ?? Red: **62%**
- ?? Blue: **38%**

### Team Breakdown (Red)
- Team 5454: Auto 10, Teleop 40, Endgame 12 = **62 pts** (12 matches, 87% consistent)
- Team 1234: Auto 8, Teleop 25, Endgame 10 = **43 pts** (11 matches, 82% consistent)
- Team 5678: Auto 7, Teleop 20, Endgame 8 = **35 pts** (10 matches, 79% consistent)

## Dependencies

### Services
- `IApiService`: Data fetching from backend
- `ISettingsService`: User preferences and tokens
- `ICacheService`: Offline data caching (via ApiService)

### Models
- `Event`, `Match`, `Team`: Core data models
- `GameConfig`: Game scoring configuration
- `ScoutingEntry`: Historical scouting data
- `MatchPrediction`, `TeamMatchPrediction`: Prediction models

### NuGet Packages
- `CommunityToolkit.Mvvm`: MVVM helpers
- `Microsoft.Maui.*`: .NET MAUI framework

## Troubleshooting

### Predictions Show 0 Points
**Cause**: No scouting data available for teams
**Solution**: 
- Scout more matches before predicting
- Verify teams have been scouted at the selected event
- Check API connection

### Win Probability is 50/50
**Cause**: All teams have equal predicted scores or no variance
**Solution**: 
- This is expected when predictions are very close
- More data will improve differentiation

### "Invalid match - missing team data"
**Cause**: Match alliance fields are empty or invalid
**Solution**:
- Verify match data is populated correctly
- Check API response for match details
- Contact system administrator

## Related Documentation
- `GRAPHS_FIX_QUICK_REFERENCE.md`: Similar statistical analysis for graphs
- `MOBILE_API_JSON_EXAMPLES.md`: Match strategy prediction API examples
- `IMPLEMENTATION_SUMMARY.md`: Overall app architecture
