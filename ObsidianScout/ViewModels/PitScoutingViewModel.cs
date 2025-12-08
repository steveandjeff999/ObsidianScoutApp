using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;

namespace ObsidianScout.ViewModels;

public partial class PitScoutingViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;

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

    public PitScoutingViewModel(IApiService apiService, ISettingsService settingsService)
    {
  _apiService = apiService;
        _settingsService = settingsService;
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
     foreach (var team in response.Teams.OrderBy(t => t.TeamNumber))
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
     await Task.Delay(3000);
  StatusMessage = string.Empty;
       ResetForm();
}
        else
            {
    StatusMessage = $"Error: {result.Error}";
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
