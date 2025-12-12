using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;
using Microcharts;
using SkiaSharp;
using System.IO;
using Microsoft.Maui.Controls;

namespace ObsidianScout.ViewModels;

public partial class GraphsViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;
    private readonly IConnectivityService _connectivityService;
    private GameConfig? _gameConfig;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Event> events = new();

    [ObservableProperty]
    private Event? selectedEvent;

    [ObservableProperty]
    private ObservableCollection<Team> teams = new();
    
    [ObservableProperty]
    private ObservableCollection<Team> availableTeams = new();

    [ObservableProperty]
    private ObservableCollection<Team> selectedTeams = new();

    [ObservableProperty]
    private ObservableCollection<MetricDefinition> availableMetrics = new();

    [ObservableProperty]
    private MetricDefinition? selectedMetric;

    [ObservableProperty]
    private CompareTeamsResponse? comparisonData;

    [ObservableProperty]
    private string selectedGraphType = "line"; // Changed from "bar" to "line"
    
    [ObservableProperty]
    private string selectedDataView = "averages"; // "averages" or "match_by_match"

    [ObservableProperty]
    private bool hasGraphData;
    
    // Legacy Microcharts properties (kept for fallback)
    [ObservableProperty]
    private Chart? currentChart;

    [ObservableProperty]
    private ObservableCollection<Chart> teamCharts = new();
    
    [ObservableProperty]
    private ObservableCollection<TeamChartInfo> teamChartsWithInfo = new();

    [ObservableProperty]
    private ImageSource? serverGraphImage;

    [ObservableProperty]
    private bool useServerImage;

    [ObservableProperty]
    private bool showMicrocharts = true;

    // Define team colors
    private readonly string[] TeamColors = new[]
    {
        "#FF6384", "#36A2EB", "#FFCE56", "#4BC0C0", "#9966FF", "#FF9F40"
    };

    public GraphsViewModel(IApiService apiService, ISettingsService settingsService, IConnectivityService connectivityService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
        _connectivityService = connectivityService;
    }

    public async Task InitializeAsync()
    {
        await LoadGameConfigAsync();
        await LoadEventsAsync();
        await LoadMetricsAsync();
    }

    private async Task LoadGameConfigAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Loading game config for graphs...");
            var response = await _apiService.GetGameConfigAsync();
            
            if (response.Success && response.Config != null)
            {
                _gameConfig = response.Config;
                System.Diagnostics.Debug.WriteLine($"Game config loaded: {_gameConfig.GameName}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load game config: {response.Error}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading game config: {ex.Message}");
        }
    }

    public async Task LoadEventsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading events...";

            var response = await _apiService.GetEventsAsync();
            
            if (response.Success && response.Events != null)
            {
                Events = new ObservableCollection<Event>(response.Events);
                StatusMessage = $"{Events.Count} events loaded";
                
                // Try to auto-select the current event from game config
                Event? eventToSelect = null;
                
                if (_gameConfig != null && !string.IsNullOrEmpty(_gameConfig.CurrentEventCode))
                {
                    // Find event matching current event code
                    eventToSelect = Events.FirstOrDefault(e => 
                        e.Code.Equals(_gameConfig.CurrentEventCode, StringComparison.OrdinalIgnoreCase));
                    
                    if (eventToSelect != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Auto-selected current event: {eventToSelect.Name} ({eventToSelect.Code})");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Current event code '{_gameConfig.CurrentEventCode}' not found in events list");
                    }
                }
                
                // Fallback to first event if current event not found
                if (eventToSelect == null && Events.Count > 0)
                {
                    eventToSelect = Events[0];
                    System.Diagnostics.Debug.WriteLine($"Falling back to first event: {eventToSelect.Name}");
                }
                
                if (eventToSelect != null)
                {
                    SelectedEvent = eventToSelect;
                }
            }
            else
            {
                StatusMessage = response.Error ?? "Failed to load events";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading events: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMetricsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("DEBUG: Loading metrics...");
            
            var response = await _apiService.GetAvailableMetricsAsync();
            
            System.Diagnostics.Debug.WriteLine($"DEBUG: Metrics API response - Success: {response.Success}");
            
            if (response.Success && response.Metrics != null && response.Metrics.Count > 0)
            {
                AvailableMetrics = new ObservableCollection<MetricDefinition>(response.Metrics);
                System.Diagnostics.Debug.WriteLine($"DEBUG: Loaded {AvailableMetrics.Count} metrics from API");
                
                // Auto-select total points metric
                SelectedMetric = AvailableMetrics.FirstOrDefault(m => m.Id == "tot" || m.Id == "total_points");
                System.Diagnostics.Debug.WriteLine($"DEBUG: Selected metric: {SelectedMetric?.Name ?? "None"}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: Metrics API failed or empty. Error: {response.Error}");
                // Fallback to default metrics if API not available
                LoadDefaultMetrics();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: Exception loading metrics: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Fallback to default metrics on error
            LoadDefaultMetrics();
        }
    }

    private void LoadDefaultMetrics()
    {
        System.Diagnostics.Debug.WriteLine("DEBUG: Loading default metrics as fallback");
        
        var defaultMetrics = new List<MetricDefinition>
        {
            new MetricDefinition
            {
                Id = "total_points",
                Name = "Total Points",
                Category = "scoring",
                Description = "Average total points scored per match",
                Unit = "points",
                HigherIsBetter = true
            },
            new MetricDefinition
            {
                Id = "auto_points",
                Name = "Auto Points",
                Category = "scoring",
                Description = "Average autonomous points",
                Unit = "points",
                HigherIsBetter = true
            },
            new MetricDefinition
            {
                Id = "teleop_points",
                Name = "Teleop Points",
                Category = "scoring",
                Description = "Average teleoperated points",
                Unit = "points",
                HigherIsBetter = true
            },
            new MetricDefinition
            {
                Id = "endgame_points",
                Name = "Endgame Points",
                Category = "scoring",
                Description = "Average endgame points",
                Unit = "points",
                HigherIsBetter = true
            },
            new MetricDefinition
            {
                Id = "consistency",
                Name = "Consistency",
                Category = "performance",
                Description = "Performance consistency (0-1, higher is better)",
                Unit = "ratio",
                HigherIsBetter = true
            },
            new MetricDefinition
            {
                Id = "win_rate",
                Name = "Win Rate",
                Category = "performance",
                Description = "Percentage of matches won",
                Unit = "percentage",
                HigherIsBetter = true
            }
        };
        
        AvailableMetrics = new ObservableCollection<MetricDefinition>(defaultMetrics);
        SelectedMetric = AvailableMetrics.FirstOrDefault();
        
        System.Diagnostics.Debug.WriteLine($"DEBUG: Loaded {AvailableMetrics.Count} default metrics");
        StatusMessage = "Using default metrics (API endpoint not available)";
    }

    [RelayCommand]
    private void ChangeDataView(string dataView)
    {
        System.Diagnostics.Debug.WriteLine($"=== CHANGE DATA VIEW ===");
        System.Diagnostics.Debug.WriteLine($"From: {SelectedDataView} → To: {dataView}");
        
        SelectedDataView = dataView;
        
        System.Diagnostics.Debug.WriteLine($"SelectedDataView now set to: {SelectedDataView}");
        
        if (HasGraphData)
        {
            // Clear old chart and regenerate with new view
            CurrentChart = null;
            System.Diagnostics.Debug.WriteLine("Cleared chart for data view change, calling GenerateGraphsAsync");
            
            // Regenerate graphs with new data view
            _ = GenerateGraphsAsync();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("HasGraphData is false, not regenerating");
        }
    }

    [RelayCommand]
    private async Task GenerateGraphsAsync()
    {
        if (SelectedEvent == null)
        {
            StatusMessage = "Please select an event";
            return;
        }

        if (SelectedTeams.Count < 1)
        {
            StatusMessage = "Please select at least 1 team";
            return;
        }

        if (SelectedMetric == null)
        {
            StatusMessage = "Please select a metric";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Fetching scouting data...";
            
            // Clear old chart and data before generating new
            CurrentChart = null;
            ComparisonData = null;
            HasGraphData = false;
            TeamCharts.Clear();
            TeamChartsWithInfo.Clear();
            ServerGraphImage = null;
            UseServerImage = false;
            ShowMicrocharts = true;

            System.Diagnostics.Debug.WriteLine("Cleared old chart and comparison data");
            
            // If online and offline-mode NOT enabled, try server image endpoint first
            var offlineMode = await _settingsService.GetOfflineModeAsync();
            if (!offlineMode && _connectivityService.IsConnected)
            {
                try
                {
                    StatusMessage = "Requesting server-generated graph image...";
                    
                    System.Diagnostics.Debug.WriteLine($"=== BUILDING REQUEST ===");
                    System.Diagnostics.Debug.WriteLine($"Current SelectedDataView: '{SelectedDataView}'");
                    System.Diagnostics.Debug.WriteLine($"Current SelectedGraphType: '{SelectedGraphType}'");

                    var request = new GraphImageRequest
                    {
                        TeamNumbers = SelectedTeams.Select(t => t.TeamNumber).ToList(),
                        EventId = SelectedEvent.Id,
                        Metric = SelectedMetric.Id,
                        GraphType = SelectedGraphType,  // Use singular graph_type
                        Mode = SelectedDataView == "match_by_match" ? "match_by_match" : "averages"  // Use 'mode' to match Python script
                    };
                    
                    System.Diagnostics.Debug.WriteLine($"📊 REQUEST BUILT:");
                    System.Diagnostics.Debug.WriteLine($"  teams: [{string.Join(",", request.TeamNumbers)}]");
                    System.Diagnostics.Debug.WriteLine($"  event: {request.EventId}");
                    System.Diagnostics.Debug.WriteLine($"  metric: {request.Metric}");
                    System.Diagnostics.Debug.WriteLine($"  graph_type: {request.GraphType}");
                    System.Diagnostics.Debug.WriteLine($"  mode: {request.Mode}");

                    var bytes = await _apiService.GetGraphsImageAsync(request);
                    if (bytes != null && bytes.Length >0)
                    {
                        // Force clear old image first to prevent caching issues
                        ServerGraphImage = null;
                        UseServerImage = false;
                        OnPropertyChanged(nameof(ServerGraphImage));
                        OnPropertyChanged(nameof(UseServerImage));
                        
                        // Small delay to ensure UI clears the old image
                        await Task.Delay(100);
                        
                        // Create a copy of bytes to ensure it's truly unique
                        var bytesCopy = new byte[bytes.Length];
                        Array.Copy(bytes, bytesCopy, bytes.Length);
                        
                        // Create new image from stream with fresh data
                        // Use StreamImageSource to avoid caching issues
                        var imageSource = new StreamImageSource
                        {
                            Stream = cancellationToken => Task.FromResult<Stream>(new MemoryStream(bytesCopy))
                        };
                        
                        ServerGraphImage = imageSource;
                        UseServerImage = true;
                        ShowMicrocharts = false;
                        HasGraphData = true;
                        StatusMessage = "Server graph image loaded";

                        // Notify UI
                        OnPropertyChanged(nameof(ServerGraphImage));
                        OnPropertyChanged(nameof(UseServerImage));
                        OnPropertyChanged(nameof(ShowMicrocharts));
                        OnPropertyChanged(nameof(HasGraphData));
                        
                        System.Diagnostics.Debug.WriteLine($"Server image updated: {bytes.Length} bytes, first 4 bytes: {bytes[0]} {bytes[1]} {bytes[2]} {bytes[3]}");

                        return; // done
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Server did not return an image, falling back to local generation");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Server image request failed: {ex.Message}");
                    // fall back to local generation
                }
            }

            // Existing offline/local processing (unchanged)
            System.Diagnostics.Debug.WriteLine($"=== GENERATING GRAPHS FROM SCOUTING DATA ===");
            System.Diagnostics.Debug.WriteLine($"Selected Event ID: {SelectedEvent.Id}, Name: {SelectedEvent.Name}");
            System.Diagnostics.Debug.WriteLine($"Selected Teams Count: {SelectedTeams.Count}");
            System.Diagnostics.Debug.WriteLine($"Selected Teams: {string.Join(", ", SelectedTeams.Select(t => $"{t.TeamNumber} - {t.TeamName}"))}");
            System.Diagnostics.Debug.WriteLine($"Current Event Teams Count: {Teams.Count}");
            System.Diagnostics.Debug.WriteLine($"Metric: {SelectedMetric.Id}");
            System.Diagnostics.Debug.WriteLine($"Data View: {SelectedDataView}");

            // VALIDATION: Ensure all selected teams are from the current event
            var validTeamNumbers = Teams.Select(t => t.TeamNumber).ToHashSet();
            var selectedTeamNumbers = SelectedTeams.Select(t => t.TeamNumber).ToHashSet();
            
            System.Diagnostics.Debug.WriteLine($"Valid team numbers from event: {string.Join(", ", validTeamNumbers)}");
            System.Diagnostics.Debug.WriteLine($"Selected team numbers: {string.Join(", ", selectedTeamNumbers)}");
            
            var invalidTeams = SelectedTeams.Where(st => !validTeamNumbers.Contains(st.TeamNumber)).ToList();
            
            if (invalidTeams.Any())
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ WARNING: Found {invalidTeams.Count} teams not in current event!");
                foreach (var invalidTeam in invalidTeams)
                {
                    System.Diagnostics.Debug.WriteLine($"  - Team {invalidTeam.TeamNumber} ({invalidTeam.TeamName}) is selected but not in event {SelectedEvent.Name}");
                }
                
                // Remove invalid teams
                foreach (var invalidTeam in invalidTeams.ToList())
                {
                    SelectedTeams.Remove(invalidTeam);
                    System.Diagnostics.Debug.WriteLine($"  ✓ Removed team {invalidTeam.TeamNumber} from selection");
                }
                UpdateAvailableTeams();
                
                StatusMessage = $"Removed {invalidTeams.Count} team(s) not in this event. {SelectedTeams.Count} teams remain selected.";
                
                if (SelectedTeams.Count == 0)
                {
                    StatusMessage = "All selected teams were from a different event. Please select teams from this event.";
                    return;
                }
                
                // Continue with valid teams
                System.Diagnostics.Debug.WriteLine($"Continuing with {SelectedTeams.Count} valid teams");
            }

            // Fetch scouting data for all selected teams
            var allEntries = new List<ScoutingEntry>();
            
            foreach (var team in SelectedTeams)
            {
                System.Diagnostics.Debug.WriteLine($"Fetching data for team {team.TeamNumber} at event {SelectedEvent.Id}...");
                var response = await _apiService.GetAllScoutingDataAsync(
                    teamNumber: team.TeamNumber, 
                    eventId: SelectedEvent.Id,
                    limit: 100
                );
                
                if (response.Success && response.Entries != null)
                {
                    System.Diagnostics.Debug.WriteLine($"  ✓ Found {response.Entries.Count} entries for team {team.TeamNumber}");
                    
                    // CRITICAL FIX: Only add entries for THIS specific team
                    var teamEntries = response.Entries.Where(e => e.TeamNumber == team.TeamNumber).ToList();
                    System.Diagnostics.Debug.WriteLine($"  ✓ After filtering: {teamEntries.Count} entries for team {team.TeamNumber}");
                    allEntries.AddRange(teamEntries);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  ✗ No data for team {team.TeamNumber}: {response.Error}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Total entries fetched: {allEntries.Count}");
            System.Diagnostics.Debug.WriteLine($"Entries by team: {string.Join(", ", allEntries.GroupBy(e => e.TeamNumber).Select(g => $"Team {g.Key}={g.Count()}"))}");

            // Fallback: If we fetched nothing per-team (maybe cached API returns empty for per-team queries), try fetching all cached scouting data for the event and filter locally
            if (allEntries.Count ==0)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("No per-team entries found. Attempting to fetch all cached scouting data for event and filter locally as fallback.");
                    var allResponse = await _apiService.GetAllScoutingDataAsync(teamNumber: null, eventId: SelectedEvent.Id, limit:1000);
                    if (allResponse.Success && allResponse.Entries != null && allResponse.Entries.Count >0)
                    {
                        var fallbackFiltered = allResponse.Entries.Where(e => selectedTeamNumbers.Contains(e.TeamNumber)).ToList();
                        System.Diagnostics.Debug.WriteLine($"Fallback: found {allResponse.Entries.Count} entries for event; {fallbackFiltered.Count} match selected teams");
                        allEntries.AddRange(fallbackFiltered);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Fallback fetch returned no entries");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback fetch failed: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Total entries after fallback: {allEntries.Count}");

            // CRITICAL VALIDATION: Ensure only selected teams are in the data
            var entriesTeamNumbers = allEntries.Select(e => e.TeamNumber).Distinct().ToHashSet();
            var unexpectedTeams = entriesTeamNumbers.Except(selectedTeamNumbers).ToList();
            
            if (unexpectedTeams.Any())
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ CRITICAL WARNING: Found data for {unexpectedTeams.Count} unexpected teams!");
                System.Diagnostics.Debug.WriteLine($"Unexpected teams: {string.Join(", ", unexpectedTeams)}");
                System.Diagnostics.Debug.WriteLine($"Filtering out unexpected teams...");

                // Remove entries for teams that weren't selected
                allEntries = allEntries.Where(e => selectedTeamNumbers.Contains(e.TeamNumber)).ToList();
                System.Diagnostics.Debug.WriteLine($"After filtering: {allEntries.Count} entries remain");
                System.Diagnostics.Debug.WriteLine($"Filtered entries by team: {string.Join(", ", allEntries.GroupBy(e => e.TeamNumber).Select(g => $"Team {g.Key}={g.Count()}"))}");
            }

            if (allEntries.Count == 0)
            {
                StatusMessage = "No scouting data found for selected teams at this event";
                HasGraphData = false;
                System.Diagnostics.Debug.WriteLine("No data found - aborting graph generation");
                return;
            }

            // Process data based on view mode
            if (SelectedDataView == "match_by_match")
            {
                GenerateMatchByMatchData(allEntries);
            }
            else
            {
                GenerateTeamAveragesData(allEntries);
            }

            // FINAL VALIDATION: Verify comparison data only has selected teams
            if (ComparisonData != null)
            {
                var comparisonTeamNumbers = ComparisonData.Teams.Select(t => t.TeamNumber).ToHashSet();
                var unexpectedComparisonTeams = comparisonTeamNumbers.Except(selectedTeamNumbers).ToList();
                
                if (unexpectedComparisonTeams.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ ERROR: ComparisonData has {unexpectedComparisonTeams.Count} unexpected teams!");
                    System.Diagnostics.Debug.WriteLine($"Unexpected comparison teams: {string.Join(", ", unexpectedComparisonTeams)}");
                    
                    // Filter comparison data
                    ComparisonData.Teams = ComparisonData.Teams.Where(t => selectedTeamNumbers.Contains(t.TeamNumber)).ToList();
                    System.Diagnostics.Debug.WriteLine($"Filtered ComparisonData.Teams to {ComparisonData.Teams.Count} teams");
                }
            }

            HasGraphData = true;
            StatusMessage = $"Graphs generated for {SelectedTeams.Count} team(s) with {allEntries.Count} data points";
            System.Diagnostics.Debug.WriteLine($"✓ Graph generation complete: {SelectedTeams.Count} teams, {allEntries.Count} entries");
            
            // Generate the chart
            GenerateChart();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating graphs: {ex.Message}";
            HasGraphData = false;
            CurrentChart = null;
            ComparisonData = null;
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void GenerateMatchByMatchData(List<ScoutingEntry> entries)
    {
        System.Diagnostics.Debug.WriteLine("=== GENERATING MATCH-BY-MATCH DATA ===");
        
        // Group by team
        var teamGroups = entries.GroupBy(e => e.TeamNumber).OrderBy(g => g.Key);
        
        var teamDataList = new List<TeamComparisonData>();
        var graphData = new GraphData
        {
            Type = "line",
            Labels = new List<string>(),
            Datasets = new List<GraphDataset>()
        };

        // Get all unique match numbers across all teams
        var allMatchNumbers = entries.Select(e => e.MatchNumber).Distinct().OrderBy(m => m).ToList();
        graphData.Labels = allMatchNumbers.Select(m => $"Match {m}").ToList();
        
        System.Diagnostics.Debug.WriteLine($"Match numbers: {string.Join(", ", allMatchNumbers)}");

        int colorIndex = 0;
        foreach (var teamGroup in teamGroups)
        {
            var teamNumber = teamGroup.Key;
            var teamEntries = teamGroup.OrderBy(e => e.MatchNumber).ToList();
            
            System.Diagnostics.Debug.WriteLine($"Team {teamNumber}: {teamEntries.Count} matches");
            
            // Calculate metric values for each match
            var matchValues = new List<double>();
            foreach (var matchNum in allMatchNumbers)
            {
                var matchEntry = teamEntries.FirstOrDefault(e => e.MatchNumber == matchNum);
                if (matchEntry != null)
                {
                    var value = ExtractMetricValue(matchEntry.Data, SelectedMetric!.Id);
                    matchValues.Add(value);
                }
                else
                {
                    matchValues.Add(double.NaN); // Missing data point
                }
            }
            
            var avgValue = matchValues.Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Average();
            var stdDev = CalculateStdDev(matchValues.Where(v => !double.IsNaN(v)));
            
            // Add to team comparison data
            var teamName = teamEntries.FirstOrDefault()?.TeamName ?? $"Team {teamNumber}";
            teamDataList.Add(new TeamComparisonData
            {
                TeamNumber = teamNumber,
                TeamName = teamName,
                Color = TeamColors[colorIndex % TeamColors.Length],
                Value = avgValue,
                StdDev = stdDev,
                MatchCount = matchValues.Count(v => !double.IsNaN(v))
            });
            
            // Add dataset
            graphData.Datasets.Add(new GraphDataset
            {
                Label = $"{teamNumber} - {teamName}",
                Data = matchValues,
                BorderColor = TeamColors[colorIndex % TeamColors.Length],
                BackgroundColor = $"{TeamColors[colorIndex % TeamColors.Length]}33", // Add alpha
                Tension = 0.4
            });
            
            colorIndex++;
        }
        
        // Create comparison response
        ComparisonData = new CompareTeamsResponse
        {
            Success = true,
            Event = SelectedEvent,
            Metric = SelectedMetric!.Id,
            MetricDisplayName = SelectedMetric!.Name,
            DataView = SelectedDataView,
            Teams = teamDataList,
            Graphs = new Dictionary<string, GraphData> { { "line", graphData } }
        };
        
        System.Diagnostics.Debug.WriteLine($"Created {teamDataList.Count} team datasets with {allMatchNumbers.Count} match points each");
    }

    private void GenerateTeamAveragesData(List<ScoutingEntry> entries)
    {
        System.Diagnostics.Debug.WriteLine("=== GENERATING TEAM AVERAGES DATA ===");
        
        // Group by team
        var teamGroups = entries.GroupBy(e => e.TeamNumber).OrderBy(g => g.Key);
        
        var teamDataList = new List<TeamComparisonData>();
        var datasets = new List<GraphDataset>();
        var labels = new List<string>();
        
        int colorIndex = 0;
        foreach (var teamGroup in teamGroups)
        {
            var teamNumber = teamGroup.Key;
            var teamEntries = teamGroup.ToList();
            var teamName = teamEntries.FirstOrDefault()?.TeamName ?? $"Team {teamNumber}";
            
            System.Diagnostics.Debug.WriteLine($"Team {teamNumber}: {teamEntries.Count} matches");
            
            // Calculate average metric value
            var values = teamEntries.Select(e => ExtractMetricValue(e.Data, SelectedMetric!.Id)).ToList();
            var avgValue = values.DefaultIfEmpty(0).Average();
            var stdDev = CalculateStdDev(values);
            
            var color = TeamColors[colorIndex % TeamColors.Length];
            
            teamDataList.Add(new TeamComparisonData
            {
                TeamNumber = teamNumber,
                TeamName = teamName,
                Color = color,
                Value = avgValue,
                StdDev = stdDev,
                MatchCount = teamEntries.Count
            });
            
            // Add individual dataset for each team
            datasets.Add(new GraphDataset
            {
                Label = $"{teamNumber} - {teamName}",
                Data = new List<double> { avgValue },
                BackgroundColor = color,
                BorderColor = color
            });
            
            labels.Add(teamNumber.ToString());
            
            colorIndex++;
        }
        
        // Create bar graph data with separate bar for each team
        var graphData = new GraphData
        {
            Type = "bar",
            Labels = labels,
            Datasets = datasets
        };
        
        // Create comparison response
        ComparisonData = new CompareTeamsResponse
        {
            Success = true,
            Event = SelectedEvent,
            Metric = SelectedMetric!.Id,
            MetricDisplayName = SelectedMetric!.Name,
            DataView = SelectedDataView,
            Teams = teamDataList,
            Graphs = new Dictionary<string, GraphData> { { "bar", graphData } }
        };
        
        System.Diagnostics.Debug.WriteLine($"Created averages for {teamDataList.Count} teams with {datasets.Count} datasets");
    }

    private double ExtractMetricValue(Dictionary<string, object> data, string metricId)
    {
        System.Diagnostics.Debug.WriteLine($"Extracting metric '{metricId}' from scouting data...");
        
        // Handle special calculated metrics
        if (metricId.ToLower() == "total_points" || metricId == "tot")
        {
            return CalculateTotalPoints(data);
        }
        
        if (metricId.ToLower() == "auto_points" || metricId == "apt")
        {
            return CalculateAutoPoints(data);
        }
        
        if (metricId.ToLower() == "teleop_points" || metricId == "tpt")
        {
            return CalculateTeleopPoints(data);
        }
        
        if (metricId.ToLower() == "endgame_points" || metricId == "ept")
        {
            return CalculateEndgamePoints(data);
        }

        // Try to find the metric directly in the data
        var possibleKeys = metricId.ToLower() switch
        {
            "consistency" => new[] { "consistency", "consistent" },
            "win_rate" => new[] { "win_rate", "wins" },
            _ => new[] { metricId }
        };

        foreach (var key in possibleKeys)
        {
            if (data.TryGetValue(key, out var value))
            {
                return ConvertToDouble(value);
            }
        }

        // Fallback: try partial key matches (some cached payloads may have slightly different keys)
        try
        {
            var lowerMetric = metricId.ToLower();
            foreach (var kvp in data)
            {
                if (kvp.Key != null && kvp.Key.ToLower().Contains(lowerMetric))
                {
                    var v = ConvertToDouble(kvp.Value);
                    if (v !=0)
                    {
                        System.Diagnostics.Debug.WriteLine($" Fallback: found value for key '{kvp.Key}' = {v}");
                        return v;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($" Fallback key scan failed: {ex.Message}");
        }

        System.Diagnostics.Debug.WriteLine($" Metric '{metricId}' not found, returning 0");
        return 0;
    }

    private double CalculateTotalPoints(Dictionary<string, object> data)
    {
        var auto = CalculateAutoPoints(data);
        var teleop = CalculateTeleopPoints(data);
        var endgame = CalculateEndgamePoints(data);
        
        var total = auto + teleop + endgame;
        System.Diagnostics.Debug.WriteLine($"  Total Points: {auto} + {teleop} + {endgame} = {total}");
        return total;
    }

    private double CalculateAutoPoints(Dictionary<string, object> data)
    {
        if (_gameConfig == null || _gameConfig.AutoPeriod == null)
        {
            System.Diagnostics.Debug.WriteLine("  No game config or auto period, returning 0");
            return 0;
        }

        double points = 0;
        
        foreach (var element in _gameConfig.AutoPeriod.ScoringElements)
        {
            if (data.TryGetValue(element.Id, out var value))
            {
                var count = ConvertToDouble(value);
                var elementPoints = count * element.Points;
                points += elementPoints;
                
                System.Diagnostics.Debug.WriteLine($"  Auto: {element.Id} = {count} × {element.Points} = {elementPoints}");
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"  Auto Total: {points}");
        return points;
    }

    private double CalculateTeleopPoints(Dictionary<string, object> data)
    {
        if (_gameConfig == null || _gameConfig.TeleopPeriod == null)
        {
            System.Diagnostics.Debug.WriteLine("  No game config or teleop period, returning 0");
            return 0;
        }

        double points = 0;
        
        foreach (var element in _gameConfig.TeleopPeriod.ScoringElements)
        {
            if (data.TryGetValue(element.Id, out var value))
            {
                var count = ConvertToDouble(value);
                var elementPoints = count * element.Points;
                points += elementPoints;
                
                System.Diagnostics.Debug.WriteLine($"  Teleop: {element.Id} = {count} × {element.Points} = {elementPoints}");
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"  Teleop Total: {points}");
        return points;
    }

    private double CalculateEndgamePoints(Dictionary<string, object> data)
    {
        if (_gameConfig == null || _gameConfig.EndgamePeriod == null)
        {
            System.Diagnostics.Debug.WriteLine("  No game config or endgame period, returning 0");
            return 0;
        }

        double points = 0;
        
        foreach (var element in _gameConfig.EndgamePeriod.ScoringElements)
        {
            if (data.TryGetValue(element.Id, out var value))
            {
                if (element.Type.ToLower() == "counter")
                {
                    var count = ConvertToDouble(value);
                    var elementPoints = count * element.Points;
                    points += elementPoints;
                    
                    System.Diagnostics.Debug.WriteLine($"  Endgame: {element.Id} = {count} × {element.Points} = {elementPoints}");
                }
                else if (element.Type.ToLower() == "boolean")
                {
                    var isTrue = ConvertToBoolean(value);
                    if (isTrue)
                    {
                        points += element.Points;
                        System.Diagnostics.Debug.WriteLine($"  Endgame: {element.Id} (boolean) = {element.Points}");
                    }
                }
                else if (element.Type.ToLower() == "multiple_choice")
                {
                    var selectedOption = ConvertToString(value);
                    var option = element.Options?.FirstOrDefault(o => o.Name == selectedOption);
                    if (option != null)
                    {
                        points += option.Points;
                        System.Diagnostics.Debug.WriteLine($"  Endgame: {element.Id} (choice) = '{selectedOption}' = {option.Points}");
                    }
                }
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"  Endgame Total: {points}");
        return points;
    }

    private bool ConvertToBoolean(object? value)
    {
        if (value == null) return false;

        try
        {
            if (value is bool b) return b;
            if (value is string s) return bool.TryParse(s, out var result) && result;
            
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.False) return false;
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    return bool.TryParse(str, out var r) && r;
                }
            }

            return Convert.ToBoolean(value);
        }
        catch
        {
            return false;
        }
    }

    private string ConvertToString(object? value, string defaultValue = "")
    {
        if (value == null) return defaultValue;

        try
        {
            if (value is string s) return s;
            
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return jsonElement.GetString() ?? defaultValue;
                }
                return jsonElement.ToString() ?? defaultValue;
            }

            return value.ToString() ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private double ConvertToDouble(object? value)
    {
        if (value == null) return 0;

        try
        {
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is float f) return f;
            if (value is decimal dec) return (double)dec;
            if (value is string s && double.TryParse(s, out var result)) return result;
            
            // Handle JsonElement
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    return jsonElement.GetDouble();
                }
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    return double.TryParse(str, out var r) ? r : 0;
                }
            }

            return Convert.ToDouble(value);
        }
        catch
        {
            return 0;
        }
    }

    private double CalculateStdDev(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        if (valueList.Count < 2) return 0;

        var avg = valueList.Average();
        var sumOfSquares = valueList.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / (valueList.Count - 1));
    }
    
    partial void OnSelectedEventChanged(Event? value)
    {
        if (value != null)
        {
            // Clear selected teams when event changes to avoid showing teams from previous event
            System.Diagnostics.Debug.WriteLine($"Event changed to: {value.Name}. Clearing selected teams.");
            SelectedTeams.Clear();
            ComparisonData = null;
            HasGraphData = false;
            CurrentChart = null;
            
            _ = LoadTeamsForEventAsync();
        }
    }

    [RelayCommand]
    private async Task LoadTeamsForEventAsync()
    {
        if (SelectedEvent == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Loading teams...";

            var response = await _apiService.GetTeamsAsync(eventId: SelectedEvent.Id);
            
            if (response.Success && response.Teams != null)
            {
                // Deduplicate by team number (keep first occurrence) and sort by team number
                var deduplicatedTeams = response.Teams
                    .GroupBy(t => t.TeamNumber)
                    .Select(g => g.First())
                    .OrderBy(t => t.TeamNumber)
                    .ToList();
                
                Teams = new ObservableCollection<Team>(deduplicatedTeams);
                UpdateAvailableTeams();
                StatusMessage = $"{Teams.Count} teams loaded";
                
                System.Diagnostics.Debug.WriteLine($"Loaded {response.Teams.Count} teams, deduplicated to {Teams.Count} unique teams");
            }
            else
            {
                StatusMessage = response.Error ?? "Failed to load teams";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading teams: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateAvailableTeams()
    {
      // Use HashSet for O(1) lookups instead of O(n) per team - HUGE performance improvement!
     var selectedNumbers = SelectedTeams.Select(t => t.TeamNumber).ToHashSet();
  
    // Filter out teams that are already selected and sort by team number
        var available = Teams
         .Where(t => !selectedNumbers.Contains(t.TeamNumber))
       .OrderBy(t => t.TeamNumber)
            .ToList();
        
  // Update the backing field, not the generated property
        availableTeams.Clear();
        foreach (var team in available)
        {
            availableTeams.Add(team);
      }
   
        System.Diagnostics.Debug.WriteLine($"Available teams updated: {availableTeams.Count} teams available, {SelectedTeams.Count} teams selected");
    }

    [RelayCommand]
    private void AddTeamToComparison(Team team)
    {
        // Removed the 6 team limit - allow unlimited teams
        if (!SelectedTeams.Any(t => t.TeamNumber == team.TeamNumber))
        {
            SelectedTeams.Add(team);
            UpdateAvailableTeams();
            StatusMessage = $"Added {team.TeamNumber} - {team.TeamName} ({SelectedTeams.Count} teams selected)";
        }
    }

    [RelayCommand]
    private void RemoveTeamFromComparison(Team team)
    {
        if (SelectedTeams.Contains(team))
        {
            SelectedTeams.Remove(team);
            UpdateAvailableTeams();
            StatusMessage = $"Removed {team.TeamNumber} - {team.TeamName}";
        }
    }

    [RelayCommand]
    private void SelectAllTeams()
    {
        System.Diagnostics.Debug.WriteLine($"=== SELECT ALL TEAMS (HashSet Optimized) ===");
        System.Diagnostics.Debug.WriteLine($"Available teams: {AvailableTeams.Count}");
        System.Diagnostics.Debug.WriteLine($"Current selected: {SelectedTeams.Count}");
        
        // Create HashSet of already-selected team numbers for O(1) lookups
        var selectedNumbers = SelectedTeams.Select(t => t.TeamNumber).ToHashSet();
        
        // Filter once with HashSet instead of repeated .Any() calls
        var teamsToAdd = AvailableTeams
            .Where(t => !selectedNumbers.Contains(t.TeamNumber))
            .ToList();
    
        System.Diagnostics.Debug.WriteLine($"Teams to add: {teamsToAdd.Count}");
        
        // Add teams (still triggers UI updates, but much faster now)
        foreach (var team in teamsToAdd)
        {
          SelectedTeams.Add(team);
        }
     
        UpdateAvailableTeams();
        StatusMessage = $"Selected all {SelectedTeams.Count} teams";
        System.Diagnostics.Debug.WriteLine($"After select all: {SelectedTeams.Count} teams selected");
    }

    [RelayCommand]
    private void ClearSelectedTeams()
    {
        SelectedTeams.Clear();
        UpdateAvailableTeams();
        ComparisonData = null;
        HasGraphData = false;
        CurrentChart = null;
        StatusMessage = "Selection cleared";
    }

    [RelayCommand]
    private void ChangeGraphType(string graphType)
    {
        System.Diagnostics.Debug.WriteLine($"=== CHANGING GRAPH TYPE ===");
        System.Diagnostics.Debug.WriteLine($"From: {SelectedGraphType} → To: {graphType}");
        
        SelectedGraphType = graphType;
        
        if (HasGraphData)
        {
            // Force clear the chart
            CurrentChart = null;
            OnPropertyChanged(nameof(CurrentChart));
            System.Diagnostics.Debug.WriteLine("Cleared old chart with OnPropertyChanged");
            
            // Regenerate graphs (fetches new server image or regenerates local chart)
            _ = GenerateGraphsAsync();
        }
    }
    
    private void GenerateChart()
    {
        if (ComparisonData == null || ComparisonData.Teams.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("No comparison data to generate chart");
            CurrentChart = null;
            return;
        }

        System.Diagnostics.Debug.WriteLine($"=== GENERATING CHART ===");
        System.Diagnostics.Debug.WriteLine($"Chart Type: {SelectedGraphType}");
        System.Diagnostics.Debug.WriteLine($"Data View: {SelectedDataView}");
        System.Diagnostics.Debug.WriteLine($"Teams in data: {ComparisonData.Teams.Count}");

        // Force clear existing charts
        CurrentChart = null;
        OnPropertyChanged(nameof(CurrentChart));

        // Check if we have graph data from processed scouting data
        if (ComparisonData.Graphs.TryGetValue(SelectedGraphType.ToLower(), out var graphData) &&
            graphData.Datasets.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"Using processed graph data");
            GenerateChartFromServerData(graphData);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Using team averages (fallback)");
            GenerateChartFromTeamAverages();
        }

        UseServerImage = false;
        ShowMicrocharts = true;
        OnPropertyChanged(nameof(UseServerImage));
        OnPropertyChanged(nameof(ShowMicrocharts));
    }

    private void GenerateChartFromServerData(GraphData graphData)
    {
        System.Diagnostics.Debug.WriteLine($"Graph has {graphData.Datasets.Count} datasets and {graphData.Labels.Count} labels");

        if (SelectedDataView == "match_by_match" && SelectedGraphType.ToLower() == "line")
        {
            System.Diagnostics.Debug.WriteLine("=== GENERATING SEPARATE LINE CHARTS PER TEAM ===");
            
            // Create separate line chart for each team
            TeamCharts.Clear();
            TeamChartsWithInfo.Clear();
            
            int chartIndex = 0;
            foreach (var dataset in graphData.Datasets)
            {
                var entries = new List<ChartEntry>();
                var color = TryParseColor(dataset.BorderColor?.ToString()) ?? 
                           SKColor.Parse(TeamColors[chartIndex % TeamColors.Length]);
                
                var teamNumber = dataset.Label.Split('-')[0].Trim();
                var teamName = dataset.Label.Contains('-') ? dataset.Label.Split('-')[1].Trim() : $"Team {teamNumber}";
                
                System.Diagnostics.Debug.WriteLine($"Creating chart {chartIndex + 1} for Team {teamNumber} - {teamName}");
                System.Diagnostics.Debug.WriteLine($"  Dataset has {dataset.Data.Count} data points, Labels has {graphData.Labels.Count} entries");
                
                for (int i = 0; i < dataset.Data.Count && i < graphData.Labels.Count; i++)
                {
                    var value = dataset.Data[i];
                    if (!double.IsNaN(value) && value >= 0)  // Include zero values
                    {
                        entries.Add(new ChartEntry((float)value)
                        {
                            Label = graphData.Labels[i],  // Just "Match 1", "Match 22", etc.
                            ValueLabel = value.ToString("F1"),
                            Color = color
                        });
                        System.Diagnostics.Debug.WriteLine($"    {graphData.Labels[i]}: {value:F1} (Color: {color})");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"    {graphData.Labels[i]}: {(double.IsNaN(value) ? "NaN" : value.ToString("F1"))} (skipped)");
                    }
                }
                
                if (entries.Count > 0)
                {
                    // Calculate max value for better scaling
                    var maxValue = entries.Max(e => e.Value);
                    var minValue = 0f;
                    
                    var teamChart = new LineChart
                    {
                        Entries = entries,
                        LineMode = LineMode.Straight,
                        LineSize = 4,
                        PointMode = PointMode.Circle,
                        PointSize = 18,
                        LabelTextSize = 20,
                        ValueLabelTextSize = 14,
                        LabelOrientation = Orientation.Horizontal,
                        ValueLabelOrientation = Orientation.Horizontal,
                        BackgroundColor = SKColors.Transparent,
                        LabelColor = SKColors.LightGray,
                        IsAnimated = false,
                        EnableYFadeOutGradient = false,
                        MinValue = minValue
                    };
                    
                    // Don't set MaxValue - let it auto-scale
                    
                    TeamCharts.Add(teamChart);
                    TeamChartsWithInfo.Add(new TeamChartInfo
                    {
                        TeamNumber = teamNumber,
                        TeamName = teamName,
                        Color = dataset.BorderColor?.ToString() ?? TeamColors[chartIndex % TeamColors.Length],
                        Chart = teamChart
                    });
                    
                    System.Diagnostics.Debug.WriteLine($"  ✓ Added chart for Team {teamNumber} with {entries.Count} points (range: {minValue} - {maxValue})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  ✗ Skipped chart for Team {teamNumber} - no valid data points");
                }
                
                chartIndex++;
            }
            
            // Set first chart as current for compatibility, but TeamCharts will be used in UI
            CurrentChart = TeamCharts.FirstOrDefault();
            System.Diagnostics.Debug.WriteLine($"=== Created {TeamCharts.Count} separate team charts ===");
            System.Diagnostics.Debug.WriteLine($"TeamCharts collection count: {TeamCharts.Count}");
            System.Diagnostics.Debug.WriteLine($"TeamChartsWithInfo collection count: {teamChartsWithInfo.Count}");
            System.Diagnostics.Debug.WriteLine($"CurrentChart is null: {CurrentChart == null}");
        }
        else if (SelectedDataView == "averages" && SelectedGraphType.ToLower() == "bar")
        {
            // Clear team charts for non-multi-line views
            TeamCharts.Clear();
            TeamChartsWithInfo.Clear();
            
            var entries = new List<ChartEntry>();
            
            System.Diagnostics.Debug.WriteLine("=== GENERATING TEAM AVERAGES BAR CHART ===");
            
            // For bar charts with team averages, create one bar per team
            for (int i = 0; i < graphData.Datasets.Count; i++)
            {
                var dataset = graphData.Datasets[i];
                if (dataset.Data.Count == 0) continue;
                
                // Get color from dataset or use default
                var colorString = dataset.BackgroundColor is List<object> bgColorList && bgColorList.Count > 0
                    ? bgColorList[0]?.ToString()
                    : dataset.BackgroundColor?.ToString();
                
                var color = TryParseColor(colorString) ?? 
                           TryParseColor(dataset.BorderColor?.ToString()) ?? 
                           SKColor.Parse(TeamColors[i % TeamColors.Length]);
                
                // Use average of all data points for this dataset
                var value = dataset.Data.Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Average();
                
                // Extract team number from label (format: "1234 - Team Name")
                var label = graphData.Labels.Count > i ? graphData.Labels[i] : dataset.Label;
                
                System.Diagnostics.Debug.WriteLine($"  Team {label}: Avg = {value:F1}, Color = {color}");
                
                entries.Add(new ChartEntry((float)value)
                {
                    Label = $"#{label}",
                    ValueLabel = value.ToString("F1"),
                    Color = color
                });
            }
            
            System.Diagnostics.Debug.WriteLine($"Total bar chart entries: {entries.Count}");
            System.Diagnostics.Debug.WriteLine($"⚠️ CRITICAL BAR CHART DEBUG:");
            for (int j = 0; j < entries.Count; j++)
            {
                System.Diagnostics.Debug.WriteLine($"  Entry[{j}]: Label='{entries[j].Label}', Value={entries[j].Value}, ValueLabel='{entries[j].ValueLabel}', Color={entries[j].Color}");
            }
            
            if (entries.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No entries created - falling back to team averages");
                GenerateChartFromTeamAverages();
                return;
            }

            var maxVal = entries.Max(e => e.Value) ?? 100f;
            var calculatedMax = maxVal * 1.2f; // Use 20% padding instead of 10%
            
            System.Diagnostics.Debug.WriteLine($"⚠️ BAR CHART SCALE:");
            System.Diagnostics.Debug.WriteLine($"  Max entry value: {maxVal}");
            System.Diagnostics.Debug.WriteLine($"  Calculated MaxValue (with 20% padding): {calculatedMax}");
            System.Diagnostics.Debug.WriteLine($"  MinValue: 0");
            
            // Force proportional bars by setting internal scale
            foreach (var entry in entries)
            {
                System.Diagnostics.Debug.WriteLine($"  Bar '{entry.Label}': {entry.Value}/{calculatedMax} = {(entry.Value / calculatedMax * 100):F1}% height");
            }

            CurrentChart = new BarChart
            {
                Entries = entries,
                LabelTextSize = 32,
                ValueLabelTextSize = 18,
                LabelOrientation = Orientation.Horizontal,
                ValueLabelOrientation = Orientation.Horizontal,
                BackgroundColor = SKColors.Transparent,
                LabelColor = SKColors.Gray,
                IsAnimated = false,
                BarAreaAlpha = 255,
                MinValue = 0f,  // Ensure bars start at zero
                MaxValue = calculatedMax  // Use calculated max with 20% padding
            };
            
            System.Diagnostics.Debug.WriteLine($"✓ BarChart created with MinValue=0, MaxValue={calculatedMax}");
            System.Diagnostics.Debug.WriteLine($"✓ Chart entries count: {((BarChart)CurrentChart).Entries.Count()}");
        }
        else
        {
            // Clear team charts for other views
            TeamCharts.Clear();
            TeamChartsWithInfo.Clear();
            
            System.Diagnostics.Debug.WriteLine("=== FALLBACK CHART GENERATION ===");
            
            var entries = new List<ChartEntry>();
            
            // General fallback for other chart types
            for (int i = 0; i < graphData.Datasets.Count; i++)
            {
                var dataset = graphData.Datasets[i];
                if (dataset.Data.Count == 0) continue;
                
                var colorString = dataset.BackgroundColor is List<object> bgColorList && bgColorList.Count > 0
                    ? bgColorList[0]?.ToString()
                    : dataset.BackgroundColor?.ToString();
                
                var color = TryParseColor(colorString) ?? 
                           TryParseColor(dataset.BorderColor?.ToString()) ?? 
                           SKColor.Parse(TeamColors[i % TeamColors.Length]);
                
                var value = dataset.Data.Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Average();
                
                entries.Add(new ChartEntry((float)value)
                {
                    Label = dataset.Label,
                    ValueLabel = value.ToString("F1"),
                    Color = color
                });
            }

            if (entries.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No entries created - falling back to team averages");
                GenerateChartFromTeamAverages();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Created {entries.Count} chart entries");

            // Generate new chart based on selected type
            CurrentChart = SelectedGraphType.ToLower() switch
            {
                "line" => new LineChart
                {
                    Entries = entries,
                    LineMode = LineMode.Straight,
                    LineSize = 2,
                    PointMode = PointMode.Circle,
                    PointSize = 15,
                    LabelTextSize = 20,
                    ValueLabelTextSize = 12,
                    LabelOrientation = Orientation.Horizontal,
                    ValueLabelOrientation = Orientation.Horizontal,
                    BackgroundColor = SKColors.Transparent,
                    LabelColor = SKColors.Gray,
                    IsAnimated = false,
                    EnableYFadeOutGradient = false
                },
                
                "bar" => new BarChart
                {
                    Entries = entries,
                    LabelTextSize = 32,
                    ValueLabelTextSize = 18,
                    LabelOrientation = Orientation.Horizontal,
                    ValueLabelOrientation = Orientation.Horizontal,
                    BackgroundColor = SKColors.Transparent,
                    LabelColor = SKColors.Gray,
                    IsAnimated = false,
                    BarAreaAlpha = 255,
                    MinValue = 0,  // Ensure bars start at zero
                    MaxValue = entries.Count > 0 ? (entries.Max(e => e.Value) ?? 100f) * 1.1f : 100f  // Add 10% padding at top
                },
                
                "radar" => new RadarChart
                {
                    Entries = entries,
                    LabelTextSize = 32,
                    BackgroundColor = SKColors.Transparent,
                    LabelColor = SKColors.Gray,
                    IsAnimated = false,
                    BorderLineSize = 3,
                    PointSize = 12
                },
                
                _ => (Chart)new BarChart { Entries = entries, IsAnimated = false }
            };
        }
        
        OnPropertyChanged(nameof(CurrentChart));
        OnPropertyChanged(nameof(TeamCharts));
        System.Diagnostics.Debug.WriteLine($"Chart created and set: {CurrentChart?.GetType().Name}");
    }
    
    private void GenerateChartFromTeamAverages()
    {
        // Clear team charts for averages view
        TeamCharts.Clear();
        TeamChartsWithInfo.Clear();
        
        // Fallback: Create chart entries from team comparison data
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

        System.Diagnostics.Debug.WriteLine($"{DateTime.Now:HH:mm:ss} === GenerateChartFromTeamAverages ===");
        System.Diagnostics.Debug.WriteLine($"Created {entries.Count} entries from team averages");
        System.Diagnostics.Debug.WriteLine($"⚠️ CRITICAL TEAM AVERAGES DEBUG:");
        for (int i = 0; i < entries.Count; i++)
        {
            System.Diagnostics.Debug.WriteLine($"  Entry[{i}]: Label='{entries[i].Label}', Value={entries[i].Value}, ValueLabel='{entries[i].ValueLabel}', Color={entries[i].Color}");
        }
        
        if (entries.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("⚠️ No entries - cannot create chart");
            CurrentChart = null;
            return;
        }

        var maxVal = entries.Max(e => e.Value) ?? 100f;
        var calculatedMax = maxVal * 1.2f; // Use 20% padding
        
        System.Diagnostics.Debug.WriteLine($"⚠️ CHART SCALE:");
        System.Diagnostics.Debug.WriteLine($"  Max entry value: {maxVal}");
        System.Diagnostics.Debug.WriteLine($"  Calculated MaxValue (with 20% padding): {calculatedMax}");
        
        // Calculate expected bar heights
        foreach (var entry in entries)
        {
            System.Diagnostics.Debug.WriteLine($"  Expected bar '{entry.Label}': {entry.Value}/{calculatedMax} = {(entry.Value / calculatedMax * 100):F1}% height");
        }

        // Generate new chart based on selected type with clean rendering
        Chart newChart = SelectedGraphType.ToLower() switch
        {
            "line" => new LineChart
            {
                Entries = entries,
                LineMode = LineMode.Straight,
                LineSize = 3,
                PointMode = PointMode.Circle,
                PointSize = 12,
                LabelTextSize = 36,
                ValueLabelTextSize = 18,
                LabelOrientation = Orientation.Horizontal,
                ValueLabelOrientation = Orientation.Horizontal,
                BackgroundColor = SKColors.Transparent,
                LabelColor = SKColors.Gray,
                IsAnimated = false,
                EnableYFadeOutGradient = false
            },
            
            "bar" => new BarChart
            {
                Entries = entries,
                LabelTextSize = 36,
                ValueLabelTextSize = 18,
                LabelOrientation = Orientation.Horizontal,
                ValueLabelOrientation = Orientation.Horizontal,
                BackgroundColor = SKColors.Transparent,
                LabelColor = SKColors.Gray,
                IsAnimated = false,
                BarAreaAlpha = 255,
                MinValue = 0f,  // CRITICAL: Bars must start at zero
                MaxValue = calculatedMax  // Use calculated max with 20% padding
            },
            
            "radar" => new RadarChart
            {
                Entries = entries,
                LabelTextSize = 36,
                BackgroundColor = SKColors.Transparent,
                LabelColor = SKColors.Gray,
                IsAnimated = false,
                BorderLineSize = 3,
                PointSize = 12
            },
            
            _ => new BarChart 
            { 
                Entries = entries, 
                IsAnimated = false,
                MinValue = 0f,
                MaxValue = calculatedMax
            }
        };
        
        CurrentChart = newChart;
        OnPropertyChanged(nameof(CurrentChart));
        OnPropertyChanged(nameof(TeamCharts));
        
        System.Diagnostics.Debug.WriteLine($"✓ Chart created and set: {CurrentChart?.GetType().Name} with {entries.Count} entries");
        if (CurrentChart is BarChart barChart)
        {
            System.Diagnostics.Debug.WriteLine($"✓ BarChart MinValue={barChart.MinValue}, MaxValue={barChart.MaxValue}");
            System.Diagnostics.Debug.WriteLine($"✓ BarChart Entries count: {barChart.Entries.Count()}");
        }
    }
    
    private SKColor? TryParseColor(string? colorString)
    {
        if (string.IsNullOrEmpty(colorString)) return null;
        
        try
        {
            return SKColor.Parse(colorString);
        }
        catch
        {
            return null;
        }
    }
}
