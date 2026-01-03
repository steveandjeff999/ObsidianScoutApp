using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;

namespace ObsidianScout.ViewModels;

public partial class PitScoutingEditViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;
    
    [ObservableProperty]
    private Models.PitConfig? pitConfig;
    
    [ObservableProperty]
    private PitScoutingEntry? pitEntry;
    
    [ObservableProperty]
    private bool isLoading;
    
    [ObservableProperty]
    private string statusMessage = string.Empty;
    
    [ObservableProperty]
    private bool isEditMode = true;
    
    private Dictionary<string, object?> _fieldValues = new();
    private int _entryId;

    public PitScoutingEditViewModel(IApiService apiService, ISettingsService settingsService)
    {
    _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public async Task LoadEntryAsync(int entryId)
    {
        _entryId = entryId;
        IsLoading = true;
   
        try
 {
            System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Loading entry {entryId}...");
            
            // Load pit config first
         var configResponse = await _apiService.GetPitConfigAsync();
            if (configResponse.Success && configResponse.Config != null)
{
                PitConfig = configResponse.Config;
         System.Diagnostics.Debug.WriteLine("[PitScoutingEdit] Pit config loaded");
  }
            else
   {
   StatusMessage = "Failed to load pit config";
    return;
       }
        
          // Load the specific entry
          var entry = await _apiService.GetPitScoutingEntryAsync(entryId);
          if (entry != null)
            {
                PitEntry = entry;
                // If this entry has a server id, treat as read-only (uploaded)
                try
                {
                    IsEditMode = entry.Id == 0;
                    if (!IsEditMode)
                    {
                        StatusMessage = "Uploaded entry - editing is read-only. Update on server.";
                    }
                }
                catch { }
            System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Entry loaded for team {entry.TeamNumber}");
        
         // Populate field values from entry data
      if (entry.Data != null)
       {
          foreach (var kvp in entry.Data)
          {
           _fieldValues[kvp.Key] = kvp.Value;
               System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit]   {kvp.Key} = {kvp.Value}");
      }
          }
    
     StatusMessage = string.Empty;
            }
            else
            {
       StatusMessage = "Entry not found";
      }
        }
   catch (Exception ex)
  {
          StatusMessage = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Error: {ex}");
        }
        finally
        {
     IsLoading = false;
        }
    }

    public object? GetFieldValue(string fieldId)
    {
        _fieldValues.TryGetValue(fieldId, out var value);
  return value;
    }

    public void SetFieldValue(string fieldId, object? value)
    {
        _fieldValues[fieldId] = value;
        System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] SetFieldValue: {fieldId} = {value}");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
      if (PitEntry == null)
        {
 StatusMessage = "No entry to save";
            return;
    }
        
        IsLoading = true;
      StatusMessage = "Saving...";
      
        try
        {
  var submission = new PitScoutingSubmission
            {
      TeamId = PitEntry.TeamId,
       Data = new Dictionary<string, object?>(_fieldValues)
    };
            
      System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Updating entry {_entryId}...");
   var response = await _apiService.UpdatePitScoutingDataAsync(_entryId, submission);
        
    if (response.Success)
            {
             StatusMessage = "? Saved successfully!";
  System.Diagnostics.Debug.WriteLine("[PitScoutingEdit] Save successful");
    
  await Task.Delay(1500);
     
// Navigate back
        await Shell.Current.GoToAsync("..");
        }
       else
      {
         StatusMessage = $"Save failed: {response.Error}";
         System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Save failed: {response.Error}");
       }
        }
  catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
     System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Save error: {ex}");
        }
     finally
        {
            IsLoading = false;
  }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (PitEntry == null)
       return;
        
        var confirm = await Shell.Current.DisplayAlert(
    "Delete Entry",
  $"Are you sure you want to delete pit scouting data for team {PitEntry.TeamNumber}?",
     "Delete",
            "Cancel");
        
        if (!confirm)
 return;
   
        IsLoading = true;
        StatusMessage = "Deleting...";
  
   try
        {
            System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Deleting entry {_entryId}...");
     var response = await _apiService.DeletePitScoutingEntryAsync(_entryId);
        
    if (response.Success)
       {
      StatusMessage = "? Deleted successfully!";
      System.Diagnostics.Debug.WriteLine("[PitScoutingEdit] Delete successful");
         
   await Task.Delay(1000);
        
    // Navigate back
        await Shell.Current.GoToAsync("..");
            }
  else
     {
                StatusMessage = $"Delete failed: {response.Error}";
          System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Delete failed: {response.Error}");
            }
        }
        catch (Exception ex)
        {
      StatusMessage = $"Error: {ex.Message}";
System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Delete error: {ex}");
   }
   finally
        {
            IsLoading = false;
}
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
  await Shell.Current.GoToAsync("..");
    }
}
