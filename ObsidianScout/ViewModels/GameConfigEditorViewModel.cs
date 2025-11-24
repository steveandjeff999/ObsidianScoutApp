using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Linq;

namespace ObsidianScout.ViewModels;

public partial class GameConfigEditorViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly ISettingsService _settings;
  
    // Reusable JSON serializer options
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions 
    { 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never  // Include all fields
    };

    [ObservableProperty]
    private string jsonText = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    private GameConfig? _currentConfig;
    public GameConfig? CurrentConfig
    {
     get => _currentConfig;
        set
        {
   SetProperty(ref _currentConfig, value);
  // Update match types string when config changes
   if (value != null)
         {
                MatchTypesString = string.Join(", ", value.MatchTypes ?? new List<string>());
       }
 }
    }

    // Match Types as comma-separated string for UI
    private string _matchTypesString = string.Empty;
    public string MatchTypesString
    {
 get => _matchTypesString;
        set
        {
 if (SetProperty(ref _matchTypesString, value) && CurrentConfig != null)
            {
      // Parse comma-separated string back to list
   CurrentConfig.MatchTypes = value
      .Split(',')
    .Select(s => s.Trim())
  .Where(s => !string.IsNullOrEmpty(s))
          .ToList();
         }
        }
    }

    // Collections exposed directly for reliable UI binding
    public ObservableCollection<ScoringElement> AutoElements { get; } = new();
 public ObservableCollection<ScoringElement> TeleopElements { get; } = new();
    public ObservableCollection<ScoringElement> EndgameElements { get; } = new();
 public ObservableCollection<RatingElement> PostMatchRatingElements { get; } = new();
    public ObservableCollection<TextElement> PostMatchTextElements { get; } = new();

    // Element being edited
    [ObservableProperty]
    private ScoringElement? currentElement;

    // Available element types
    public List<string> ElementTypes { get; } = new() { "counter", "boolean", "multiplechoice", "rating" };

    // Boolean default options
    public List<string> BooleanOptions { get; } = new() { "True", "False" };

    // API Source options
  public List<string> ApiSourceOptions { get; } = new() { "first", "tba", "both" };

    // Options for current multiple-choice element being edited
