using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;
using Microcharts;
using SkiaSharp;
using System.IO;
using Microsoft.Maui.Controls;
using System.Text.Json;
using System.ComponentModel;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using Microsoft.Maui.ApplicationModel;
using System.Threading;
// OxyPlot removed from dependencies in this change; keep Microcharts-based fallback

namespace ObsidianScout.ViewModels;

public partial class GraphsViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;
    private readonly IConnectivityService _connectivityService;
    private GameConfig? _gameConfig;
    // Cached scouting entries used to quickly rebuild graphs when switching data view
    private List<ScoutingEntry>? _cachedScoutingEntries;

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

    // Checkbox selections for graph types
    [ObservableProperty]
    private bool isLineSelected = true;

    [ObservableProperty]
    private bool isBarSelected = false;

    [ObservableProperty]
    private bool isRadarSelected = false;

    [ObservableProperty]
    private bool isScatterSelected = false;

    [ObservableProperty]
    private bool isHistogramSelected = false;

    [ObservableProperty]
    private bool isViolinSelected = false;

    [ObservableProperty]
    private bool isBoxPlotSelected = false;

    [ObservableProperty]
    private bool isSunburstSelected = false;

    [ObservableProperty]
    private bool isWaterfallSelected = false;

    // Computed properties for conditional visibility based on data view mode
    public bool ShowLineChart => SelectedDataView == "match_by_match" || SelectedDataView == "averages"; // Show in both modes
    public bool ShowBarChart => SelectedDataView == "averages";
    public bool ShowRadarChart => SelectedDataView == "averages";
    public bool ShowScatterChart => SelectedDataView == "averages"; // Changed to averages for team distribution
    public bool ShowHistogramChart => SelectedDataView == "averages";
    public bool ShowViolinChart => SelectedDataView == "averages";
    public bool ShowBoxPlotChart => SelectedDataView == "averages";
    public bool ShowSunburstChart => SelectedDataView == "averages";
    public bool ShowWaterfallChart => SelectedDataView == "averages";

    public List<string> GetSelectedGraphTypes()
    {
        var types = new List<string>();
        // Only include graph types that are compatible with current data view
        if (isLineSelected && ShowLineChart) types.Add("line");
        if (isBarSelected && ShowBarChart) types.Add("bar");
        if (isRadarSelected && ShowRadarChart) types.Add("radar");
        if (isScatterSelected && ShowScatterChart) types.Add("scatter");
        if (isHistogramSelected && ShowHistogramChart) types.Add("hist");
        if (isBoxPlotSelected && ShowBoxPlotChart) types.Add("box");
        if (isViolinSelected && ShowViolinChart) types.Add("violin");
        if (isSunburstSelected && ShowSunburstChart) types.Add("sunburst");
        if (isWaterfallSelected && ShowWaterfallChart) types.Add("waterfall");
        return types;
    }

    [ObservableProperty]
    private bool hasGraphData;

    // Note: advanced OxyPlot offline model removed when package unavailable; Microcharts used as fallback
    
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

    // Plotly HTML payload for WebView
    [ObservableProperty]
    private string? plotlyHtml;

    [ObservableProperty]
    private string? plotlyPayloadJson;

    [ObservableProperty]
    private bool usePlotlyWebView;

    // Collection of multiple Plotly HTML graphs (one for each selected graph type)
    [ObservableProperty]
    private ObservableCollection<GraphHtmlInfo> plotlyHtmlGraphs = new();

    // Define team colors
    private readonly string[] TeamColors = new[]
    {
        "#FF6384", "#36A2EB", "#FFCE56", "#4BC0C0", "#9966FF", "#FF9F40"
    };

    // Maximum value used for radar charts (used to scale consistency and Plotly polar axis)
    private double _radarMaxValue = 100.0;

    public GraphsViewModel(IApiService apiService, ISettingsService settingsService, IConnectivityService connectivityService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
        _connectivityService = connectivityService;
    }

    // Auto-refresh timer fields
    private CancellationTokenSource? _autoRefreshCts;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Start periodic refresh of server data every 5 minutes.
    /// </summary>
    public void StartAutoRefresh()
    {
        StopAutoRefresh();
        _autoRefreshCts = new CancellationTokenSource();
        var ct = _autoRefreshCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await RefreshServerDataAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Auto-refresh failed: {ex.Message}");
                    }

                    await Task.Delay(_refreshInterval, ct);
                }
            }
            catch (TaskCanceledException) { }
        }, ct);
    }

    public void StopAutoRefresh()
    {
        try
        {
            if (_autoRefreshCts != null && !_autoRefreshCts.IsCancellationRequested)
            {
                _autoRefreshCts.Cancel();
                _autoRefreshCts.Dispose();
            }
        }
        catch { }
        finally { _autoRefreshCts = null; }
    }

    /// <summary>
    /// Refreshes server-cached graph image and preloads data without forcing UI regeneration.
    /// This method is safe to call periodically.
    /// </summary>
    public async Task RefreshServerDataAsync()
    {
        try
        {
            // Only attempt if connected and offline-mode not enabled
            var offlineMode = await _settingsService.GetOfflineModeAsync();
            if (offlineMode || !_connectivityService.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("RefreshServerDataAsync: offline mode or no connectivity, skipping");
                return;
            }

            if (SelectedEvent == null || SelectedTeams == null || SelectedTeams.Count == 0 || SelectedMetric == null)
            {
                System.Diagnostics.Debug.WriteLine("RefreshServerDataAsync: missing selection (event/teams/metric), skipping");
                return;
            }

            var selectedTypes = GetSelectedGraphTypes();
            if (selectedTypes.Count == 0) selectedTypes.Add("line");

            var request = new GraphImageRequest
            {
                TeamNumbers = SelectedTeams.Select(t => t.TeamNumber).ToList(),
                EventId = SelectedEvent.Id,
                Metric = SelectedMetric.Id,
                GraphTypes = selectedTypes,
                Mode = SelectedDataView == "match_by_match" ? "match_by_match" : "averages"
            };

            System.Diagnostics.Debug.WriteLine($"Auto-refresh requesting server image for event {request.EventId} teams [{string.Join(',', request.TeamNumbers)}]");

            // Fire preload and image fetch in parallel
            var preloadTask = PreloadComparisonDataAsync(request.TeamNumbers, request.EventId);
            try
            {
                var bytes = await _apiService.GetGraphsImageAsync(request);
                if (bytes != null && bytes.Length > 0)
                {
                    // Cache server image but don't change UI state here
                    var bytesCopy = new byte[bytes.Length];
                    Array.Copy(bytes, bytesCopy, bytes.Length);
                    var imageSource = new StreamImageSource
                    {
                        Stream = cancellationToken => Task.FromResult<Stream>(new MemoryStream(bytesCopy))
                    };
                    ServerGraphImage = imageSource;
                    OnPropertyChanged(nameof(ServerGraphImage));
                    System.Diagnostics.Debug.WriteLine($"Auto-refresh: cached server image ({bytes.Length} bytes)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-refresh image fetch failed: {ex.Message}");
            }

            _ = preloadTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshServerDataAsync error: {ex.Message}");
        }
    }

    public async Task InitializeAsync()
    {
        await LoadGameConfigAsync();
        await LoadEventsAsync();
        await LoadMetricsAsync();
        // Start periodic refresh after initial load
        StartAutoRefresh();
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
    }

    // Note: ChangeDataView removed - data view is now selected via dropdown
    // and graphs are regenerated when Generate button is clicked

    [RelayCommand]
    private async Task GenerateGraphsAsync()
    {
        System.Diagnostics.Debug.WriteLine($"=== GENERATE GRAPHS CLICKED ===");
        System.Diagnostics.Debug.WriteLine($"SelectedEvent: {SelectedEvent?.Name ?? "null"}");
        System.Diagnostics.Debug.WriteLine($"SelectedTeams.Count: {SelectedTeams.Count}");
        System.Diagnostics.Debug.WriteLine($"SelectedMetric: {SelectedMetric?.Name ?? "null"}");
        
        if (SelectedEvent == null)
        {
            StatusMessage = "Please select an event";
            System.Diagnostics.Debug.WriteLine("❌ No event selected");
            return;
        }

        if (SelectedTeams.Count < 1)
        {
            StatusMessage = "Please select at least 1 team";
            System.Diagnostics.Debug.WriteLine("❌ No teams selected");
            return;
        }

        if (SelectedMetric == null)
        {
            StatusMessage = "Please select a metric";
            System.Diagnostics.Debug.WriteLine("❌ No metric selected");
            return;
        }

        var selectedTypes = GetSelectedGraphTypes();
        System.Diagnostics.Debug.WriteLine($"Selected graph types: {string.Join(", ", selectedTypes)}");
        
        if (selectedTypes.Count == 0)
        {
            StatusMessage = "Please select at least one graph type";
            System.Diagnostics.Debug.WriteLine("❌ No graph types selected");
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
                    System.Diagnostics.Debug.WriteLine($"Selected Graph Types: {string.Join(", ", selectedTypes)}");

                    var request = new GraphImageRequest
                    {
                        TeamNumbers = SelectedTeams.Select(t => t.TeamNumber).ToList(),
                        EventId = SelectedEvent.Id,
                        Metric = SelectedMetric.Id,
                        GraphTypes = selectedTypes.Count > 0 ? selectedTypes : new List<string> { "line" },  // Use multiple graph types
                        Mode = SelectedDataView == "match_by_match" ? "match_by_match" : "averages"  // Use 'mode' to match Python script
                    };
                    
                    System.Diagnostics.Debug.WriteLine($"📊 REQUEST BUILT:");
                    System.Diagnostics.Debug.WriteLine($"  teams: [{string.Join(",", request.TeamNumbers)}]");
                    System.Diagnostics.Debug.WriteLine($"  event: {request.EventId}");
                    System.Diagnostics.Debug.WriteLine($"  metric: {request.Metric}");
                    System.Diagnostics.Debug.WriteLine($"  graph_types: [{string.Join(",", request.GraphTypes)}]");
                    System.Diagnostics.Debug.WriteLine($"  mode: {request.Mode}");

                    // Start preloading raw scouting entries in parallel so local graphs and Plotly HTML
                    // are available quickly even when server returns an image.
                    var preloadTask = PreloadComparisonDataAsync(request.TeamNumbers, request.EventId);
                    var bytes = await _apiService.GetGraphsImageAsync(request);
                    if (bytes != null && bytes.Length >0)
                    {
                        // Store server image but prefer local generation by default. Only switch UI to server image
                        // when UseServerImage is explicitly enabled.
                        try
                        {
                            // Force clear old image first to prevent caching issues
                            ServerGraphImage = null;
                            OnPropertyChanged(nameof(ServerGraphImage));

                            // Small delay to ensure UI clears the old image
                            await Task.Delay(50);

                            // Create a copy of bytes to ensure it's truly unique
                            var bytesCopy = new byte[bytes.Length];
                            Array.Copy(bytes, bytesCopy, bytes.Length);

                            // Create new image from stream with fresh data
                            var imageSource = new StreamImageSource
                            {
                                Stream = cancellationToken => Task.FromResult<Stream>(new MemoryStream(bytesCopy))
                            };

                            // Assign cached server image but don't switch UI unless requested
                            ServerGraphImage = imageSource;
                            OnPropertyChanged(nameof(ServerGraphImage));

                            System.Diagnostics.Debug.WriteLine($"Server image cached: {bytes.Length} bytes");

                            // If the user explicitly wants server image, show it and skip local generation
                            if (UseServerImage)
                            {
                                ShowMicrocharts = false;
                                HasGraphData = true;
                                StatusMessage = "Server graph image loaded";
                                OnPropertyChanged(nameof(ShowMicrocharts));
                                OnPropertyChanged(nameof(HasGraphData));
                                _ = preloadTask;
                                return; // show server image instead of local charts
                            }

                            // Otherwise keep cached image available and continue to local generation so
                            // Plotly HTML and microcharts are generated and can be switched dynamically.
                            System.Diagnostics.Debug.WriteLine("Server image cached but local generation will be used (UseServerImage=false)");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to cache server image: {ex.Message}");
                        }

                        // Ensure preload continues (may already be running)
                        _ = preloadTask;
                        // continue to local generation (do not return)
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

            // Cache entries so mode switches can rebuild charts without re-fetching
            try
            {
                _cachedScoutingEntries = allEntries.ToList();
                System.Diagnostics.Debug.WriteLine($"Cached {_cachedScoutingEntries.Count} scouting entries for quick rebuilds");
            }
            catch { _cachedScoutingEntries = null; }

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

            // Prepare Plotly HTML payloads for all selected graph types
            try
            {
                await PrepareAllPlotlyGraphsAsync();
                UsePlotlyWebView = plotlyHtmlGraphs.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PrepareAllPlotlyGraphsAsync failed: {ex.Message}");
            }
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
        
        // Also build a bar-version of the same match-by-match data so UI/plotly can switch types
        var barGraphData = new GraphData
        {
            Type = "bar",
            Labels = graphData.Labels,
            Datasets = graphData.Datasets.Select(ds => new GraphDataset
            {
                Label = ds.Label,
                Data = ds.Data,
                BorderColor = ds.BorderColor,
                BackgroundColor = ds.BackgroundColor,
                Tension = ds.Tension
            }).ToList()
        };

        // Also build scatter, histogram, and box plot versions
        var scatterGraphData = new GraphData
        {
            Type = "scatter",
            Labels = graphData.Labels,
            Datasets = graphData.Datasets.Select(ds => new GraphDataset
            {
                Label = ds.Label,
                Data = ds.Data,
                BorderColor = ds.BorderColor,
                BackgroundColor = ds.BackgroundColor,
                Tension = ds.Tension
            }).ToList()
        };

        var histogramGraphData = new GraphData
        {
            Type = "hist",
            Labels = graphData.Labels,
            Datasets = graphData.Datasets.Select(ds => new GraphDataset
            {
                Label = ds.Label,
                Data = ds.Data,
                BorderColor = ds.BorderColor,
                BackgroundColor = ds.BackgroundColor,
                Tension = ds.Tension
            }).ToList()
        };

        var boxPlotGraphData = new GraphData
        {
            Type = "box",
            Labels = graphData.Labels,
            Datasets = graphData.Datasets.Select(ds => new GraphDataset
            {
                Label = ds.Label,
                Data = ds.Data,
                BorderColor = ds.BorderColor,
                BackgroundColor = ds.BackgroundColor,
                Tension = ds.Tension
            }).ToList()
        };

        // Build violin graph data for match-by-match (one violin per team using non-NaN values)
        var violinGraphData = new GraphData
        {
            Type = "violin",
            Labels = graphData.Labels,
            Datasets = graphData.Datasets.Select(ds => new GraphDataset
            {
                Label = ds.Label,
                Data = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList(),
                BorderColor = ds.BorderColor,
                BackgroundColor = ds.BackgroundColor,
                Tension = ds.Tension
            }).ToList()
        };

        // Build waterfall graph data for match-by-match (total/average per team)
        var waterfallValues = teamDataList.Select(t => t.Value).ToList();
        var waterfallColors = teamDataList.Select(t => t.Color).ToList();
        var waterfallGraphData = new GraphData
        {
            Type = "waterfall",
            Labels = teamDataList.Select(t => t.TeamNumber.ToString()).ToList(),
            Datasets = new List<GraphDataset>
            {
                new GraphDataset
                {
                    Label = "Waterfall",
                    Data = waterfallValues,
                    BackgroundColor = string.Join(",", waterfallColors),
                    BorderColor = string.Join(",", waterfallColors)
                }
            }
        };

        // Create comparison response with all graph type representations
        ComparisonData = new CompareTeamsResponse
        {
            Success = true,
            Event = SelectedEvent,
            Metric = SelectedMetric!.Id,
            MetricDisplayName = SelectedMetric!.Name,
            DataView = SelectedDataView,
            Teams = teamDataList,
            Graphs = new Dictionary<string, GraphData>
            {
                { "line", graphData },
                { "scatter", scatterGraphData },
                { "bar", barGraphData },
                { "hist", histogramGraphData },
                { "box", boxPlotGraphData },
                { "violin", violinGraphData },
                { "waterfall", waterfallGraphData }
            }
        };
        
        System.Diagnostics.Debug.WriteLine($"Created {teamDataList.Count} team datasets with {allMatchNumbers.Count} match points each");
    }

    // Helper to build match-by-match GraphData and TeamComparisonData from raw entries without mutating existing ComparisonData
    private (GraphData? GraphData, List<TeamComparisonData> Teams) BuildMatchByMatchGraphData(List<ScoutingEntry> entries)
    {
        try
        {
            // reuse logic from GenerateMatchByMatchData but return data instead of assigning ComparisonData
            var teamGroups = entries.GroupBy(e => e.TeamNumber).OrderBy(g => g.Key);
            var teamDataList = new List<TeamComparisonData>();
            var graphData = new GraphData
            {
                Type = "line",
                Labels = new List<string>(),
                Datasets = new List<GraphDataset>()
            };

            var allMatchNumbers = entries.Select(e => e.MatchNumber).Distinct().OrderBy(m => m).ToList();
            graphData.Labels = allMatchNumbers.Select(m => $"Match {m}").ToList();

            int colorIndex = 0;
            foreach (var teamGroup in teamGroups)
            {
                var teamNumber = teamGroup.Key;
                var teamEntries = teamGroup.OrderBy(e => e.MatchNumber).ToList();

                var matchValues = new List<double>();
                foreach (var matchNum in allMatchNumbers)
                {
                    var matchEntry = teamEntries.FirstOrDefault(e => e.MatchNumber == matchNum);
                    if (matchEntry != null)
                        matchValues.Add(ExtractMetricValue(matchEntry.Data, SelectedMetric!.Id));
                    else
                        matchValues.Add(double.NaN);
                }

                var avgValue = matchValues.Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Average();
                var stdDev = CalculateStdDev(matchValues.Where(v => !double.IsNaN(v)));

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

                graphData.Datasets.Add(new GraphDataset
                {
                    Label = $"{teamNumber} - {teamName}",
                    Data = matchValues,
                    BorderColor = TeamColors[colorIndex % TeamColors.Length],
                    BackgroundColor = $"{TeamColors[colorIndex % TeamColors.Length]}33",
                    Tension = 0.4
                });

                colorIndex++;
            }

            return (graphData, teamDataList);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BuildMatchByMatchGraphData failed: {ex.Message}");
            return (null, new List<TeamComparisonData>());
        }
    }

    // Preload and cache comparison data entries for quick view-mode switching without re-fetch
    private async Task PreloadComparisonDataAsync(List<int> teamNumbers, int? eventId)
    {
        try
        {
            if (!eventId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("PreloadComparisonDataAsync: eventId is null, aborting preload");
                return;
            }
            var allEntries = new List<ScoutingEntry>();
            foreach (var tn in teamNumbers)
            {
                var resp = await _apiService.GetAllScoutingDataAsync(teamNumber: tn, eventId: eventId, limit: 1000, ignoreOfflineMode: true);
                if (resp.Success && resp.Entries != null)
                {
                    allEntries.AddRange(resp.Entries.Where(e => e.TeamNumber == tn));
                }
            }

            if (allEntries.Count > 0)
            {
                _cachedScoutingEntries = allEntries;
                System.Diagnostics.Debug.WriteLine($"Preloaded {_cachedScoutingEntries.Count} entries for event {eventId}");
                // regenerate ComparisonData based on current SelectedDataView
                if (SelectedDataView == "match_by_match")
                    GenerateMatchByMatchData(_cachedScoutingEntries);
                else
                    GenerateTeamAveragesData(_cachedScoutingEntries);

                // regenerate Plotly html so UI can switch modes without network
                try { await PreparePlotlyHtmlAsync(); UsePlotlyWebView = !string.IsNullOrEmpty(PlotlyHtml); }
                catch { }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PreloadComparisonDataAsync failed: {ex.Message}");
        }
    }

    private void GenerateTeamAveragesData(List<ScoutingEntry> entries)
    {
        System.Diagnostics.Debug.WriteLine("=== GENERATING TEAM AVERAGES DATA ===");
        
        // Group by team
        var teamGroups = entries.GroupBy(e => e.TeamNumber).OrderBy(g => g.Key);
        
        var teamDataList = new List<TeamComparisonData>();
        var datasetsBar = new List<GraphDataset>();
        var datasetsLine = new List<GraphDataset>();
        var datasetsRadar = new List<GraphDataset>();
        var labels = new List<string>();

        // Temp storage for radar values so we can scale consistency to the same numeric range as points
        var radarTemps = new List<(string Label, double Consistency, double Avg, double Max)>();
        
        int colorIndex = 0;
        foreach (var teamGroup in teamGroups)
        {
            var teamNumber = teamGroup.Key;
            var teamEntries = teamGroup.ToList();
            var teamName = teamEntries.FirstOrDefault()?.TeamName ?? $"Team {teamNumber}";
            
            System.Diagnostics.Debug.WriteLine($"Team {teamNumber}: {teamEntries.Count} matches");
            
            // Calculate metric values
            var values = teamEntries.Select(e => ExtractMetricValue(e.Data, SelectedMetric!.Id)).ToList();
            var avgValue = values.DefaultIfEmpty(0).Average();
            var maxValue = values.DefaultIfEmpty(0).Max();
            var stdDev = CalculateStdDev(values);

            // Estimate consistency from how consistent the team's total points are.
            // Use coefficient of variation (stdDev / mean) and convert to a 0..1 consistency score via 1/(1+cv).
            var totalPointsVals = teamEntries.Select(e => ExtractMetricValue(e.Data, "total_points")).ToList();
            var avgTotalPoints = totalPointsVals.DefaultIfEmpty(0).Average();
            var stdDevTotalPoints = CalculateStdDev(totalPointsVals);
            double consistencyAvg;
            if (avgTotalPoints <= 0 || double.IsNaN(stdDevTotalPoints))
            {
                consistencyAvg = 0;
            }
            else
            {
                var cv = stdDevTotalPoints / avgTotalPoints; // coefficient of variation
                consistencyAvg = 1.0 / (1.0 + cv); // maps lower cv to value near 1, higher cv to near 0
                if (double.IsNaN(consistencyAvg) || double.IsInfinity(consistencyAvg)) consistencyAvg = 0;
                // clamp to 0..1
                consistencyAvg = Math.Max(0, Math.Min(1, consistencyAvg));
            }
            
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
            
            // Add individual dataset for bar/line (single value)
            datasetsBar.Add(new GraphDataset
            {
                Label = $"{teamNumber} - {teamName}",
                Data = new List<double> { avgValue },
                BackgroundColor = color,
                BorderColor = color
            });

            datasetsLine.Add(new GraphDataset
            {
                Label = $"{teamNumber} - {teamName}",
                Data = new List<double> { avgValue },
                BackgroundColor = color,
                BorderColor = color
            });

            // Store radar components and build datasets after loop so consistency can be scaled
            radarTemps.Add(($"{teamNumber} - {teamName}", consistencyAvg, avgValue, maxValue));

            labels.Add(teamNumber.ToString());
            
            colorIndex++;
        }
        
        // Create bar/line/radar graph data
        var barGraphData = new GraphData
        {
            Type = "bar",
            Labels = labels,
            Datasets = datasetsBar
        };

        var lineGraphData = new GraphData
        {
            Type = "line",
            Labels = labels,
            Datasets = datasetsLine
        };

        // Determine numeric range for radar. Ensure at least 100 so rings are reasonable.
        var globalMaxPoints = radarTemps.Select(t => t.Max).DefaultIfEmpty(0).Max();
        var radarRange = Math.Max(100.0, globalMaxPoints);
        // Round radar max up to nearest 100 so rings are at nice round numbers
        var radarMaxRound = Math.Ceiling(radarRange / 100.0) * 100.0;
        _radarMaxValue = radarMaxRound;

        // Build radar datasets scaling consistency (0..1) into the same numeric range as points
        foreach (var rt in radarTemps)
        {
            var scaledConsistency = rt.Consistency * radarRange;
            datasetsRadar.Add(new GraphDataset
            {
                Label = rt.Label,
                Data = new List<double> { scaledConsistency, rt.Avg, rt.Max },
                BackgroundColor = TeamColors[ (datasetsRadar.Count) % TeamColors.Length ],
                BorderColor = TeamColors[ (datasetsRadar.Count) % TeamColors.Length ]
            });
        }

        var radarGraphData = new GraphData
        {
            Type = "radar",
            Labels = new List<string> { "Consistency", "Average Points", "Max Points" },
            Datasets = datasetsRadar
        };

        // Build scatter graph data - sort teams by value (lowest to highest)
        // Sort by value first, then create labels and datasets in that order
        var sortedTeamsForScatter = teamDataList.OrderBy(t => t.Value).ToList();
        var scatterLabels = sortedTeamsForScatter.Select(t => t.TeamNumber.ToString()).ToList();
        var scatterDatasets = new List<GraphDataset>();
        
        // Create datasets with team numbers sorted by their point values
        for (int i = 0; i < sortedTeamsForScatter.Count; i++)
        {
            var team = sortedTeamsForScatter[i];
            scatterDatasets.Add(new GraphDataset
            {
                Label = $"{team.TeamNumber} - {team.TeamName}",
                Data = new List<double> { team.Value },
                BorderColor = team.Color, // Use original team color
                BackgroundColor = team.Color
            });
        }

        var scatterGraphData = new GraphData
        {
            Type = "scatter",
            Labels = scatterLabels, // X-axis: team numbers sorted by points
            Datasets = scatterDatasets // Y-axis: points in ascending order
        };

        // Also build histogram and box plot versions for team averages
        var histogramGraphData = new GraphData
        {
            Type = "hist",
            Labels = labels,
            Datasets = datasetsBar.Select(ds => new GraphDataset
            {
                Label = ds.Label,
                Data = ds.Data,
                BorderColor = ds.BorderColor,
                BackgroundColor = ds.BackgroundColor
            }).ToList()
        };

        var boxPlotGraphData = new GraphData
        {
            Type = "box",
            Labels = labels,
            Datasets = datasetsBar.Select(ds => new GraphDataset
            {
                Label = ds.Label,
                Data = ds.Data,
                BorderColor = ds.BorderColor,
                BackgroundColor = ds.BackgroundColor
            }).ToList()
        };

        // Build violin graph data for averages: one violin per team using raw values from teamGroups
        var violinGraphData = new GraphData
        {
            Type = "violin",
            Labels = labels,
            Datasets = new List<GraphDataset>()
        };

        int viIndex = 0;
        foreach (var tg in teamGroups)
        {
            var vals = tg.Select(e => ExtractMetricValue(e.Data, SelectedMetric!.Id)).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
            if (vals.Count == 0) { viIndex++; continue; }
            violinGraphData.Datasets.Add(new GraphDataset
            {
                Label = labels.Count > viIndex ? labels[viIndex] : ($"Team {tg.Key}"),
                Data = vals,
                BorderColor = TeamColors[viIndex % TeamColors.Length],
                BackgroundColor = TeamColors[viIndex % TeamColors.Length]
            });
            viIndex++;
        }

        // Build sunburst graph data for hierarchical team performance visualization
        var sunburstGraphData = BuildSunburstData(teamDataList, teamGroups.ToList());
        // Build waterfall graph data: single dataset with team averages in order
        var waterfallValues = datasetsBar.Select(ds => ds.Data.FirstOrDefault()).ToList();
        var waterfallColors = labels.Select((l, idx) => TeamColors[idx % TeamColors.Length]).ToList();
        var waterfallDataset = new GraphDataset
        {
            Label = "Waterfall",
            Data = waterfallValues,
            BackgroundColor = string.Join(",", waterfallColors),
            BorderColor = string.Join(",", waterfallColors)
        };

        var waterfallGraphData = new GraphData
        {
            Type = "waterfall",
            Labels = labels,
            Datasets = new List<GraphDataset> { waterfallDataset }
        };

        // Create comparison response containing all graph type data
        ComparisonData = new CompareTeamsResponse
        {
            Success = true,
            Event = SelectedEvent,
            Metric = SelectedMetric!.Id,
            MetricDisplayName = SelectedMetric!.Name,
            DataView = SelectedDataView,
            Teams = teamDataList,
            Graphs = new Dictionary<string, GraphData> 
            { 
                { "bar", barGraphData }, 
                { "line", lineGraphData }, 
                { "radar", radarGraphData },
                { "scatter", scatterGraphData },
                { "hist", histogramGraphData },
                { "box", boxPlotGraphData }, 
                { "violin", violinGraphData },
                { "sunburst", sunburstGraphData },
                { "waterfall", waterfallGraphData }
            }
        };
        
        System.Diagnostics.Debug.WriteLine($"Created averages for {teamDataList.Count} teams with {datasetsBar.Count} datasets");
    }


    private double ExtractMetricValue(Dictionary<string, object> data, string metricId)
    {
        System.Diagnostics.Debug.WriteLine($"Extracting metric '{metricId}' from scouting data...");

        try
        {
            // Handle special calculated metrics
            if (metricId.ToLower() == "total_points" || metricId == "tot")
            {
                try { return CalculateTotalPoints(data); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CalculateTotalPoints failed: {ex.GetType().Name}: {ex.Message}");
                    return 0;
                }
            }

            if (metricId.ToLower() == "auto_points" || metricId == "apt")
            {
                try { return CalculateAutoPoints(data); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CalculateAutoPoints failed: {ex.GetType().Name}: {ex.Message}");
                    return 0;
                }
            }

            if (metricId.ToLower() == "teleop_points" || metricId == "tpt")
            {
                try { return CalculateTeleopPoints(data); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CalculateTeleopPoints failed: {ex.GetType().Name}: {ex.Message}");
                    return 0;
                }
            }

            if (metricId.ToLower() == "endgame_points" || metricId == "ept")
            {
                try { return CalculateEndgamePoints(data); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CalculateEndgamePoints failed: {ex.GetType().Name}: {ex.Message}");
                    return 0;
                }
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
            var lowerMetric = metricId.ToLower();
            foreach (var kvp in data)
            {
                if (kvp.Key != null && kvp.Key.ToLower().Contains(lowerMetric))
                {
                    var v = ConvertToDouble(kvp.Value);
                    if (v != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($" Fallback: found value for key '{kvp.Key}' = {v}");
                        return v;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($" Metric '{metricId}' not found, returning 0");
            return 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ExtractMetricValue error for '{metricId}': {ex.GetType().Name}: {ex.Message}");
            return 0;
        }
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

    private GraphData BuildSunburstData(List<TeamComparisonData> teamDataList, List<IGrouping<int, ScoutingEntry>> teamGroups)
    {
        // Sunburst shows hierarchical breakdown: Root -> Teams -> Performance Categories
        var sunburstData = new GraphData
        {
            Type = "sunburst",
            Labels = new List<string>(),
            Datasets = new List<GraphDataset>()
        };

        // Build hierarchical structure for Plotly sunburst
        var labels = new List<string> { "All Teams" }; // Root
        var parents = new List<string> { "" }; // Root has no parent
        var values = new List<double> { teamDataList.Sum(t => t.Value) }; // Total
        var colors = new List<string> { "#CCCCCC" };

        int colorIndex = 0;
        foreach (var team in teamDataList)
        {
            var teamLabel = $"Team {team.TeamNumber}";
            labels.Add(teamLabel);
            parents.Add("All Teams");
            values.Add(team.Value);
            colors.Add(TeamColors[colorIndex % TeamColors.Length]);

            // Add performance breakdown if we have game config
            if (_gameConfig != null)
            {
                var teamEntries = teamGroups.FirstOrDefault(g => g.Key == team.TeamNumber)?.ToList();
                if (teamEntries != null && teamEntries.Count > 0)
                {
                    // Calculate category averages
                    var autoAvg = teamEntries.Select(e => ExtractMetricValue(e.Data, "auto_points")).Average();
                    var teleopAvg = teamEntries.Select(e => ExtractMetricValue(e.Data, "teleop_points")).Average();
                    var endgameAvg = teamEntries.Select(e => ExtractMetricValue(e.Data, "endgame_points")).Average();

                    if (autoAvg > 0)
                    {
                        labels.Add($"{teamLabel} - Auto");
                        parents.Add(teamLabel);
                        values.Add(autoAvg);
                        colors.Add(AdjustColor(TeamColors[colorIndex % TeamColors.Length], 0.7));
                    }

                    if (teleopAvg > 0)
                    {
                        labels.Add($"{teamLabel} - Teleop");
                        parents.Add(teamLabel);
                        values.Add(teleopAvg);
                        colors.Add(AdjustColor(TeamColors[colorIndex % TeamColors.Length], 0.85));
                    }

                    if (endgameAvg > 0)
                    {
                        labels.Add($"{teamLabel} - Endgame");
                        parents.Add(teamLabel);
                        values.Add(endgameAvg);
                        colors.Add(AdjustColor(TeamColors[colorIndex % TeamColors.Length], 1.0));
                    }
                }
            }

            colorIndex++;
        }

        // Store sunburst data in custom format that Plotly understands
        var dataset = new GraphDataset
        {
            Label = "Sunburst",
            Data = values,
            BorderColor = string.Join(",", colors),
            BackgroundColor = string.Join(",", colors)
        };

        // Store labels and parents in the dataset's custom properties
        dataset.Tension = 0; // Unused, but repurpose to store data count
        sunburstData.Labels = labels;
        sunburstData.Datasets.Add(dataset);

        // Store parent structure in a second dataset
        var parentDataset = new GraphDataset
        {
            Label = "Parents",
            Data = new List<double>(), // Not used for sunburst
            BorderColor = string.Join(",", parents),
            BackgroundColor = ""
        };
        sunburstData.Datasets.Add(parentDataset);

        return sunburstData;
    }

    private string AdjustColor(string hexColor, double brightness)
    {
        // Parse hex color and adjust brightness
        if (!hexColor.StartsWith("#") || hexColor.Length != 7)
            return hexColor;

        try
        {
            var r = Convert.ToInt32(hexColor.Substring(1, 2), 16);
            var g = Convert.ToInt32(hexColor.Substring(3, 2), 16);
            var b = Convert.ToInt32(hexColor.Substring(5, 2), 16);

            r = (int)Math.Min(255, r * brightness);
            g = (int)Math.Min(255, g * brightness);
            b = (int)Math.Min(255, b * brightness);

            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch
        {
            return hexColor;
        }
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
            // Clear cached entries when event changes
            _cachedScoutingEntries = null;
            
            _ = LoadTeamsForEventAsync();
        }
    }

    partial void OnSelectedDataViewChanged(string value)
    {
        // Notify UI that visibility properties have changed
        OnPropertyChanged(nameof(ShowLineChart));
        OnPropertyChanged(nameof(ShowBarChart));
        OnPropertyChanged(nameof(ShowRadarChart));
        OnPropertyChanged(nameof(ShowScatterChart));
        OnPropertyChanged(nameof(ShowHistogramChart));
        OnPropertyChanged(nameof(ShowBoxPlotChart));
        OnPropertyChanged(nameof(ShowSunburstChart));
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

    // Note: ChangeGraphType removed - graph types are now selected via checkboxes
    // and multiple graphs are generated when Generate button is clicked
    
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

        // Try to resolve a GraphData to use for chart generation
        GraphData? graphData = null;

        // If user requested a line chart, prefer match-by-match data built from cached entries
        if (SelectedGraphType?.ToLower() == "line")
        {
            if (_cachedScoutingEntries != null && _cachedScoutingEntries.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("Selected graph is line - building match-by-match data from cached entries");
                var mm = BuildMatchByMatchGraphData(_cachedScoutingEntries);
                if (mm.GraphData != null && mm.GraphData.Datasets.Count > 0)
                {
                    graphData = mm.GraphData;
                    // Also update ComparisonData teams so UI summary shows match-based stats
                    ComparisonData = ComparisonData ?? new CompareTeamsResponse();
                    ComparisonData.Teams = mm.Teams;
                }
            }
        }

        // If we still don't have graphData, try to use server-processed graphs included in ComparisonData
        if (graphData == null && ComparisonData != null && ComparisonData.Graphs != null)
        {
            if (ComparisonData.Graphs.TryGetValue(SelectedGraphType.ToLower(), out var gd) && gd.Datasets.Count > 0)
            {
                graphData = gd;
            }
        }

        // If no server/processed graph found, fall back to generating from team averages
        if (graphData != null)
        {
            System.Diagnostics.Debug.WriteLine($"Using processed graph data for type {SelectedGraphType}");
            GenerateChartFromServerData(graphData);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"No processed graph data available for {SelectedGraphType} - using team averages fallback");
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
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
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
            // If no per-team charts were created (all values missing), fall back to team averages
            if (TeamCharts.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No per-team charts created (all values missing) - falling back to team averages chart");
                GenerateChartFromTeamAverages();
                return;
            }

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

            // Special handling for radar: if radar selected, generate per-team RadarChart showing consistency/avg/max
            if (SelectedGraphType.ToLower() == "radar")
            {
                System.Diagnostics.Debug.WriteLine("=== GENERATING RADAR CHARTS PER TEAM ===");
                TeamCharts.Clear();
                TeamChartsWithInfo.Clear();

                int radarIndex = 0;
                foreach (var ds in graphData.Datasets)
                {
                    // Expect ds.Data = [consistency, avg, max]
                    var color = TryParseColor(ds.BorderColor?.ToString()) ?? SKColor.Parse(TeamColors[radarIndex % TeamColors.Length]);
                    var label = ds.Label ?? ($"Team {radarIndex}");
                    var values = ds.Data;

                    var radarEntries = new List<ChartEntry>();
                    var categories = graphData.Labels ?? new List<string> { "Consistency", "Average Points", "Max Points" };
                    for (int i = 0; i < values.Count && i < categories.Count; i++)
                    {
                        var v = values[i];
                        radarEntries.Add(new ChartEntry((float)v)
                        {
                            Label = categories[i],
                            ValueLabel = v.ToString("F1"),
                            Color = color
                        });
                    }

                    if (radarEntries.Count > 0)
                    {
                        var radar = new RadarChart
                        {
                            Entries = radarEntries,
                            LabelTextSize = 20,
                            BackgroundColor = SKColors.Transparent,
                            LabelColor = SKColors.Gray,
                            IsAnimated = false,
                            BorderLineSize = 3,
                            PointSize = 10
                        };

                        TeamCharts.Add(radar);
                        TeamChartsWithInfo.Add(new TeamChartInfo
                        {
                            TeamNumber = ds.Label?.Split('-')[0].Trim() ?? radarIndex.ToString(),
                            TeamName = ds.Label?.Contains('-') == true ? ds.Label.Split('-')[1].Trim() : ds.Label,
                            Color = ds.BorderColor?.ToString() ?? TeamColors[radarIndex % TeamColors.Length],
                            Chart = radar
                        });
                    }

                    radarIndex++;
                }

                CurrentChart = TeamCharts.FirstOrDefault();
                OnPropertyChanged(nameof(CurrentChart));
                return;
            }

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
        System.Diagnostics.Debug.WriteLine($"⚠️ TEAM AVERAGES DEBUG:");
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

    // Generate Plotly HTML for all selected graph types
    private async Task PrepareAllPlotlyGraphsAsync()
    {
        plotlyHtmlGraphs.Clear();
        
        if (ComparisonData == null || ComparisonData.Graphs == null || ComparisonData.Graphs.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[PrepareAllPlotlyGraphsAsync] No comparison data available");
            return;
        }

        var selectedTypes = GetSelectedGraphTypes();
        System.Diagnostics.Debug.WriteLine($"[PrepareAllPlotlyGraphsAsync] Generating HTML for {selectedTypes.Count} graph types");

        foreach (var graphType in selectedTypes)
        {
            try
            {
                var html = await GeneratePlotlyHtmlForTypeAsync(graphType);
                if (!string.IsNullOrEmpty(html))
                {
                    var displayName = graphType.ToLower() switch
                    {
                        "line" => "Line Chart",
                        "bar" => "Bar Chart",
                        "radar" => "Radar Chart",
                        "scatter" => "Scatter Plot",
                        "waterfall" => "Waterfall",
                        "hist" => "Histogram",
                        "box" => "Box Plot",
                        "sunburst" => "Sunburst",
                        _ => graphType
                    };

                    plotlyHtmlGraphs.Add(new GraphHtmlInfo
                    {
                        GraphType = graphType,
                        DisplayName = displayName,
                        HtmlContent = html
                    });

                    System.Diagnostics.Debug.WriteLine($"[PrepareAllPlotlyGraphsAsync] Generated {displayName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrepareAllPlotlyGraphsAsync] Failed to generate {graphType}: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"[PrepareAllPlotlyGraphsAsync] Successfully generated {plotlyHtmlGraphs.Count} graphs");
    }

    // Generate Plotly HTML for a specific graph type
    private async Task<string?> GeneratePlotlyHtmlForTypeAsync(string graphType)
    {
        if (ComparisonData == null || ComparisonData.Graphs == null)
            return null;

        var key = graphType.ToLower();
        if (!ComparisonData.Graphs.TryGetValue(key, out var graphData))
        {
            System.Diagnostics.Debug.WriteLine($"[GeneratePlotlyHtmlForTypeAsync] Graph type '{key}' not found in ComparisonData");
            return null;
        }

        // Build Plotly traces (reuse logic from PreparePlotlyHtmlAsync but for specific graph type)
        var traces = new List<object>();
        
        System.Diagnostics.Debug.WriteLine($"[GeneratePlotlyHtmlForTypeAsync] Building {key} chart with {graphData.Datasets.Count} datasets");

        for (int dsIndex = 0; dsIndex < graphData.Datasets.Count; dsIndex++)
        {
            var ds = graphData.Datasets[dsIndex];
            string? colorStr = null;
            if (ds.BorderColor is string s) colorStr = s;
            else if (ds.BorderColor is IEnumerable<object> arr)
            {
                var first = arr.FirstOrDefault();
                if (first != null) colorStr = first.ToString();
            }

            if (key == "radar")
            {
                var rValues = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (rValues.Count == 0) continue;

                var theta = graphData.Labels;
                if (theta == null || theta.Count != rValues.Count)
                {
                    theta = Enumerable.Range(1, rValues.Count).Select(i => i.ToString()).ToList();
                }

                var tracePolar = new Dictionary<string, object>
                {
                    ["type"] = "scatterpolar",
                    ["r"] = rValues,
                    ["theta"] = theta,
                    ["name"] = ds.Label ?? string.Empty,
                    ["fill"] = "toself"
                };
                if (!string.IsNullOrEmpty(colorStr))
                {
                    tracePolar["marker"] = new Dictionary<string, object> { ["color"] = colorStr };
                    tracePolar["line"] = new Dictionary<string, object> { ["color"] = colorStr };
                }
                traces.Add(tracePolar);
                continue;
            }

            if (key == "violin")
            {
                var samples = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (samples.Count == 0) continue;

                var traceViolin = new Dictionary<string, object>
                {
                    ["type"] = "violin",
                    ["y"] = samples,
                    ["name"] = ds.Label ?? string.Empty,
                    ["box"] = new { visible = true },
                    ["meanline"] = new { visible = true }
                };
                if (!string.IsNullOrEmpty(colorStr))
                {
                    traceViolin["marker"] = new Dictionary<string, object> { ["color"] = colorStr };
                    traceViolin["line"] = new Dictionary<string, object> { ["color"] = colorStr };
                    traceViolin["fillcolor"] = colorStr;
                }
                traces.Add(traceViolin);
                continue;
            }

            if (key == "violin")
            {
                // Each dataset.Data is a list of numeric samples for the violin
                var samples = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (samples.Count == 0) continue;

                var traceViolin = new Dictionary<string, object>
                {
                    ["type"] = "violin",
                    ["y"] = samples,
                    ["name"] = ds.Label ?? string.Empty,
                    ["box"] = new { visible = true },
                    ["meanline"] = new { visible = true }
                };
                if (!string.IsNullOrEmpty(colorStr))
                {
                    traceViolin["marker"] = new Dictionary<string, object> { ["color"] = colorStr };
                    traceViolin["line"] = new Dictionary<string, object> { ["color"] = colorStr };
                    traceViolin["fillcolor"] = colorStr;
                }
                traces.Add(traceViolin);
                continue;
            }

            if (key == "sunburst")
            {
                // Sunburst uses special hierarchical format
                var labels = graphData.Labels;
                var values = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                var colors = ds.BackgroundColor?.ToString()?.Split(',') ?? new string[0];
                var parents = graphData.Datasets.Count > 1 ? 
                    (graphData.Datasets[1].BorderColor?.ToString()?.Split(',') ?? new string[0]) : 
                    new string[0];

                if (values.Count > 0 && labels != null && labels.Count == values.Count)
                {
                    var traceSunburst = new Dictionary<string, object>
                    {
                        ["type"] = "sunburst",
                        ["ids"] = labels,
                        ["labels"] = labels,
                        ["parents"] = parents.Length == labels.Count ? parents : Enumerable.Repeat("", labels.Count).ToArray(),
                        ["values"] = values,
                        ["branchvalues"] = "total",
                        ["hovertemplate"] = "<b>%{label}</b><br>Points: %{value:.2f}<extra></extra>",
                        ["marker"] = new Dictionary<string, object>
                        {
                            ["colors"] = colors.Length == labels.Count ? colors : Enumerable.Repeat("#CCCCCC", labels.Count).ToArray(),
                            ["line"] = new Dictionary<string, object> { ["color"] = "rgb(30,30,32)", ["width"] = 1 }
                        }
                    };

                    traces.Add(traceSunburst);
                }
                continue;
            }

            var trace = new Dictionary<string, object> { ["name"] = ds.Label ?? string.Empty };
            
            // Special handling for scatter in averages mode  
            if (key == "scatter" && SelectedDataView == "averages")
            {
                // For scatter in averages, use x as team numbers (from labels) and y as values
                var filteredData = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (filteredData.Count == 0) continue;
                
                // Get corresponding x label (team number)
                var teamLabel = dsIndex < (graphData.Labels?.Count ?? 0) ? graphData.Labels[dsIndex] : dsIndex.ToString();
                trace["x"] = new List<string> { teamLabel };
                trace["y"] = filteredData;
                trace["type"] = "scatter";
                trace["mode"] = "markers";
                
                if (!string.IsNullOrEmpty(colorStr))
                {
                    trace["marker"] = new Dictionary<string, object> 
                    { 
                        ["color"] = colorStr,
                        ["size"] = 12
                    };
                }
                
                traces.Add(trace);
                continue;
            }
            
            if (SelectedDataView == "match_by_match")
            {
                var xVals = new List<string>();
                var yVals = new List<double>();
                for (int i = 0; i < ds.Data.Count && i < (graphData.Labels?.Count ?? 0); i++)
                {
                    var v = ds.Data[i];
                    if (!double.IsNaN(v) && !double.IsInfinity(v))
                    {
                        xVals.Add(graphData.Labels![i]);
                        yVals.Add(v);
                    }
                }
                if (xVals.Count > 0)
                {
                    trace["x"] = xVals;
                    trace["y"] = yVals;
                }
                else continue;
            }
            else
            {
                var filteredData = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (filteredData.Count == 0) continue;
                // For averages mode, prefer explicit x labels so Plotly shows team labels on the x-axis
                if (graphData.Labels != null && graphData.Labels.Count > 0)
                {
                    // If dataset aligns with labels (equal length), use full label list
                    if (filteredData.Count == graphData.Labels.Count)
                    {
                        trace["x"] = graphData.Labels;
                        trace["y"] = filteredData;
                    }
                    else
                    {
                        // Otherwise map this dataset to its corresponding label by index
                        var teamLabel = dsIndex < graphData.Labels.Count ? graphData.Labels[dsIndex] : dsIndex.ToString();
                        trace["x"] = new List<string> { teamLabel };
                        trace["y"] = filteredData;
                    }
                }
                else
                {
                    trace["y"] = filteredData;
                }
            }
            
            if (key == "bar") 
            {
                trace["type"] = "bar";
            }
            else if (key == "waterfall")
            {
                // Waterfall expects x (labels) and y (values) with measure types
                var vals = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (vals.Count == 0) continue;
                var x = graphData.Labels ?? Enumerable.Range(1, vals.Count).Select(i => i.ToString()).ToList();
                // Build waterfall measures: first is 'relative' items and final is 'total'
                var measures = new List<string>();
                for (int i = 0; i < vals.Count; i++) measures.Add("relative");
                // last mark as total
                if (measures.Count > 0) measures[measures.Count - 1] = "total";

                trace["type"] = "waterfall";
                trace["x"] = x;
                trace["y"] = vals;
                trace["measure"] = measures;
                trace["connector"] = new Dictionary<string, object> { ["line"] = new { color = "rgb(68,68,68)", width = 2 } };
                trace["marker"] = new Dictionary<string, object> { ["color"] = ds.BackgroundColor?.ToString()?.Split(',') ?? new string[0] };
            }
            else if (key == "scatter") 
            { 
                trace["type"] = "scatter"; 
                trace["mode"] = "markers";
                trace["marker"] = new Dictionary<string, object> { ["size"] = 10 };
            }
            else if (key == "hist") 
            {
                trace["type"] = "histogram";
                trace["nbinsx"] = 10; // Number of bins
            }
            else if (key == "box") 
            {
                trace["type"] = "box";
                trace["boxmean"] = "sd"; // Show mean and standard deviation
            }
            else 
            { 
                trace["type"] = "scatter"; 
                trace["mode"] = key == "line" ? "lines+markers" : "markers"; 
            }
            
            if (!string.IsNullOrEmpty(colorStr))
            {
                trace["marker"] = new Dictionary<string, object> { ["color"] = colorStr };
                trace["line"] = new Dictionary<string, object> { ["color"] = colorStr };
            }
            traces.Add(trace);
        }

        if (traces.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine($"[GeneratePlotlyHtmlForTypeAsync] No traces generated for {key}");
            return null;
        }

        // If radar is used, prepare polar radial axis ticks
        object? polarObj = null;
        if (key == "radar")
        {
            try
            {
                var targetTicks = 8;
                var rawStep = Math.Ceiling(_radarMaxValue / (double)targetTicks);
                double step = Math.Max(25.0, Math.Ceiling(rawStep / 25.0) * 25.0);
                polarObj = new { radialaxis = new { visible = true, tickangle = 0, range = new double[] { 0.0, _radarMaxValue }, tickmode = "linear", dtick = step } };
            }
            catch { polarObj = new { radialaxis = new { visible = true, tickangle = 0 } }; }
        }

        // Build xaxis with explicit category ticks when using averages so team labels are shown
        object xaxisObj;
        if (SelectedDataView != "match_by_match" && graphData.Labels != null && graphData.Labels.Count > 0)
        {
            xaxisObj = new
            {
                title = SelectedDataView == "match_by_match" ? "Match" : "Team",
                automargin = true,
                type = "category",
                tickmode = "array",
                tickvals = graphData.Labels,
                ticktext = graphData.Labels
            };
        }
        else
        {
            xaxisObj = new { title = SelectedDataView == "match_by_match" ? "Match" : "Team", automargin = true, type = SelectedDataView == "match_by_match" ? "linear" : "category" };
        }

        var layout = new Dictionary<string, object>
        {
            ["title"] = new { text = $"{ComparisonData.MetricDisplayName ?? ComparisonData.Metric} - {graphType.ToUpper()}", font = new { size = 18 } },
            ["xaxis"] = xaxisObj,
            ["yaxis"] = new { title = ComparisonData.MetricDisplayName ?? ComparisonData.Metric, automargin = true },
            ["margin"] = new { l = 60, r = 30, t = 60, b = 60 },
            ["autosize"] = true,
            ["showlegend"] = true,
            ["legend"] = new { x = 1, y = 1, xanchor = "right" },
            // Dark background to match SVG style
            ["paper_bgcolor"] = "rgb(45, 51, 56)",
            ["plot_bgcolor"] = "rgb(45, 51, 56)",
            ["font"] = new { color = "rgb(230,230,230)" }
        };

        if (polarObj != null)
        {
            layout["polar"] = polarObj;
        }

        // If this is a sunburst, apply dark theme and title/hover tweaks to match example
        if (key == "sunburst")
        {
            try
            {
                layout["plot_bgcolor"] = "#0f1720";
                layout["paper_bgcolor"] = "#0f1720";
                layout["font"] = new { color = "#e6eef8" };
                layout["hoverlabel"] = new { align = "left" };
                layout["title"] = new { text = "Team Points Performance Hierarchy (Sunburst)", font = new { size = 17 } };
                layout["margin"] = new { l = 40, r = 20, t = 50, b = 60 };
                // Provide a pleasant colorway fallback if not supplied by dataset
                layout["colorway"] = new[] { "#636efa","#EF553B","#00cc96","#ab63fa","#FFA15A","#19d3f3","#FF6692","#B6E880","#FF97FF","#FECB52" };
            }
            catch { }
        }

        var payload = new Dictionary<string, object>
        {
            ["data"] = traces,
            ["layout"] = layout
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        try
        {
            var dataJson = JsonSerializer.Serialize(payload, jsonOptions);

            // Try to load bundled Plotly JS
            string? plotlyJs = null;
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("plotly-latest.min.js");
                using var sr = new StreamReader(stream);
                plotlyJs = await sr.ReadToEndAsync();
            }
            catch { plotlyJs = null; }

            string html;
            if (!string.IsNullOrEmpty(plotlyJs) && DeviceInfo.Platform == DevicePlatform.Android)
            {
                // Android: inline reference to bundled asset
                html = $"<!doctype html>\n<html>\n  <head>\n    <meta charset=\"utf-8\" />\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n    <script src=\"plotly-latest.min.js\"></script>\n    <style>body {{ margin: 0; padding: 0; height: 100vh; }} #plot {{ width: 100%; height: 100%; }}</style>\n  </head>\n  <body>\n    <div id=\"plot\"></div>\n    <script>const payload = {dataJson}; try {{ Plotly.newPlot('plot', payload.data, payload.layout, {{responsive:true, displayModeBar:true}}); }} catch(e) {{ console.error('Plotly error:', e); document.getElementById('plot').innerHTML = '<div style=\"padding:20px;color:red;\">Error: ' + e.message + '</div>'; }}</script>\n  </body>\n</html>\n";
            }
            else if (!string.IsNullOrEmpty(plotlyJs) && DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                // Windows: write files to AppData
                var appDir = Path.Combine(FileSystem.AppDataDirectory, "plotly_bundle");
                if (!Directory.Exists(appDir)) Directory.CreateDirectory(appDir);
                var jsPath = Path.Combine(appDir, "plotly-latest.min.js");
                await File.WriteAllTextAsync(jsPath, plotlyJs);
                html = $"<!doctype html>\n<html>\n  <head>\n    <meta charset=\"utf-8\" />\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n    <script src=\"plotly-latest.min.js\"></script>\n    <style>body {{ margin: 0; padding: 0; height: 100vh; }} #plot {{ width: 100%; height: 100%; }}</style>\n  </head>\n  <body>\n    <div id=\"plot\"></div>\n    <script>const payload = {dataJson}; try {{ Plotly.newPlot('plot', payload.data, payload.layout, {{responsive:true, displayModeBar:true}}); }} catch(e) {{ console.error('Plotly error:', e); document.getElementById('plot').innerHTML = '<div style=\"padding:20px;color:red;\">Error: ' + e.message + '</div>'; }}</script>\n  </body>\n</html>\n";
                var htmlPath = Path.Combine(appDir, $"plot_{graphType}.html");
                await File.WriteAllTextAsync(htmlPath, html);
                return new Uri(htmlPath).AbsoluteUri;
            }
            else
            {
                // Fallback: CDN
                html = $"<!doctype html>\n<html>\n  <head>\n    <meta charset=\"utf-8\" />\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n    <script src=\"https://cdn.plot.ly/plotly-latest.min.js\"></script>\n    <style>body {{ margin: 0; padding: 0; height: 100vh; }} #plot {{ width: 100%; height: 100%; }}</style>\n  </head>\n  <body>\n    <div id=\"plot\"></div>\n    <script>const payload = {dataJson}; try {{ Plotly.newPlot('plot', payload.data, payload.layout, {{responsive:true, displayModeBar:true}}); }} catch(e) {{ console.error('Plotly error:', e); document.getElementById('plot').innerHTML = '<div style=\"padding:20px;color:red;\">Error: ' + e.message + '</div>'; }}</script>\n  </body>\n</html>\n";
            }

            return html;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeneratePlotlyHtmlForTypeAsync] ERROR: {ex.Message}");
            return null;
        }
    }

    private void PreparePlotlyHtml()
    {
        if (ComparisonData == null || ComparisonData.Graphs == null || ComparisonData.Graphs.Count == 0)
        {
            PlotlyHtml = null;
            UsePlotlyWebView = false;
            return;
        }

        // Choose graph type available in Graphs dictionary matching SelectedGraphType
        var key = SelectedGraphType.ToLower();
        if (!ComparisonData.Graphs.TryGetValue(key, out var graphData))
        {
            // fallback to first graph
            graphData = ComparisonData.Graphs.Values.FirstOrDefault();
        }

        if (graphData == null)
        {
            PlotlyHtml = null;
            UsePlotlyWebView = false;
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtml] Building {key} chart with {graphData.Datasets.Count} datasets");

        // Build Plotly traces (limit number when many datasets to avoid clutter)
        var traces = new List<object>();
        var maxTraces = int.MaxValue; // unlimited traces
        var totalDatasets = graphData.Datasets.Count;
        var stepIndex = 1;
        if (totalDatasets > maxTraces) stepIndex = (int)Math.Ceiling(totalDatasets / (double)maxTraces);

        for (int dsIndex = 0; dsIndex < graphData.Datasets.Count; dsIndex++)
        {
            // sample datasets when too many
            if (totalDatasets > maxTraces && (dsIndex % stepIndex) != 0) continue;
            var ds = graphData.Datasets[dsIndex];
            // normalize color
            string? colorStr = null;
            if (ds.BorderColor is string s) colorStr = s;
            else if (ds.BorderColor is IEnumerable<object> arr)
            {
                var first = arr.FirstOrDefault();
                if (first != null) colorStr = first.ToString();
            }

            // If radar chart requested, use scatterpolar traces with r/theta
            if (key == "radar")
            {
                // Filter out NaN values from radar data
                var rValues = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (rValues.Count == 0) continue; // Skip empty datasets

                var theta = graphData.Labels;
                if (theta == null || theta.Count != rValues.Count)
                {
                    // build numeric theta like ["1","2",...]
                    theta = Enumerable.Range(1, rValues.Count).Select(i => i.ToString()).ToList();
                }

                var tracePolar = new Dictionary<string, object>
                {
                    ["type"] = "scatterpolar",
                    ["r"] = rValues,
                    ["theta"] = theta,
                    ["name"] = ds.Label ?? string.Empty,
                    ["fill"] = "toself"
                };

                if (!string.IsNullOrEmpty(colorStr))
                {
                    tracePolar["marker"] = new Dictionary<string, object> { ["color"] = colorStr };
                    tracePolar["line"] = new Dictionary<string, object> { ["color"] = colorStr };
                }

                System.Diagnostics.Debug.WriteLine($"  Radar trace: {ds.Label}, {rValues.Count} points");
                traces.Add(tracePolar);
                continue;
            }

            // Default: line / scatter / bar with x (labels) when match-by-match
            var trace = new Dictionary<string, object>();
            trace["name"] = ds.Label ?? string.Empty;

            // Special handling for scatter in averages mode
            if (key == "scatter" && SelectedDataView == "averages")
            {
                // For scatter in averages, use x as team numbers (from labels) and y as values
                var filteredData = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (filteredData.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"  Skipping scatter trace {ds.Label} - no valid data");
                    continue;
                }
                
                // Get corresponding x label (team number)
                var teamLabel = dsIndex < (graphData.Labels?.Count ?? 0) ? graphData.Labels[dsIndex] : dsIndex.ToString();
                trace["x"] = new List<string> { teamLabel };
                trace["y"] = filteredData;
                trace["type"] = "scatter";
                trace["mode"] = "markers";
                
                if (!string.IsNullOrEmpty(colorStr))
                {
                    trace["marker"] = new Dictionary<string, object> 
                    { 
                        ["color"] = colorStr,
                        ["size"] = 12
                    };
                }
                
                System.Diagnostics.Debug.WriteLine($"  Scatter trace (averages): {ds.Label}, x={teamLabel}, y={filteredData[0]}");
                traces.Add(trace);
                continue;
            }

            // if match-by-match, provide x values from labels
            if (SelectedDataView == "match_by_match")
            {
                // ALWAYS filter out NaN values to avoid JSON serialization errors
                var xVals = new List<string>();
                var yVals = new List<double>();
                
                for (int i = 0; i < ds.Data.Count && i < (graphData.Labels?.Count ?? 0); i++)
                {
                    var v = ds.Data[i];
                    if (!double.IsNaN(v) && !double.IsInfinity(v))
                    {
                        xVals.Add(graphData.Labels![i]);
                        yVals.Add(v);
                    }
                }
                
                // Only add trace if we have valid points
                if (xVals.Count > 0)
                {
                    trace["x"] = xVals;
                    trace["y"] = yVals;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  Skipping trace {ds.Label} - no valid data");
                    continue; // Skip empty traces
                }
            }
            else
            {
                // for averages and other modes, filter out NaN values as well
                var filteredData = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (filteredData.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"  Skipping trace {ds.Label} - no valid data");
                    continue; // Skip empty traces
                }
                trace["y"] = filteredData;
            }

            if (key == "bar")
            {
                trace["type"] = "bar";
            }
            else
            {
                trace["type"] = "scatter";
                trace["mode"] = key == "line" ? "lines+markers" : "markers";
            }

            if (!string.IsNullOrEmpty(colorStr))
            {
                trace["marker"] = new Dictionary<string, object> { ["color"] = colorStr };
                trace["line"] = new Dictionary<string, object> { ["color"] = colorStr };
            }

            System.Diagnostics.Debug.WriteLine($"  {key} trace: {ds.Label}, {(trace.ContainsKey("y") ? ((List<double>)trace["y"]).Count : 0)} points");
            traces.Add(trace);
        }

        // If sampling or filtering removed all traces, fall back to a simpler trace set so Plotly shows something.
        if (traces.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[PreparePlotlyHtml] No traces after sampling/filtering - building fallback traces");
            foreach (var ds in graphData.Datasets)
            {
                // attempt to build a full trace for any dataset with valid numeric data
                var filtered = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (filtered.Count == 0) continue;

                var trace = new Dictionary<string, object> { ["name"] = ds.Label ?? string.Empty };
                if (SelectedDataView == "match_by_match")
                {
                    // Build matching x values for filtered y values
                    var xVals = new List<string>();
                    var yVals = new List<double>();
                    for (int i = 0; i < ds.Data.Count && i < (graphData.Labels?.Count ?? 0); i++)
                    {
                        var v = ds.Data[i];
                        if (!double.IsNaN(v) && !double.IsInfinity(v))
                        {
                            xVals.Add(graphData.Labels![i]);
                            yVals.Add(v);
                        }
                    }
                    if (xVals.Count > 0)
                    {
                        trace["x"] = xVals;
                        trace["y"] = yVals;
                    }
                    else continue;
                }
                else
                {
                    trace["y"] = filtered;
                }
                trace["type"] = "scatter";
                trace["mode"] = "lines+markers";
                System.Diagnostics.Debug.WriteLine($"  Fallback trace: {ds.Label}, {filtered.Count} points");
                traces.Add(trace);
                if (traces.Count >= 3) break; // keep fallback small
            }

            // If still empty, build a single averages trace from ComparisonData.Teams
            if (traces.Count == 0 && ComparisonData?.Teams != null && ComparisonData.Teams.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("  Building averages fallback from Teams data");
                var avgTrace = new Dictionary<string, object>
                {
                    ["name"] = "Team Averages",
                    ["x"] = ComparisonData.Teams.Select(t => t.TeamNumber.ToString()).ToList(),
                    ["y"] = ComparisonData.Teams.Select(t => t.Value).ToList(),
                    ["type"] = "scatter",
                    ["mode"] = "lines+markers"
                };
                traces.Add(avgTrace);
            }
        }

        if (traces.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[PreparePlotlyHtml] ERROR: No traces generated, cannot create chart");
            PlotlyHtml = null;
            UsePlotlyWebView = false;
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtml] Generated {traces.Count} traces");

            // Prepare polar radial axis ticks using _radarMaxValue (ensures consistency scaled into same range)
            var polarObj = (object?)null;
            if (key == "radar")
            {
                try
                {
                    // Choose tick step dynamically to avoid clutter. Target max ~8 ticks, round step to nearest 25.
                    var targetTicks = 8;
                    var rawStep = Math.Ceiling(_radarMaxValue / (double)targetTicks);
                    // round up to nearest 25 for nicer labels
                    double step = Math.Max(25.0, Math.Ceiling(rawStep / 25.0) * 25.0);
                    // Use linear tick mode with dtick to control both ticks and gridlines
                    polarObj = new { radialaxis = new { visible = true, tickangle = 0, range = new double[] { 0.0, _radarMaxValue }, tickmode = "linear", dtick = step } };
                }
                catch
                {
                    polarObj = new { radialaxis = new { visible = true, tickangle = 0 } };
                }
            }

            var layout = new Dictionary<string, object>
            {
                ["title"] = new { text = ComparisonData.MetricDisplayName ?? ComparisonData.Metric, font = new { size = 18 } },
                ["xaxis"] = new { title = SelectedDataView == "match_by_match" ? "Match" : "Team", automargin = true },
                ["yaxis"] = new { title = ComparisonData.MetricDisplayName ?? ComparisonData.Metric, automargin = true },
                ["margin"] = new { l = 60, r = 30, t = 60, b = 60 },
                ["autosize"] = true,
                ["showlegend"] = true,
                ["legend"] = new { x = 1, y = 1, xanchor = "right" }
            };

            if (polarObj != null)
            {
                layout["polar"] = polarObj;
            }

        var payload = new Dictionary<string, object>
        {
            ["data"] = traces,
            ["layout"] = layout
        };

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            var dataJson = JsonSerializer.Serialize(payload, jsonOptions);
            System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtml] JSON payload length: {dataJson.Length}");

            // Minimal HTML that loads Plotly from CDN and renders the JSON payload
            var html =
                "<!doctype html>\n" +
                "<html>\n" +
                "  <head>\n" +
                "    <meta charset=\"utf-8\" />\n" +
                "    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n" +
                "    <script src=\"https://cdn.plot.ly/plotly-latest.min.js\"></script>\n" +
                "    <style>body { margin: 0; padding: 0; height: 100vh; } #plot { width: 100%; height: 100%; }</style>\n" +
                "  </head>\n" +
                "  <body>\n" +
                "    <div id=\"plot\"></div>\n" +
                "    <script>\n" +
                "      const payload = " + dataJson + ";\n" +
                "      console.log('Plotly payload:', payload);\n" +
                "      try {\n" +
                "        Plotly.newPlot('plot', payload.data, payload.layout, {responsive: true, displayModeBar: true});\n" +
                "        console.log('Plotly chart created successfully');\n" +
                "      } catch(e) {\n" +
                "        console.error('Plotly error:', e);\n" +
                "        document.getElementById('plot').innerHTML = '<div style=\"padding:20px;color:red;\">Error creating chart: ' + e.message + '</div>';\n" +
                "      }\n" +
                "    </script>\n" +
                "  </body>\n" +
                "</html>\n";

            PlotlyHtml = html;
            UsePlotlyWebView = true;
            System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtml] HTML generated successfully ({html.Length} chars)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtml] ERROR: {ex.Message}");
            PlotlyHtml = null;
            UsePlotlyWebView = false;
        }
    }

    private async Task PreparePlotlyHtmlAsync()
    {
        // Build payload as before
        if (ComparisonData == null || ComparisonData.Graphs == null || ComparisonData.Graphs.Count == 0)
        {
            PlotlyHtml = null;
            UsePlotlyWebView = false;
            return;
        }

        var key = SelectedGraphType.ToLower();
        if (!ComparisonData.Graphs.TryGetValue(key, out var graphData))
            graphData = ComparisonData.Graphs.Values.FirstOrDefault();
        if (graphData == null)
        {
            PlotlyHtml = null;
            UsePlotlyWebView = false;
            return;
        }

        // Reuse PreparePlotlyHtml to get traces/layout JSON
        // We'll reconstruct payload same way as PreparePlotlyHtml but inline JS
        var traces = new List<object>();
        var maxTraces = int.MaxValue;
        var totalDatasets = graphData.Datasets.Count;
        var stepIndex = 1;
        if (totalDatasets > maxTraces) stepIndex = (int)Math.Ceiling(totalDatasets / (double)maxTraces);

        System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtmlAsync] Building {key} chart with {graphData.Datasets.Count} datasets");

        for (int dsIndex = 0; dsIndex < graphData.Datasets.Count; dsIndex++)
        {
            if (totalDatasets > maxTraces && (dsIndex % stepIndex) != 0) continue;
            var ds = graphData.Datasets[dsIndex];
            string? colorStr = null;
            if (ds.BorderColor is string s) colorStr = s;
            else if (ds.BorderColor is IEnumerable<object> arr)
            {
                var first = arr.FirstOrDefault();
                if (first != null) colorStr = first.ToString();
            }

            if (key == "radar")
            {
                var rValues = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (rValues.Count == 0) continue;

                var theta = graphData.Labels;
                if (theta == null || theta.Count != rValues.Count)
                {
                    theta = Enumerable.Range(1, rValues.Count).Select(i => i.ToString()).ToList();
                }

                var tracePolar = new Dictionary<string, object>
                {
                    ["type"] = "scatterpolar",
                    ["r"] = rValues,
                    ["theta"] = theta,
                    ["name"] = ds.Label ?? string.Empty,
                    ["fill"] = "toself"
                };
                if (!string.IsNullOrEmpty(colorStr))
                {
                    tracePolar["marker"] = new Dictionary<string, object> { ["color"] = colorStr };
                    tracePolar["line"] = new Dictionary<string, object> { ["color"] = colorStr };
                }
                traces.Add(tracePolar);
                continue;
            }

            var trace = new Dictionary<string, object> { ["name"] = ds.Label ?? string.Empty };
            if (SelectedDataView == "match_by_match")
            {
                var xVals = new List<string>();
                var yVals = new List<double>();
                for (int i = 0; i < ds.Data.Count && i < (graphData.Labels?.Count ?? 0); i++)
                {
                    var v = ds.Data[i];
                    if (!double.IsNaN(v) && !double.IsInfinity(v))
                    {
                        xVals.Add(graphData.Labels![i]);
                        yVals.Add(v);
                    }
                }
                if (xVals.Count > 0)
                {
                    trace["x"] = xVals;
                    trace["y"] = yVals;
                }
                else continue;
            }
            else
            {
                var filteredData = ds.Data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (filteredData.Count == 0) continue;
                trace["y"] = filteredData;
            }
            if (key == "bar") trace["type"] = "bar";
            else { trace["type"] = "scatter"; trace["mode"] = key == "line" ? "lines+markers" : "markers"; }
            if (!string.IsNullOrEmpty(colorStr)) { trace["marker"] = new Dictionary<string, object> { ["color"] = colorStr }; trace["line"] = new Dictionary<string, object> { ["color"] = colorStr }; }
            traces.Add(trace);
        }

        if (traces.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[PreparePlotlyHtmlAsync] No traces generated - aborting");
            PlotlyHtml = null;
            UsePlotlyWebView = false;
            return;
        }

        // If radar is used, prepare polar radial axis ticks
        object? polarObj2 = null;
        if (key == "radar")
        {
            try
            {
                var targetTicks = 8;
                var rawStep = Math.Ceiling(_radarMaxValue / (double)targetTicks);
                double step = Math.Max(25.0, Math.Ceiling(rawStep / 25.0) * 25.0);
                polarObj2 = new { radialaxis = new { visible = true, tickangle = 0, range = new double[] { 0.0, _radarMaxValue }, tickmode = "linear", dtick = step } };
            }
            catch { polarObj2 = new { radialaxis = new { visible = true, tickangle = 0 } }; }
        }

        var layout = new Dictionary<string, object>
        {
            ["title"] = new { text = ComparisonData.MetricDisplayName ?? ComparisonData.Metric, font = new { size = 18 } },
            ["xaxis"] = new { title = SelectedDataView == "match_by_match" ? "Match" : "Team", automargin = true },
            ["yaxis"] = new { title = ComparisonData.MetricDisplayName ?? ComparisonData.Metric, automargin = true },
            ["margin"] = new { l = 60, r = 30, t = 60, b = 60 },
            ["autosize"] = true,
            ["showlegend"] = true,
            ["legend"] = new { x = 1, y = 1, xanchor = "right" }
        };

        if (polarObj2 != null)
        {
            layout["polar"] = polarObj2;
        }

        var payload = new Dictionary<string, object>
        {
            ["data"] = traces,
            ["layout"] = layout
        };

        var jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        
        try
        {
            var dataJson = JsonSerializer.Serialize(payload, jsonOptions);
            System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtmlAsync] JSON payload length: {dataJson.Length}");

            // Attempt to read bundled plotly JS from Resources/Raw/plotly-latest.min.js
            string? plotlyJs = null;
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("plotly-latest.min.js");
                using var sr = new StreamReader(stream);
                plotlyJs = await sr.ReadToEndAsync();
                System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtmlAsync] Loaded local Plotly JS ({plotlyJs.Length} chars)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtmlAsync] Local plotly not found: {ex.Message}");
                plotlyJs = null;
            }

            string html;

            if (!string.IsNullOrEmpty(plotlyJs))
            {
                try
                {
                    // On Android inline JS (file:// blocked). On other platforms write files to cache and load via file:// URI.
                    if (DeviceInfo.Platform == DevicePlatform.Android)
                    {
                        // Use a small HTML that references the bundled asset by relative path and rely on WebView BaseUrl to resolve it
                        html = "<!doctype html>\n<html>\n  <head>\n    <meta charset=\"utf-8\" />\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n    <script src=\"plotly-latest.min.js\"></script>\n    <style>body { margin: 0; padding: 0; height: 100vh; } #plot { width: 100%; height: 100%; }</style>\n  </head>\n  <body>\n    <div id=\"plot\"></div>\n    <script>const payload = " + dataJson + "; console.log('Plotly data:', payload); try { Plotly.newPlot('plot', payload.data, payload.layout, {responsive:true, displayModeBar:true}); console.log('Chart created'); } catch(e) { console.error('Plotly error:', e); document.getElementById('plot').innerHTML = '<div style=\"padding:20px;color:red;\">Error: ' + e.message + '</div>'; }</script>\n  </body>\n</html>\n";

                        PlotlyHtml = html;
                        UsePlotlyWebView = true;
                        System.Diagnostics.Debug.WriteLine("[PreparePlotlyHtmlAsync] Android HTML generated with bundled JS");
                        return;
                    }

                // Non-Android: write files and choose platform-appropriate URI
                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    // Write files to AppDataDirectory and load via ms-appdata:///local/ which WebView2 supports
                    var appDir = Path.Combine(FileSystem.AppDataDirectory, "plotly_bundle");
                    if (!Directory.Exists(appDir)) Directory.CreateDirectory(appDir);

                    var jsPath = Path.Combine(appDir, "plotly-latest.min.js");
                    await File.WriteAllTextAsync(jsPath, plotlyJs);

                    html = "<!doctype html>\n<html>\n  <head>\n    <meta charset=\"utf-8\" />\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n    <script src=\"plotly-latest.min.js\"></script>\n    <style>body { margin: 0; padding: 0; height: 100vh; } #plot { width: 100%; height: 100%; }</style>\n  </head>\n  <body>\n    <div id=\"plot\"></div>\n    <script>const payload = " + dataJson + "; console.log('Plotly data:', payload); try { Plotly.newPlot('plot', payload.data, payload.layout, {responsive:true, displayModeBar:true}); console.log('Chart created'); } catch(e) { console.error('Plotly error:', e); document.getElementById('plot').innerHTML = '<div style=\"padding:20px;color:red;\">Error: ' + e.message + '</div>'; }</script>\n  </body>\n</html>\n";

                    var htmlPath = Path.Combine(appDir, "plot.html");
                    await File.WriteAllTextAsync(htmlPath, html);

                    // Use file:// URI pointing at the AppData file - WebView2 can navigate to local file paths.
                    PlotlyHtml = new Uri(htmlPath).AbsoluteUri;
                    UsePlotlyWebView = true;
                    System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtmlAsync] Windows HTML generated at: {htmlPath}");
                    return;
                }

                else
                {
                    var cacheDir = Path.Combine(FileSystem.CacheDirectory, "plotly_bundle");
                    if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
                    var jsPath = Path.Combine(cacheDir, "plotly-latest.min.js");
                    await File.WriteAllTextAsync(jsPath, plotlyJs);
                    html = "<!doctype html>\n<html>\n  <head>\n    <meta charset=\"utf-8\" />\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n    <script src=\"plotly-latest.min.js\"></script>\n    <style>body { margin: 0; padding: 0; height: 100vh; } #plot { width: 100%; height: 100%; }</style>\n  </head>\n  <body>\n    <div id=\"plot\"></div>\n    <script>const payload = " + dataJson + "; console.log('Plotly data:', payload); try { Plotly.newPlot('plot', payload.data, payload.layout, {responsive:true, displayModeBar:true}); console.log('Chart created'); } catch(e) { console.error('Plotly error:', e); document.getElementById('plot').innerHTML = '<div style=\"padding:20px;color:red;\">Error: ' + e.message + '</div>'; }</script>\n  </body>\n</html>\n";
                    var htmlPath = Path.Combine(cacheDir, "plot.html");
                    await File.WriteAllTextAsync(htmlPath, html);
                    PlotlyHtml = new Uri(htmlPath).AbsoluteUri;
                    UsePlotlyWebView = true;
                    System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtmlAsync] Other platform HTML generated at: {htmlPath}");
                }
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtmlAsync] Preparing local plotly bundle failed: {ex.Message}");
                // fall back to CDN below
            }
        }

        // Fallback: load Plotly from CDN
        html = "<!doctype html>\n<html>\n  <head>\n    <meta charset=\"utf-8\" />\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n    <script src=\"https://cdn.plot.ly/plotly-latest.min.js\"></script>\n    <style>body { margin: 0; padding: 0; height: 100vh; } #plot { width: 100%; height: 100%; }</style>\n  </head>\n  <body>\n    <div id=\"plot\"></div>\n    <script>const payload = " + dataJson + "; console.log('Plotly data:', payload); try { Plotly.newPlot('plot', payload.data, payload.layout, {responsive:true, displayModeBar:true}); console.log('Chart created'); } catch(e) { console.error('Plotly error:', e); document.getElementById('plot').innerHTML = '<div style=\"padding:20px;color:red;\">Error: ' + e.message + '</div>'; }</script>\n  </body>\n</html>\n";

        PlotlyHtml = html;
        UsePlotlyWebView = true;
        System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtmlAsync] CDN fallback HTML generated ({html.Length} chars)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreparePlotlyHtmlAsync] ERROR: {ex.Message}");
            PlotlyHtml = null;
            UsePlotlyWebView = false;
        }
    }
}
