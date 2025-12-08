using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Text.Json;
using System.Collections.ObjectModel;

namespace ObsidianScout.ViewModels;

public partial class PitConfigEditorViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly ISettingsService _settings;
    
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions 
    { 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    [ObservableProperty]
    private string jsonText = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    private PitConfig? _currentConfig;
    public PitConfig? CurrentConfig
 {
        get => _currentConfig;
        set => SetProperty(ref _currentConfig, value);
    }

    // Collections for sections
    public ObservableCollection<PitSection> Sections { get; } = new();

    // Element types available
    public List<string> ElementTypes { get; } = new() { "text", "textarea", "number", "boolean", "select", "multiselect" };

    // Current element being edited
    [ObservableProperty]
    private PitElement? currentElement;

    // Options for current element
    public ObservableCollection<PitOption> CurrentElementOptions { get; } = new();

    // Counts
    [ObservableProperty]
    private int sectionCount;

    [ObservableProperty]
    private int totalElementCount;

 // View mode
    [ObservableProperty]
    private bool isRawVisible = true;

    [ObservableProperty]
    private bool isFormVisible = false;

    public PitConfigEditorViewModel(IApiService apiService, ISettingsService settingsService)
    {
   _api = apiService;
        _settings = settingsService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
  {
     StatusMessage = "Loading pit config...";
            // Use team config endpoint for editors (explicit per-team config, not alliance)
  var resp = await _api.GetTeamPitConfigAsync();
 
  if (resp.Success && resp.Config != null)
      {
NormalizeConfig(resp.Config);
   CurrentConfig = resp.Config;
   JsonText = JsonSerializer.Serialize(resp.Config, _jsonOptions);
     PopulateCollections(resp.Config);
     UpdateStatusCounts();
       StatusMessage = "Loaded pit config successfully";
  }
      else
    {
   StatusMessage = resp.Error ?? "Failed to load pit config";
  }
        }
        catch (Exception ex)
   {
    StatusMessage = $"Error loading: {ex.Message}";
    System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Load error: {ex}");
        }
    }

    [RelayCommand]
    public async Task<bool> ParseJsonToModelAsync()
  {
        try
        {
    PitConfig? cfg = null;

 // Deserialize on background thread to avoid blocking UI
 await Task.Run(() =>
 {
 cfg = JsonSerializer.Deserialize<PitConfig>(JsonText, _jsonOptions);
 });

 if (cfg == null)
 {
 StatusMessage = "Invalid pit config JSON";
 return false;
 }

 // Normalize on background thread (works on POCOs)
 NormalizeConfig(cfg);

 // Apply to UI-bound collections on UI thread
 await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
 {
 CurrentConfig = cfg;
 PopulateCollections(cfg);
 UpdateStatusCounts();
 });

 StatusMessage = "Parsed JSON to model";
 return true;
 }
 catch (JsonException jex)
 {
 StatusMessage = "JSON parse error: " + jex.Message;
 return false;
 }
    }

    private void FinalizeAvailableTypes()
    {
        try
        {
            foreach (var sec in Sections)
            {
                if (sec.Elements == null) continue;
                foreach (var el in sec.Elements)
                {
                    // Ensure AvailableTypes populated and includes the element's current Type
                    if (el.AvailableTypes == null)
                    {
                        el.AvailableTypes = new System.Collections.ObjectModel.ObservableCollection<string>(ElementTypes);
                    }
                    else
                    {
                        // If AvailableTypes doesn't contain current type, insert it at front
                        if (!el.AvailableTypes.Contains(el.Type))
                        {
                            el.AvailableTypes.Insert(0, el.Type);
                        }
                    }

                    // Now set SelectedIndex to point to the item that equals el.Type (case-insensitive)
                    if (el.AvailableTypes != null && el.AvailableTypes.Count >0)
                    {
                        var idx = el.AvailableTypes.ToList().FindIndex(x => string.Equals(x, el.Type, StringComparison.OrdinalIgnoreCase));
                        if (idx >=0)
                        {
                            // assign the exact string instance from AvailableTypes so SelectedItem binding (if used) matches
                            el.Type = el.AvailableTypes[idx];
                            // set selected index silently so it doesn't overwrite Type
                            el.SetSelectedIndexSilently(idx);
                        }
                        else
                        {
                            // leave SelectedIndex at -1, do not change Type
                            el.SetSelectedIndexSilently(-1);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] FinalizeAvailableTypes failed: {ex}");
        }
    }

    private void PopulateCollections(PitConfig cfg)
    {
        Sections.Clear();
        
        if (cfg.PitScouting?.Sections != null)
        {
      foreach (var section in cfg.PitScouting.Sections)
   {
    // Ensure elements collection is initialized as ObservableCollection
    if (section.Elements == null)
    {
        section.Elements = new System.Collections.ObjectModel.ObservableCollection<PitElement>();
    }
         
    // Ensure each element has options list if it's a select/multiselect
                foreach (var element in section.Elements)
   {
                    var original = element.Type;
                    // Normalize type for internal handling
                    var normalized = NormalizeType(element.Type);

                    // If normalized type is in ElementTypes, use that. Otherwise, keep original but expose AvailableTypes
                    var match = ElementTypes.FirstOrDefault(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        element.Type = match;
                        // use default element types as picker items
                        element.AvailableTypes = new System.Collections.ObjectModel.ObservableCollection<string>(ElementTypes);
                    }
                    else
                    {
                        // unknown type from server - include it plus the known ElementTypes so user can keep/see original value
                        var list = new List<string> { original };
                        list.AddRange(ElementTypes);
                        element.AvailableTypes = new System.Collections.ObjectModel.ObservableCollection<string>(list);
                        element.Type = original; // keep server-provided value so UI shows it
                    }

        // Debug logging to help trace server types mapping
        try
        {
            System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Element '{element.Name}' originalType='{original}', normalized='{normalized}', assignedType='{element.Type}', availableCount={element.AvailableTypes?.Count ??0}");
        }
        catch { }

        // Initialize Options as ObservableCollection if needed
        if ((element.Type == "select" || element.Type == "multiselect"))
        {
            if (element.Options == null)
            {
                element.Options = new System.Collections.ObjectModel.ObservableCollection<PitOption>();
            }
        }
            }
 
   Sections.Add(section);
 }
      }

        SectionCount = Sections.Count;
        TotalElementCount = Sections.Sum(s => s.Elements?.Count ?? 0);

        System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Populated: {SectionCount} sections, {TotalElementCount} total elements");

        // Ensure AvailableTypes exist for all elements and preserve Type values
        FinalizeAvailableTypes();

        // Force picker selection refresh on UI thread
        try
        {
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var sec in Sections)
                {
                    if (sec.Elements == null) continue;
                    foreach (var el in sec.Elements)
                    {
                        // Reassign to same value to trigger UI update if needed
                        var current = el.Type;
                        el.Type = current;

                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Refreshing picker for '{el.Name}' -> '{el.Type}' (Available first='{el.AvailableTypes?.FirstOrDefault()}')");
                        }
                        catch { }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Force picker refresh failed: {ex}");
        }
    }

    private void NormalizeConfig(PitConfig cfg)
    {
    if (cfg.PitScouting == null)
        {
  cfg.PitScouting = new PitScoutingConfig
   {
        Title = "Pit Scouting",
    Description = "Default pit scouting configuration",
                Sections = new List<PitSection>()
            };
        }

        cfg.PitScouting.Sections ??= new List<PitSection>();

        // Ensure all sections have ObservableCollection for Elements
 foreach (var sec in cfg.PitScouting.Sections)
 {
   if (sec.Elements == null)
   {
     sec.Elements = new System.Collections.ObjectModel.ObservableCollection<PitElement>();
   }
 }
    }

    private string NormalizeType(string type)
    {
      if (string.IsNullOrEmpty(type)) return "text";
    
      var t = type.Trim().ToLowerInvariant();

      return t switch
      {
          "multi_select" => "multiselect",
          "multiple_choice" => "multiselect",
          "multiple-choice" => "multiselect",
          "multi-select" => "multiselect",
          "multiselect" => "multiselect",
          "multiplechoice" => "multiselect",
          "select" => "select",
          "textarea" => "textarea",
          "text" => "text",
          "number" => "number",
          "boolean" => "boolean",
          _ => t
      };
    }
  
  private void UpdateStatusCounts()
    {
        if (CurrentConfig == null)
        {
            StatusMessage = "No config loaded";
       return;
        }

        StatusMessage = $"Sections: {SectionCount}, Elements: {TotalElementCount}";
    }

    public void UpdateJsonFromModel()
    {
        if (CurrentConfig == null) return;
        
        try
        {
            SyncCollectionsToModel();
            JsonText = JsonSerializer.Serialize(CurrentConfig, _jsonOptions);
      StatusMessage = "Updated JSON from model";
        }
   catch (Exception ex)
      {
     StatusMessage = $"Failed to serialize model: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
         // If in Raw JSON view, parse first
            if (IsRawVisible)
      {
         var parseOk = await ParseJsonToModelAsync();
           if (!parseOk)
     {
        StatusMessage = "Cannot save: Invalid JSON";
   return;
       }
      }
            else
    {
       // Sync collections to model
        SyncCollectionsToModel();
     }

            if (CurrentConfig == null)
            {
     StatusMessage = "No config to save";
           return;
   }

            // Save to server
            StatusMessage = "Saving pit config...";
            
    // Note: You'll need to add this endpoint to IApiService and ApiService
            // For now, we'll use a placeholder
        var saveResp = await _api.SavePitConfigAsync(CurrentConfig);
            
         if (saveResp.Success)
   {
        StatusMessage = "? Saved successfully!";
        await LoadAsync();
    }
          else
   {
    StatusMessage = saveResp.Error ?? "Save failed";
         }
        }
      catch (Exception ex)
        {
         StatusMessage = $"Save error: {ex.Message}";
System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Save error: {ex}");
      }
    }

    private void SyncCollectionsToModel()
    {
        if (CurrentConfig?.PitScouting == null) return;
        
        // Convert Sections ObservableCollection to List, and Elements ObservableCollection to List for each section
        var sectionsList = new List<PitSection>();
        foreach (var sec in Sections)
        {
            var sectionCopy = new PitSection
            {
                Id = sec.Id,
                Name = sec.Name,
                Elements = new System.Collections.ObjectModel.ObservableCollection<PitElement>(sec.Elements ?? new System.Collections.ObjectModel.ObservableCollection<PitElement>())
            };
            sectionsList.Add(sectionCopy);
        }
        
        CurrentConfig.PitScouting.Sections = sectionsList;
    }

    [RelayCommand]
    public async Task RevertAsync()
    {
        await LoadAsync();
    }

    public async Task ShowRawAsync()
    {
 await Task.Run(() =>
 {
            UpdateJsonFromModel();
        });
        
        IsRawVisible = true;
      IsFormVisible = false;
        StatusMessage = "Switched to Raw JSON";
    }

    public async Task<bool> ShowFormAsync()
    {
        StatusMessage = "Parsing JSON...";
        await Task.Delay(50);
        
        var ok = await ParseJsonToModelAsync();
        
        if (!ok)
{
      return false;
   }

     IsRawVisible = false;
        IsFormVisible = true;
    StatusMessage = "Switched to Form Editor";
     return true;
  }

    [RelayCommand]
    public void AddSection()
    {
        var newSection = new PitSection
        {
    Name = "New Section",
   Elements = new System.Collections.ObjectModel.ObservableCollection<PitElement>()
  };
        
      Sections.Add(newSection);
    SectionCount = Sections.Count;
        StatusMessage = "Added new section";
    }

    [RelayCommand]
    public void DeleteSection(PitSection section)
    {
        if (Sections.Contains(section))
      {
            Sections.Remove(section);
            SectionCount = Sections.Count;
        TotalElementCount = Sections.Sum(s => s.Elements?.Count ?? 0);
   StatusMessage = $"Deleted section: {section.Name}";
      }
    }

    [RelayCommand]
    public void AddElementToSection(PitSection section)
    {
     if (section == null) return;

   if (section.Elements == null)
   {
     section.Elements = new System.Collections.ObjectModel.ObservableCollection<PitElement>();
   }
        
  var newElement = new PitElement
        {
            Id = $"field_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
            Name = "New Field",
          Type = "text",
 Required = false,
    Placeholder = "Enter value"
        };

 // Initialize AvailableTypes so the Picker has items to display and SelectedIndex to match Type
 newElement.AvailableTypes = new System.Collections.ObjectModel.ObservableCollection<string>(ElementTypes);
 newElement.SelectedIndex =0; // 'text'

        section.Elements.Add(newElement);
 TotalElementCount = Sections.Sum(s => s.Elements?.Count ?? 0);
        StatusMessage = $"Added element to {section.Name}";
 
 System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Added element '{newElement.Name}' to section '{section.Name}'. Section now has {section.Elements.Count} elements.");
 }

    [RelayCommand]
    public void DeleteElement(PitElement element)
    {
  if (element == null) return;

 foreach (var section in Sections)
        {
            if (section.Elements != null && section.Elements.Contains(element))
        {
         var elementName = element.Name;
 var sectionName = section.Name;
 
 section.Elements.Remove(element);
           TotalElementCount = Sections.Sum(s => s.Elements?.Count ?? 0);
   StatusMessage = $"Deleted element: {elementName}";
 
 System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Deleted element '{elementName}' from section '{sectionName}'. Section now has {section.Elements.Count} elements.");
        return;
            }
      }
    }

    [RelayCommand]
 public void AddOptionToElement(PitElement element)
    {
        if (element == null || (element.Type != "select" && element.Type != "multiselect")) 
       return;

 if (element.Options == null)
 {
 element.Options = new System.Collections.ObjectModel.ObservableCollection<PitOption>();
 }
 
 var newOption = new PitOption
 {
 Value = $"option_{element.Options.Count + 1}",
 Label = $"Option {element.Options.Count + 1}"
 };
 
 element.Options.Add(newOption);
 StatusMessage = $"Added option to {element.Name}";
 
 System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Added option to '{element.Name}'. Element now has {element.Options.Count} options.");
 }

    [RelayCommand]
    public void DeleteOptionFromElement(PitOption option)
    {
        if (option == null) return;
 
 foreach (var section in Sections)
 {
 if (section.Elements == null) continue;
 
 foreach (var element in section.Elements)
 {
 if (element.Options != null && element.Options.Contains(option))
 {
 var optionLabel = option.Label;
 var elementName = element.Name;
 
 element.Options.Remove(option);
 StatusMessage = $"Deleted option '{optionLabel}' from {elementName}";
 
 System.Diagnostics.Debug.WriteLine($"[PitConfigEditor] Deleted option '{optionLabel}' from element '{elementName}'. Element now has {element.Options.Count} options.");
 return;
 }
 }
 }
 }

 [RelayCommand]
 public void MoveSectionUp(PitSection section)
 {
 var index = Sections.IndexOf(section);
 if (index > 0)
 {
 Sections.Move(index, index - 1);
 StatusMessage = $"Moved {section.Name} up";
 }
 }

 [RelayCommand]
 public void MoveSectionDown(PitSection section)
 {
 var index = Sections.IndexOf(section);
 if (index < Sections.Count - 1)
 {
 Sections.Move(index, index + 1);
 StatusMessage = $"Moved {section.Name} down";
 }
 }

 [RelayCommand]
 public void MoveElementUp(PitElement element)
 {
 foreach (var section in Sections)
 {
 if (section.Elements == null) continue;

 var index = section.Elements.IndexOf(element);
 if (index > 0)
 {
 var temp = section.Elements[index];
 section.Elements[index] = section.Elements[index - 1];
 section.Elements[index - 1] = temp;
 StatusMessage = $"Moved {element.Name} up";

 // Refresh the collection
 OnPropertyChanged(nameof(Sections));
 return;
 }
 }
 }

 [RelayCommand]
 public void MoveElementDown(PitElement element)
 {
 foreach (var section in Sections)
 {
 if (section.Elements == null) continue;

 var index = section.Elements.IndexOf(element);
 if (index >= 0 && index < section.Elements.Count - 1)
 {
 var temp = section.Elements[index];
 section.Elements[index] = section.Elements[index + 1];
 section.Elements[index + 1] = temp;
 StatusMessage = $"Moved {element.Name} down";

 // Refresh the collection
 OnPropertyChanged(nameof(Sections));
 return;
 }
 }
 }
}
