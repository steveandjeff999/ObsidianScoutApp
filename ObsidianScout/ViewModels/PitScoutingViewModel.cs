using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;
// removed invalid usings

namespace ObsidianScout.ViewModels;

public partial class PitScoutingViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;
    private readonly ICacheService _cacheService;
    private readonly IConnectivityService _connectivityService;

    [ObservableProperty]
    private int teamId;

    [ObservableProperty]
    private Team? selectedTeam;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private PitConfig? pitConfig;

    [ObservableProperty]
    private string scoutName = string.Empty;

    [ObservableProperty]
    private bool isOfflineMode;

    [ObservableProperty]
private bool isViewingHistory = false;

    private Dictionary<string, object?> fieldValues = new();

    public ObservableCollection<Team> Teams { get; } = new();
    public ObservableCollection<PitScoutingEntry> HistoryEntries { get; } = new();

    public PitScoutingViewModel(IApiService apiService, ISettingsService settingsService, ICacheService cacheService, IConnectivityService connectivityService)
    {
  _apiService = apiService;
        _settingsService = settingsService;
        _cacheService = cacheService;
        _connectivityService = connectivityService;
 _ = InitializeAsync();
    }

    private async Task InitializeAsync()
  {
        await LoadPitConfigAsync();
        await LoadTeamsAsync();
 await LoadScoutNameAsync();
    }

    private async Task LoadScoutNameAsync()
    {
        try
        {
            var username = await _settingsService.GetUsernameAsync();
            if (!string.IsNullOrEmpty(username))
         {
      ScoutName = username;
            }
        }
        catch (Exception ex)
        {
    System.Diagnostics.Debug.WriteLine($"Failed to load scout name: {ex.Message}");
        }
    }

    partial void OnSelectedTeamChanged(Team? value)
    {
        if (value != null)
      {
     TeamId = value.Id;
        }
    }

    private async Task LoadPitConfigAsync()
    {
 IsLoading = true;
        try
        {
            System.Diagnostics.Debug.WriteLine("[PitScouting] Loading pit config...");
            var response = await _apiService.GetPitConfigAsync();

      System.Diagnostics.Debug.WriteLine($"[PitScouting] Response success: {response.Success}");
      System.Diagnostics.Debug.WriteLine($"[PitScouting] Response config null: {response.Config == null}");

            if (response.Success && response.Config != null)
       {
         PitConfig = response.Config;

     System.Diagnostics.Debug.WriteLine($"[PitScouting] PitConfig loaded. PitScouting null: {PitConfig.PitScouting == null}");

      if (PitConfig.PitScouting != null)
    {
     System.Diagnostics.Debug.WriteLine($"[PitScouting] Title: {PitConfig.PitScouting.Title}");
      System.Diagnostics.Debug.WriteLine($"[PitScouting] Sections count: {PitConfig.PitScouting.Sections?.Count ?? 0}");

          if (PitConfig.PitScouting.Sections != null)
   {
                foreach (var section in PitConfig.PitScouting.Sections)
      {
    System.Diagnostics.Debug.WriteLine($"[PitScouting]   Section: {section.Name} with {section.Elements?.Count ?? 0} elements");

    if (section.Elements != null)
           {
        foreach (var element in section.Elements)
  {
               System.Diagnostics.Debug.WriteLine($"[PitScouting]     Element: {element.Name}, Type: {element.Type}, Options: {element.Options?.Count ?? 0}");

           if (element.Options != null && element.Options.Count > 0)
         {
     foreach (var option in element.Options)
       {
             System.Diagnostics.Debug.WriteLine($"[PitScouting]   Option: {option.Label} = {option.Value}");
    }
           }
             }
 }
       }
            }
}

    InitializeFieldValues();

    if (!string.IsNullOrEmpty(response.Error) && response.Error.Contains("offline"))
 {
           IsOfflineMode = true;
    StatusMessage = "Using cached pit config";
              await Task.Delay(3000);
    StatusMessage = string.Empty;
 }
 else
   {
       IsOfflineMode = false;
     }
      }
            else
   {
   StatusMessage = $"Failed to load pit config: {response.Error}";
          System.Diagnostics.Debug.WriteLine($"[PitScouting] Failed to load: {response.Error}");
    }
        }
    catch (Exception ex)
        {
    StatusMessage = $"Error: {ex.Message}";
     System.Diagnostics.Debug.WriteLine($"[PitScouting] Exception: {ex.Message}");
        System.Diagnostics.Debug.WriteLine($"[PitScouting] Stack trace: {ex.StackTrace}");
    }
        finally
        {
      IsLoading = false;
   }
  }

    private async Task LoadTeamsAsync(bool silent = false)
    {
        try
   {
            if (!silent)
      {
          StatusMessage = "Loading teams...";
            }

    var response = await _apiService.GetTeamsAsync(limit: 500);

            if (response.Success && response.Teams != null && response.Teams.Count > 0)
         {
       Teams.Clear();
                // Deduplicate teams by TeamNumber (like TeamsPage does)
                var uniqueTeams = response.Teams
                    .GroupBy(t => t.TeamNumber)
                    .Select(g => g.First())
                    .OrderBy(t => t.TeamNumber);
     foreach (var team in uniqueTeams)
         {
  Teams.Add(team);
       }

     if (!string.IsNullOrEmpty(response.Error) && response.Error.Contains("offline"))
                {
               IsOfflineMode = true;
          }
   else
                {
  IsOfflineMode = false;
      }
          
    // Clear status message after successful load
       if (!silent)
         {
         StatusMessage = string.Empty;
       }
         }
            else
   {
             if (!silent)
    {
     StatusMessage = $"Failed to load teams: {response.Error}";
         }
     }
        }
    catch (Exception ex)
        {
     if (!silent)
       {
    StatusMessage = $"Error loading teams: {ex.Message}";
            }
     }
    }

    private void InitializeFieldValues()
    {
      fieldValues.Clear();
        if (PitConfig?.PitScouting?.Sections == null) return;

        foreach (var section in PitConfig.PitScouting.Sections)
      {
         foreach (var element in section.Elements)
        {
        fieldValues[element.Id] = element.DefaultValue ?? GetDefaultForType(element.Type);
            }
        }
    }

    private object GetDefaultForType(string type)
    {
    return type.ToLower() switch
        {
   "number" => 0,
       "boolean" => false,
    "select" => string.Empty,
       "multiselect" => new List<string>(),
            "text" => string.Empty,
      "textarea" => string.Empty,
  _ => string.Empty
    };
    }

    public object? GetFieldValue(string fieldId)
    {
     return fieldValues.TryGetValue(fieldId, out var value) ? value : null;
    }

    public void SetFieldValue(string fieldId, object? value)
    {
        fieldValues[fieldId] = value;
        OnPropertyChanged("FieldValuesChanged");
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (TeamId <= 0 || SelectedTeam == null)
        {
          StatusMessage = "Please select a team";
   return;
        }

 if (PitConfig?.PitScouting?.Sections != null)
        {
        foreach (var section in PitConfig.PitScouting.Sections)
  {
          foreach (var element in section.Elements)
       {
   if (element.Required && (!fieldValues.TryGetValue(element.Id, out var value) || IsValueEmpty(value)))
      {
StatusMessage = $"Required: {element.Name}";
       return;
     }
        }
          }
        }

        IsLoading = true;
    StatusMessage = "Submitting...";

        try
        {
            var convertedData = new Dictionary<string, object?>();
      foreach (var kvp in fieldValues)
   {
         convertedData[kvp.Key] = kvp.Value;
            }

  if (!string.IsNullOrEmpty(ScoutName))
     {
       convertedData["scout_name"] = ScoutName;
   }

            var submission = new PitScoutingSubmission
     {
          TeamId = TeamId,
                Data = convertedData
     };

  var result = await _apiService.SubmitPitScoutingDataAsync(submission);

            if (result.Success)
            {
                StatusMessage = "Submitted successfully!";

                // Build a local PitScoutingEntry for history
                try
                {
                    var entry = new PitScoutingEntry
                    {
                        TeamId = TeamId,
                        TeamNumber = SelectedTeam?.TeamNumber ?? 0,
                        Timestamp = DateTime.Now,
                        Data = convertedData.ToDictionary(k => k.Key, v => v.Value ?? new object()),
                        Images = new List<string>()
                    };

                    // If server returned an id, attach it so entry is treated as uploaded
                    try
                    {
                        if (result.PitScoutingId > 0)
                        {
                            entry.Id = result.PitScoutingId;
                            entry.HasLocalChanges = false;
                        }
                    }
                    catch { }

                    // Insert into HistoryViewModel so history updates immediately
                    try
                    {
                        var services = Application.Current?.Handler?.MauiContext?.Services;
                        if (services != null && services.GetService(typeof(HistoryViewModel)) is HistoryViewModel hvm)
                        {
                            await MainThread.InvokeOnMainThreadAsync(() => { try { hvm.AllPit.Insert(0, entry); } catch { } });

                            // Remove any matching pending entries from in-memory collections so UI reflects uploaded state
                            try
                            {
                                await MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    // remove pending duplicates (same timestamp + team)
                                    var toRemove = hvm.PendingPit.Where(p => p.TeamId == entry.TeamId && p.Timestamp == entry.Timestamp).ToList();
                                    foreach (var r in toRemove) { try { hvm.PendingPit.Remove(r); } catch { } }

                                    // remove any local AllPit entries that match but are still pending (Id == 0)
                                    var localMatches = hvm.AllPit.Where(p => p.TeamId == entry.TeamId && p.Timestamp == entry.Timestamp && p.Id == 0).ToList();
                                    foreach (var lm in localMatches) { try { hvm.AllPit.Remove(lm); } catch { } }
                                    // ensure server-backed entry is present at top
                                    if (!hvm.AllPit.Any(p => p.Id == entry.Id && entry.Id > 0)) hvm.AllPit.Insert(0, entry);
                                });
                            }
                            catch { }
                            // Refresh history view model to ensure UI/cache reconciliation
                            try
                            {
                                if (services.GetService(typeof(HistoryViewModel)) is HistoryViewModel hvm2)
                                {
                                    await hvm2.LoadAsync();
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }

                    // Also update cached pit scouting data so History.LoadAsync can pick it up
                    try
                    {
                        if (_cacheService != null)
                        {
                            var cached = await _cacheService.GetCachedPitScoutingDataAsync() ?? new List<PitScoutingEntry>();

                            // If server returned id, remove any matching pending entries first
                            try
                            {
                                if (entry.Id > 0)
                                {
                                    await _cacheService.RemovePendingPitAsync(x => x.Id == entry.Id || (x.Timestamp == entry.Timestamp && x.TeamId == entry.TeamId));
                                }
                            }
                            catch { }

                            // Insert or update cached list
                            var existingIdx = cached.FindIndex(x => (entry.Id > 0 && x.Id == entry.Id) || (x.Timestamp == entry.Timestamp && x.TeamId == entry.TeamId));
                            if (existingIdx >= 0) cached[existingIdx] = entry; else cached.Insert(0, entry);

                            await _cacheService.CachePitScoutingDataAsync(cached);
                        }
                    }
                    catch { }
                }
                catch { }

                await Task.Delay(3000);
                StatusMessage = string.Empty;
                ResetForm();
            }
        else
            {
    StatusMessage = $"Error: {result.Error}";
    // Save to pending pit cache so it appears in history and can be retried
    try
    {
        var entry = new PitScoutingEntry
        {
            TeamId = TeamId,
            TeamNumber = SelectedTeam?.TeamNumber ?? 0,
            Timestamp = DateTime.Now,
            Data = convertedData.ToDictionary(k => k.Key, v => v.Value ?? new object()),
            Images = new List<string>()
        };

        if (_cacheService != null)
        {
            await _cacheService.AddPendingPitAsync(entry);
            try
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                if (services != null && services.GetService(typeof(HistoryViewModel)) is HistoryViewModel hvm2)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => { try { hvm2.AllPit.Insert(0, entry); } catch { } });
                }
            }
            catch { }
        }
    }
    catch { }
 }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
 {
            IsLoading = false;
        }
    }

    private bool IsValueEmpty(object? value)
    {
        if (value == null) return true;
        if (value is string str) return string.IsNullOrWhiteSpace(str);
        if (value is int intVal) return intVal == 0;
        if (value is List<string> list) return list.Count == 0;
        return false;
  }

    [RelayCommand]
    private async Task RefreshAsync()
    {
   await LoadPitConfigAsync();
  await LoadTeamsAsync();
    }

    [RelayCommand]
    private void ResetForm()
    {
        SelectedTeam = null;
        TeamId = 0;
        ScoutName = string.Empty;
      InitializeFieldValues();
        OnPropertyChanged("FieldValuesChanged");
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ViewHistoryAsync()
    {
     IsLoading = true;
        IsViewingHistory = true;
 StatusMessage = "Loading history...";

        try
        {
    var response = await _apiService.GetPitScoutingDataAsync(SelectedTeam?.TeamNumber);

    HistoryEntries.Clear();

          if (response.Success && response.Entries != null && response.Entries.Count > 0)
  {
      foreach (var entry in response.Entries.OrderByDescending(e => e.Timestamp))
{
      HistoryEntries.Add(entry);
     }

    StatusMessage = $"Found {HistoryEntries.Count} entries";
      await Task.Delay(1500);
StatusMessage = string.Empty;
    }
          else if (response.Success && response.Entries != null && response.Entries.Count == 0)
            {
    StatusMessage = "No pit scouting data found for this team";
     }
          else
            {
    StatusMessage = $"Error: {response.Error}";
            }
        }
 catch (Exception ex)
      {
        StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CloseHistory()
    {
        IsViewingHistory = false;
        HistoryEntries.Clear();
 }

    [RelayCommand]
    private async Task EditEntryAsync(PitScoutingEntry entry)
    {
        if (entry == null) return;

        try
        {
            await Shell.Current.GoToAsync("PitScoutingEditPage", new Dictionary<string, object>
      {
    { "EntryId", entry.Id }
   });
    }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation to edit page failed: {ex}");
          await Shell.Current.DisplayAlert("Error", "Could not open edit page", "OK");
  }
    }
}
