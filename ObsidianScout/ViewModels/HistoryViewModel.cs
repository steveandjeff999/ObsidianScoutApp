using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.IO;
using Microsoft.Maui.Storage;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ObsidianScout.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ICacheService _cacheService;
    private readonly IApiService _apiService;
    private readonly IConnectivityService _connectivityService;

    public ObservableCollection<ScoutingEntry> PendingScouting { get; } = new();
    public ObservableCollection<PitScoutingEntry> PendingPit { get; } = new();

    // Combined server + pending lists shown in tabs
    public ObservableCollection<ScoutingEntry> AllScouting { get; } = new();
    public ObservableCollection<PitScoutingEntry> AllPit { get; } = new();

    private int _selectedTab = 0; // 0 = Match, 1 = Pit
    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab == value) return;
            _selectedTab = value;
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(IsMatchTabSelected));
            OnPropertyChanged(nameof(IsPitTabSelected));
        }
    }

    public bool IsMatchTabSelected => SelectedTab == 0;
    public bool IsPitTabSelected => SelectedTab == 1;

    public IRelayCommand SwitchToMatchCommand => new RelayCommand(() => SelectedTab = 0);
    public IRelayCommand SwitchToPitCommand => new RelayCommand(() => SelectedTab = 1);

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public HistoryViewModel(ICacheService cacheService, IApiService apiService, IConnectivityService connectivityService)
    {
        _cacheService = cacheService;
        _apiService = apiService;
        _connectivityService = connectivityService;
    }

    public async Task LoadAsync()
    {
        PendingScouting.Clear();
        PendingPit.Clear();
        AllScouting.Clear();
        AllPit.Clear();
        // server entries cache used for comparison
        List<ScoutingEntry> serverEntries = new();

        // Load deletion markers persisted to AppData so deleted items are omitted after restart
        var deletedOfflineIds = new List<string>();
        var deletedScoutingIds = new List<int>();
        var deletedPitIds = new List<int>();
        try
        {
            var appData = FileSystem.AppDataDirectory;
            var offlinePath = Path.Combine(appData, "deleted_scouting_offline.json");
            if (File.Exists(offlinePath))
            {
                try { deletedOfflineIds = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(offlinePath)) ?? new List<string>(); } catch { }
            }

            var idsPath = Path.Combine(appData, "deleted_scouting_ids.json");
            if (File.Exists(idsPath))
            {
                try { deletedScoutingIds = JsonSerializer.Deserialize<List<int>>(await File.ReadAllTextAsync(idsPath)) ?? new List<int>(); } catch { }
            }

            var pitIdsPath = Path.Combine(appData, "deleted_pit_ids.json");
            if (File.Exists(pitIdsPath))
            {
                try { deletedPitIds = JsonSerializer.Deserialize<List<int>>(await File.ReadAllTextAsync(pitIdsPath)) ?? new List<int>(); } catch { }
            }
        }
        catch { }

        // Load pending (local) entries
        try
        {
            var pendingScouting = await _cacheService.GetPendingScoutingAsync();
            if (pendingScouting != null)
            {
                // Deduplicate pending by OfflineId or (Timestamp+Team+Match) with tolerance
                var deduped = new List<ScoutingEntry>();
                foreach (var s in pendingScouting.OrderByDescending(e => e.Timestamp))
                {
                    if (!string.IsNullOrEmpty(s.OfflineId) && deletedOfflineIds.Contains(s.OfflineId)) continue;
                    if (s.Id > 0 && deletedScoutingIds.Contains(s.Id)) continue;
                    bool exists = deduped.Any(x => EntriesEqual(x, s));
                    if (!exists) deduped.Add(s);
                }
                foreach (var s in deduped) PendingScouting.Add(s);
            }

            var pendingPit = await _cacheService.GetPendingPitAsync();
            if (pendingPit != null)
            {
                foreach (var p in pendingPit.OrderByDescending(e => e.Timestamp))
                {
                    if (p.Id > 0 && deletedPitIds.Contains(p.Id)) continue;
                    PendingPit.Add(p);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] Error loading pending cache: {ex.Message}");
        }

        // Load server scouting entries (best-effort)
        try
        {
            // Request server entries and allow History to bypass offline-mode setting so users can view server data when connected
            var serverRes = await _apiService.GetAllScoutingDataAsync(limit: 500, ignoreOfflineMode: true);
            if (serverRes != null && serverRes.Success && serverRes.Entries != null && serverRes.Entries.Count > 0)
            {
                serverEntries = serverRes.Entries;
                foreach (var e in serverEntries.OrderByDescending(x => x.Timestamp))
                {
                    if (!string.IsNullOrEmpty(e.OfflineId) && deletedOfflineIds.Contains(e.OfflineId)) continue;
                    if (e.Id > 0 && deletedScoutingIds.Contains(e.Id)) continue;
                    AllScouting.Add(e);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] Failed to load server scouting: {ex.Message}");
        }

        // If server returned nothing, fall back to cached scouting data
        if (AllScouting.Count == 0)
        {
            try
            {
                var cached = await _cacheService.GetCachedScoutingDataAsync();
                if (cached != null && cached.Count > 0)
                {
                    foreach (var e in cached.OrderByDescending(x => x.Timestamp))
                    {
                        if (!string.IsNullOrEmpty(e.OfflineId) && deletedOfflineIds.Contains(e.OfflineId)) continue;
                        if (e.Id > 0 && deletedScoutingIds.Contains(e.Id)) continue;
                        bool exists = AllScouting.Any(x =>
                            (!string.IsNullOrEmpty(e.OfflineId) && !string.IsNullOrEmpty(x.OfflineId) && x.OfflineId == e.OfflineId) ||
                            (e.Id > 0 && x.Id == e.Id) ||
                            (x.Timestamp == e.Timestamp && x.TeamId == e.TeamId && x.MatchId == e.MatchId)
                        );
                        if (!exists) AllScouting.Add(e);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[History] Failed to load cached scouting: {ex.Message}");
            }
        }

        // Merge pending on top of server/cache data (avoid duplicates)
        foreach (var s in PendingScouting.OrderByDescending(e => e.Timestamp))
        {
            bool exists = AllScouting.Any(x =>
                (!string.IsNullOrEmpty(s.OfflineId) && !string.IsNullOrEmpty(x.OfflineId) && x.OfflineId == s.OfflineId) ||
                (s.Id > 0 && x.Id == s.Id) ||
                (x.Timestamp == s.Timestamp && x.TeamId == s.TeamId && x.MatchId == s.MatchId)
            );
            if (!exists) AllScouting.Insert(0, s);
        }

        // Mark entries that differ from server as locally modified (if we loaded server entries above)
        try
        {
            if (serverEntries != null && serverEntries.Count > 0)
            {
                var byId = serverEntries.Where(x => x.Id > 0).ToDictionary(x => x.Id, x => x);
                var byOffline = serverEntries.Where(x => !string.IsNullOrEmpty(x.OfflineId)).ToDictionary(x => x.OfflineId, x => x);

                foreach (var local in AllScouting.ToList())
                {
                    try
                    {
                        ScoutingEntry? serverMatch = null;
                        if (local.Id > 0 && byId.TryGetValue(local.Id, out var m1)) serverMatch = m1;
                        else if (!string.IsNullOrEmpty(local.OfflineId) && byOffline.TryGetValue(local.OfflineId, out var m2)) serverMatch = m2;

                        if (serverMatch != null)
                        {
                            var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = false };
                            var localJson = System.Text.Json.JsonSerializer.Serialize(local.Data ?? new Dictionary<string, object>(), opts);
                            var serverJson = System.Text.Json.JsonSerializer.Serialize(serverMatch.Data ?? new Dictionary<string, object>(), opts);
                            local.HasLocalChanges = !string.Equals(localJson, serverJson, StringComparison.Ordinal);
                        }
                        else
                        {
                            local.HasLocalChanges = false;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        // Also load exported JSON files created by ExportJsonAsync so locally exported entries are visible
        try
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var exportsFolder = Path.Combine(documentsPath, "ObsidianScout", "Exports");
            if (Directory.Exists(exportsFolder))
            {
                var files = Directory.GetFiles(exportsFolder, "*.json");
                foreach (var file in files.OrderByDescending(f => File.GetCreationTimeUtc(f)))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                        if (doc != null)
                        {
                            var teamNumber = doc.ContainsKey("team_number") && int.TryParse(doc["team_number"]?.ToString(), out var tn) ? tn : 0;
                            var matchNumber = doc.ContainsKey("match_number") && int.TryParse(doc["match_number"]?.ToString(), out var mn) ? mn : 0;
                            var scoutName = doc.ContainsKey("scout_name") ? doc["scout_name"]?.ToString() ?? string.Empty : string.Empty;
                            DateTime timestamp = File.GetCreationTimeUtc(file);
                            if (doc.ContainsKey("generated_at") && DateTime.TryParse(doc["generated_at"]?.ToString(), out var parsed)) timestamp = parsed;

                            var entry = new ScoutingEntry
                            {
                                TeamNumber = teamNumber,
                                MatchNumber = matchNumber,
                                ScoutName = scoutName,
                                Timestamp = timestamp.ToLocalTime(),
                                Data = doc.ToDictionary(k => k.Key, v => v.Value ?? new object())
                            };

                            // avoid duplicates by timestamp + team
                            if (!AllScouting.Any(a => a.Timestamp == entry.Timestamp && a.TeamNumber == entry.TeamNumber && a.MatchNumber == entry.MatchNumber))
                            {
                                AllScouting.Insert(0, entry);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[History] Failed to parse export file {file}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] Error loading exported JSON files: {ex.Message}");
        }

        // Load server pit entries
        try
        {
            var pitRes = await _apiService.GetPitScoutingDataAsync();
            if (pitRes != null && pitRes.Success && pitRes.Entries != null)
            {
                foreach (var e in pitRes.Entries.OrderByDescending(x => x.Timestamp))
                {
                    if (e.Id > 0 && deletedPitIds.Contains(e.Id)) continue;
                    AllPit.Add(e);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] Failed to load server pit scouting: {ex.Message}");
        }

        // If server returned nothing, fall back to cached pit data
        if (AllPit.Count == 0)
        {
            try
            {
                var cachedPit = await _cacheService.GetCachedPitScoutingDataAsync();
                if (cachedPit != null && cachedPit.Count > 0)
                {
                    foreach (var e in cachedPit.OrderByDescending(x => x.Timestamp))
                    {
                        if (e.Id > 0 && deletedPitIds.Contains(e.Id)) continue;
                        // avoid duplicates
                        bool exists = AllPit.Any(x => (e.Id > 0 && x.Id == e.Id) || (x.Timestamp == e.Timestamp && x.TeamId == e.TeamId && x.TeamNumber == e.TeamNumber));
                        if (!exists) AllPit.Add(e);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[History] Failed to load cached pit scouting: {ex.Message}");
            }
        }

        // Merge pending pit entries on top
        foreach (var p in PendingPit.OrderByDescending(e => e.Timestamp)) AllPit.Insert(0, p);
        // Load server pit entries (best-effort) and fall back to cache
        try
        {
            System.Diagnostics.Debug.WriteLine("[History] Fetching pit scouting data from API...");
            var pitRes = await _apiService.GetPitScoutingDataAsync();
            System.Diagnostics.Debug.WriteLine($"[History] Pit API response: success={(pitRes?.Success.ToString() ?? "null")}, entries={(pitRes?.Entries?.Count.ToString() ?? "null")}");

            if (pitRes != null && pitRes.Success && pitRes.Entries != null && pitRes.Entries.Count > 0)
            {
                foreach (var e in pitRes.Entries.OrderByDescending(x => x.Timestamp))
                {
                    if (e.Id > 0 && deletedPitIds.Contains(e.Id)) continue;
                    AllPit.Add(e);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[History] No pit entries returned from server - trying cache");
                // Try cached pit entries
                try
                {
                    var cachedPit = await _cacheService.GetCachedPitScoutingDataAsync();
                    System.Diagnostics.Debug.WriteLine($"[History] Cached pit entries count: {(cachedPit?.Count.ToString() ?? "null")}");
                    if (cachedPit != null && cachedPit.Count > 0)
                    {
                        foreach (var e in cachedPit.OrderByDescending(x => x.Timestamp))
                        {
                            if (e.Id > 0 && deletedPitIds.Contains(e.Id)) continue;
                            AllPit.Add(e);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[History] Failed to load cached pit scouting: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] Failed to load server pit scouting: {ex.Message}");
            // Try cache on exception
            try
            {
                var cachedPit = await _cacheService.GetCachedPitScoutingDataAsync();
                if (cachedPit != null && cachedPit.Count > 0)
                {
                    foreach (var e in cachedPit.OrderByDescending(x => x.Timestamp))
                    {
                        if (e.Id > 0 && deletedPitIds.Contains(e.Id)) continue;
                        AllPit.Add(e);
                    }
                }
            }
            catch (Exception ex2)
            {
                System.Diagnostics.Debug.WriteLine($"[History] Failed to load cached pit scouting after error: {ex2.Message}");
            }
        }

        // Merge pending pit entries on top
        var pendingPitList = PendingPit.OrderByDescending(e => e.Timestamp).ToList();
        System.Diagnostics.Debug.WriteLine($"[History] PendingPit count: {pendingPitList.Count}");
        foreach (var p in pendingPitList) AllPit.Insert(0, p);

        // Update status for debugging/UI and dump a sample entry to output
        try
        {
            StatusMessage = $"Loaded {AllScouting.Count} match entries, {AllPit.Count} pit entries";
            System.Diagnostics.Debug.WriteLine($"[History] {StatusMessage}");
            if (AllScouting.Count > 0)
            {
                try
                {
                    var sample = System.Text.Json.JsonSerializer.Serialize(AllScouting[0], new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.Diagnostics.Debug.WriteLine($"[History] Sample match entry:\n{sample}");
                }
                catch { }
            }
            if (AllPit.Count > 0)
            {
                try
                {
                    var samplePit = System.Text.Json.JsonSerializer.Serialize(AllPit[0], new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.Diagnostics.Debug.WriteLine($"[History] Sample pit entry:\n{samplePit}");
                }
                catch { }
            }
            // Ensure default tab selection
            SelectedTab = 0;
        }
        catch { }
    }

    // helper: consider entries equal if offline id matches or id matches or timestamp+team+match within tolerance
    private static bool EntriesEqual(ScoutingEntry a, ScoutingEntry b)
    {
        try
        {
            if (!string.IsNullOrEmpty(a.OfflineId) && !string.IsNullOrEmpty(b.OfflineId)) return a.OfflineId == b.OfflineId;
            if (a.Id > 0 && b.Id > 0) return a.Id == b.Id;
            var diff = Math.Abs((a.Timestamp - b.Timestamp).TotalSeconds);
            return a.TeamId == b.TeamId && a.MatchId == b.MatchId && diff <= 5.0;
        }
        catch { return false; }
    }

    private static bool PitEntriesEqual(PitScoutingEntry a, PitScoutingEntry b)
    {
        try
        {
            if (a.Id > 0 && b.Id > 0) return a.Id == b.Id;
            var diff = Math.Abs((a.Timestamp - b.Timestamp).TotalSeconds);
            return a.TeamId == b.TeamId && diff <= 5.0;
        }
        catch { return false; }
    }

    [RelayCommand]
    public async Task UploadAllAsync()
    {
        if (!_connectivityService.IsConnected)
        {
            StatusMessage = "Offline - cannot upload now";
            return;
        }

        StatusMessage = "Uploading...";

        // Upload scouting entries
        foreach (var entry in PendingScouting.ToList())
        {
            try
            {
                var submission = new ScoutingSubmission
                {
                    TeamId = entry.TeamId,
                    MatchId = entry.MatchId,
                    Data = entry.Data ?? new Dictionary<string, object?>(),
                    OfflineId = entry.OfflineId
                };

                var res = await _apiService.SubmitScoutingDataAsync(submission);
                if (res.Success)
                {
                    await _cacheService.RemovePendingScoutingAsync(e => e.OfflineId == entry.OfflineId || e.Id == entry.Id);
                    PendingScouting.Remove(entry);
                }
            }
            catch { }
        }

        // Upload pit entries
        foreach (var entry in PendingPit.ToList())
        {
            try
            {
                var submission = new PitScoutingSubmission
                {
                    TeamId = entry.TeamId,
                    Data = entry.Data ?? new Dictionary<string, object?>(),
                    Images = entry.Images
                };

                var res = await _apiService.SubmitPitScoutingDataAsync(submission);
                if (res != null && res.Success)
                {
                    int serverId = 0;
                    try { serverId = res.PitScoutingId; } catch { }

                    // Remove pending entries from cache that match this one (by timestamp/team) or by returned id
                    try
                    {
                        await _cacheService.RemovePendingPitAsync(e => (e.Timestamp == entry.Timestamp && e.TeamId == entry.TeamId) || (serverId > 0 && e.Id == serverId));
                    }
                    catch { }

                    // Remove from PendingPit collection
                    try { PendingPit.Remove(entry); } catch { }

                    // Replace or insert server-backed entry into AllPit so UI shows Uploaded
                    try
                    {
                        var match = AllPit.FirstOrDefault(x => x.Timestamp == entry.Timestamp && x.TeamId == entry.TeamId && x.TeamNumber == entry.TeamNumber);
                        var newEntry = new PitScoutingEntry
                        {
                            Id = serverId,
                            TeamId = entry.TeamId,
                            TeamNumber = entry.TeamNumber,
                            TeamName = entry.TeamName,
                            Data = entry.Data,
                            ScoutName = entry.ScoutName,
                            Timestamp = entry.Timestamp,
                            Images = entry.Images ?? new List<string>(),
                            HasLocalChanges = false,
                            UploadInProgress = false
                        };

                        if (match != null)
                        {
                            var idx = AllPit.IndexOf(match);
                            if (idx >= 0) AllPit[idx] = newEntry;
                        }
                        else
                        {
                            AllPit.Insert(0, newEntry);
                        }
                    }
                    catch { }

                    // Update cached pit scouting list
                    try
                    {
                        var cached = await _cacheService.GetCachedPitScoutingDataAsync() ?? new List<PitScoutingEntry>();
                        var idx = cached.FindIndex(x => x.Timestamp == entry.Timestamp && x.TeamId == entry.TeamId && x.TeamNumber == entry.TeamNumber);
                        var cachedEntry = new PitScoutingEntry
                        {
                            Id = serverId,
                            TeamId = entry.TeamId,
                            TeamNumber = entry.TeamNumber,
                            TeamName = entry.TeamName,
                            Data = entry.Data,
                            ScoutName = entry.ScoutName,
                            Timestamp = entry.Timestamp,
                            Images = entry.Images ?? new List<string>()
                        };
                        if (idx >= 0) cached[idx] = cachedEntry; else cached.Insert(0, cachedEntry);
                        await _cacheService.CachePitScoutingDataAsync(cached);
                    }
                    catch { }
                }
            }
            catch { }
        }

        StatusMessage = "Upload complete";
    }
}