public ObservableCollection<ScoringOption> CurrentElementOptions { get; } = new();

    // Counts
    private int _autoCount;
    public int AutoCount { get => _autoCount; set => SetProperty(ref _autoCount, value); }

    private int _teleopCount;
    public int TeleopCount { get => _teleopCount; set => SetProperty(ref _teleopCount, value); }

    private int _endgameCount;
    public int EndgameCount { get => _endgameCount; set => SetProperty(ref _endgameCount, value); }

    private int _ratingCount;
    public int RatingCount { get => _ratingCount; set => SetProperty(ref _ratingCount, value); }

    private int _textCount;
    public int TextCount { get => _textCount; set => SetProperty(ref _textCount, value); }

    // Visibility helpers
    private bool _isRawVisible = true;
    public bool IsRawVisible { get => _isRawVisible; set => SetProperty(ref _isRawVisible, value); }

    private bool _isFormVisible = false;
    public bool IsFormVisible { get => _isFormVisible; set => SetProperty(ref _isFormVisible, value); }

    public GameConfigEditorViewModel(IApiService apiService, ISettingsService settingsService)
    {
        _api = apiService;
   _settings = settingsService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
    try
        {
            StatusMessage = "Loading...";
      var resp = await _api.GetGameConfigAsync();
      if (resp.Success && resp.Config != null)
  {
    NormalizeConfig(resp.Config);
CurrentConfig = resp.Config;
      JsonText = JsonSerializer.Serialize(resp.Config, _jsonOptions);
 PopulateCollections(resp.Config);
           UpdateStatusCounts();
        StatusMessage = "Loaded";
         }
else
            {
 StatusMessage = resp.Error ?? "Failed to load";
}
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task<bool> ParseJsonToModelAsync()
    {
 try
 {
 // Deserialize off the UI thread to avoid blocking
 GameConfig? cfg = null;
 await Task.Run(() =>
 {
 cfg = JsonSerializer.Deserialize<GameConfig>(JsonText, _jsonOptions);
 });

 if (cfg == null)
 {
 StatusMessage = "Invalid config JSON";
 return false;
 }

 // Normalize on background thread where safe (works on POCOs)
 NormalizeConfig(cfg);

 // Apply collections and UI-bound ObservableCollections on UI thread
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

 private void PopulateCollections(GameConfig cfg)
    {
        // Update match types string for UI display
        MatchTypesString = string.Join(", ", cfg.MatchTypes ?? new List<string>());
    
        AutoElements.Clear();
        foreach (var e in cfg.AutoPeriod?.ScoringElements ?? Enumerable.Empty<ScoringElement>())
        {
    // Normalize type value to match picker options
      e.Type = NormalizeType(e.Type);
  
    if (string.IsNullOrEmpty(e.Type))
      e.Type = "counter";
  
     if (e.Type == "multiplechoice" && e.Options == null)
            e.Options = new ObservableCollection<ScoringOption>();
       
     AutoElements.Add(e);
        }

        TeleopElements.Clear();
        foreach (var e in cfg.TeleopPeriod?.ScoringElements ?? Enumerable.Empty<ScoringElement>())
        {
            e.Type = NormalizeType(e.Type);
    
       if (string.IsNullOrEmpty(e.Type))
  e.Type = "counter";
     
          if (e.Type == "multiplechoice" && e.Options == null)
         e.Options = new ObservableCollection<ScoringOption>();
 
    TeleopElements.Add(e);
        }

        EndgameElements.Clear();
   foreach (var e in cfg.EndgamePeriod?.ScoringElements ?? Enumerable.Empty<ScoringElement>())
        {
            e.Type = NormalizeType(e.Type);
     
     if (string.IsNullOrEmpty(e.Type))
    e.Type = "counter";

      if (e.Type == "multiplechoice" && e.Options == null)
      e.Options = new ObservableCollection<ScoringOption>();
 
 EndgameElements.Add(e);
        }

        PostMatchRatingElements.Clear();
   foreach (var r in cfg.PostMatch?.RatingElements ?? Enumerable.Empty<RatingElement>())
      PostMatchRatingElements.Add(r);

PostMatchTextElements.Clear();
        foreach (var t in cfg.PostMatch?.TextElements ?? Enumerable.Empty<TextElement>())
            PostMatchTextElements.Add(t);

     AutoCount = AutoElements.Count;
        TeleopCount = TeleopElements.Count;
        EndgameCount = EndgameElements.Count;
        RatingCount = PostMatchRatingElements.Count;
        TextCount = PostMatchTextElements.Count;

#if DEBUG
        System.Diagnostics.Debug.WriteLine($"[GameConfigEditor] Populated: Auto={AutoCount}, Teleop={TeleopCount}, Endgame={EndgameCount}, Rating={RatingCount}, Text={TextCount}");
#endif
    }

    private void NormalizeConfig(GameConfig cfg)
    {
        if (cfg.AutoPeriod == null) cfg.AutoPeriod = new GamePeriod();
   if (cfg.TeleopPeriod == null) cfg.TeleopPeriod = new GamePeriod();
        if (cfg.EndgamePeriod == null) cfg.EndgamePeriod = new GamePeriod();
        if (cfg.PostMatch == null) cfg.PostMatch = new PostMatch();

        cfg.AutoPeriod.ScoringElements ??= new List<ScoringElement>();
      cfg.TeleopPeriod.ScoringElements ??= new List<ScoringElement>();
        cfg.EndgamePeriod.ScoringElements ??= new List<ScoringElement>();
        cfg.PostMatch.RatingElements ??= new List<RatingElement>();
        cfg.PostMatch.TextElements ??= new List<TextElement>();

        // Initialize API settings if null
        if (cfg.ApiSettings == null)
            cfg.ApiSettings = new FirstApiSettings();
  
        if (cfg.TbaApiSettings == null)
            cfg.TbaApiSettings = new TbaApiSettings();

  // Set default preferred API source if missing
      if (string.IsNullOrEmpty(cfg.PreferredApiSource))
       cfg.PreferredApiSource = "first";
    }

    private void UpdateStatusCounts()
    {
        if (CurrentConfig == null)
     {
  StatusMessage = "No config loaded";
    return;
        }

        StatusMessage = $"Auto:{AutoCount} Teleop:{TeleopCount} Endgame:{EndgameCount} Ratings:{RatingCount} Text:{TextCount}";
    }

    public void UpdateJsonFromModel()
  {
        if (CurrentConfig == null) return;
        try
     {
     SyncCollectionsToModel();
     ValidateElementTypes();
       DenormalizeTypes();
   
        JsonText = JsonSerializer.Serialize(CurrentConfig, _jsonOptions);
  StatusMessage = "Updated JSON from model";
        }
        catch (Exception ex)
     {
      StatusMessage = $"Failed to serialize model: {ex.Message}";
        }
    }
    
    private void ValidateElementTypes()
 {
        foreach (var element in AutoElements.Concat(TeleopElements).Concat(EndgameElements))
    {
            if (string.IsNullOrEmpty(element.Type))
              element.Type = "counter";
        }
    }

    private void DenormalizeTypes()
    {
      if (CurrentConfig == null) return;

    foreach (var element in CurrentConfig.AutoPeriod?.ScoringElements ?? Enumerable.Empty<ScoringElement>())
       element.Type = DenormalizeType(element.Type);
  
   foreach (var element in CurrentConfig.TeleopPeriod?.ScoringElements ?? Enumerable.Empty<ScoringElement>())
        element.Type = DenormalizeType(element.Type);
        
        foreach (var element in CurrentConfig.EndgamePeriod?.ScoringElements ?? Enumerable.Empty<ScoringElement>())
         element.Type = DenormalizeType(element.Type);
    }

 [RelayCommand]
    public async Task SaveAsync()
    {
  try
        {
     // If we're in Raw JSON view, parse the JSON first
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
           // If in Form view, sync collections to model
      SyncCollectionsToModel();
     }

 if (CurrentConfig == null)
     {
    StatusMessage = "No config to save";
      return;
            }

            // Denormalize types before saving
          DenormalizeTypes();
      
        // Save to server
 StatusMessage = "Saving...";
            var saveResp = await _api.SaveGameConfigAsync(CurrentConfig!);
       
    if (saveResp.Success)
    {
 StatusMessage = "Saved successfully";
     // Reload to get fresh data
      await LoadAsync();
 }
       else
  {
          StatusMessage = saveResp.Error ?? "Save failed";
    }
        }
        catch (JsonException jex)
        {
            StatusMessage = $"JSON error: {jex.Message}";
        }
  catch (Exception ex)
    {
  StatusMessage = $"Save error: {ex.Message}";
        }
    }

    private void SyncCollectionsToModel()
    {
        if (CurrentConfig == null) return;

        CurrentConfig.AutoPeriod.ScoringElements = AutoElements.ToList();
     CurrentConfig.TeleopPeriod.ScoringElements = TeleopElements.ToList();
        CurrentConfig.EndgamePeriod.ScoringElements = EndgameElements.ToList();
        CurrentConfig.PostMatch.RatingElements = PostMatchRatingElements.ToList();
   CurrentConfig.PostMatch.TextElements = PostMatchTextElements.ToList();
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
        
        // Switch views on UI thread
  IsRawVisible = true;
        IsFormVisible = false;

        StatusMessage = "Switched to Raw JSON";
    }

    public async Task<bool> ShowFormAsync()
    {
        StatusMessage = "Parsing JSON...";
        
  // Allow UI to update
        await Task.Delay(50);
  
        // CRITICAL FIX: Parse on UI thread, not background thread!
     // ParseJsonToModelAsync modifies ObservableCollections which must be on UI thread
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
    public void AddAutoElement()
    {
        var newElement = new ScoringElement
        {
         Id = $"auto_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
     Name = "New Element",
   Type = "counter",
        Points = 1,
            Default = 0
  };
        AutoElements.Add(newElement);
      AutoCount = AutoElements.Count;
        StatusMessage = "Added new Auto element";
}

    [RelayCommand]
    public void AddTeleopElement()
    {
  var newElement = new ScoringElement
        {
          Id = $"teleop_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
        Name = "New Element",
            Type = "counter",
            Points = 1,
 Default = 0
        };
        TeleopElements.Add(newElement);
 TeleopCount = TeleopElements.Count;
        StatusMessage = "Added new Teleop element";
    }

    [RelayCommand]
    public void AddEndgameElement()
    {
        var newElement = new ScoringElement
        {
    Id = $"endgame_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
            Name = "New Element",
  Type = "counter",
            Points = 1,
   Default = 0
        };
    EndgameElements.Add(newElement);
      EndgameCount = EndgameElements.Count;
        StatusMessage = "Added new Endgame element";
    }

    [RelayCommand]
public void AddRatingElement()
    {
        var newElement = new RatingElement
        {
       Id = $"rating_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
            Name = "New Rating",
            Type = "rating",
        Default = 3,
   Min = 1,
       Max = 5
    };
        PostMatchRatingElements.Add(newElement);
     RatingCount = PostMatchRatingElements.Count;
        StatusMessage = "Added new Rating element";
    }

    [RelayCommand]
    public void AddTextElement()
    {
        var newElement = new TextElement
        {
      Id = $"text_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
   Name = "New Text Field",
            Type = "text",
            Multiline = false
};
        PostMatchTextElements.Add(newElement);
        TextCount = PostMatchTextElements.Count;
        StatusMessage = "Added new Text element";
    }

  [RelayCommand]
    public void DeleteRatingElement(RatingElement element)
    {
    if (PostMatchRatingElements.Contains(element))
   {
            PostMatchRatingElements.Remove(element);
   RatingCount = PostMatchRatingElements.Count;
            StatusMessage = $"Deleted rating: {element.Name}";
 }
    }

    [RelayCommand]
    public void DeleteTextElement(TextElement element)
    {
    if (PostMatchTextElements.Contains(element))
    {
    PostMatchTextElements.Remove(element);
          TextCount = PostMatchTextElements.Count;
  StatusMessage = $"Deleted text field: {element.Name}";
     }
    }

[RelayCommand]
    public void DeleteElement(ScoringElement element)
    {
        if (AutoElements.Contains(element))
        {
            AutoElements.Remove(element);
       AutoCount = AutoElements.Count;
        }
        else if (TeleopElements.Contains(element))
        {
            TeleopElements.Remove(element);
            TeleopCount = TeleopElements.Count;
    }
     else if (EndgameElements.Contains(element))
        {
            EndgameElements.Remove(element);
    EndgameCount = EndgameElements.Count;
        }
        StatusMessage = $"Deleted element: {element.Name}";
    }

    [RelayCommand]
 public void AddOptionToElement(ScoringElement element)
 {
        if (element == null || element.Type != "multiplechoice") return;

        element.Options ??= new ObservableCollection<ScoringOption>();
      var newOption = new ScoringOption
        {
            Name = $"Option {element.Options.Count + 1}",
    Points = 0
        };
        element.Options.Add(newOption);
  
        StatusMessage = $"Added option to {element.Name}";
  }

    [RelayCommand]
    public void DeleteOptionFromElement(ScoringOption option)
    {
        if (option == null) return;
 
     foreach (var element in AutoElements.Concat(TeleopElements).Concat(EndgameElements))
        {
    if (element.Options != null && element.Options.Contains(option))
         {
         element.Options.Remove(option);
   StatusMessage = $"Deleted option from {element.Name}";
             return;
       }
        }
    }

    /// <summary>
    /// Normalize type values from JSON format to UI format
    /// </summary>
    private string NormalizeType(string type)
    {
        if (string.IsNullOrEmpty(type))
            return "counter";

        return type.ToLower() switch
        {
            "multiple_choice" => "multiplechoice",
    "multiplechoice" => "multiplechoice",
            "counter" => "counter",
            "boolean" => "boolean",
            "rating" => "rating",
         _ => "counter"
     };
    }

/// <summary>
    /// Convert type from UI format to JSON format
    /// </summary>
    private string DenormalizeType(string type)
    {
      if (string.IsNullOrEmpty(type))
        return "counter";
   
        return type.ToLower() switch
        {
     "multiplechoice" => "multiple_choice",
            "multiple_choice" => "multiple_choice",
       "counter" => "counter",
       "boolean" => "boolean",
            "rating" => "rating",
            _ => "counter"
        };
    }
}
