using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ObsidianScout.Models;

public class PitConfig
{
    [JsonPropertyName("pit_scouting")]
    public PitScoutingConfig? PitScouting { get; set; }
}

public class PitScoutingConfig
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("sections")]
    public List<PitSection> Sections { get; set; } = new();
}

public class PitSection : INotifyPropertyChanged
{
  private string _id = string.Empty;
 private string _name = string.Empty;
    private System.Collections.ObjectModel.ObservableCollection<PitElement> _elements = new();

    [JsonPropertyName("id")]
    public string Id
    {
     get => _id;
  set { _id = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("name")]
    public string Name
    {
     get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("elements")]
    public System.Collections.ObjectModel.ObservableCollection<PitElement> Elements
    {
        get => _elements;
        set { _elements = value; OnPropertyChanged(); }
    }

  public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PitElement : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _permId = string.Empty;
    private string _name = string.Empty;
    private string _type = "text";
    private bool _required;
    private PitValidation? _validation;
    private System.Collections.ObjectModel.ObservableCollection<PitOption>? _options;
    private string? _placeholder;
    private object? _defaultValue;

    // New: per-element available types for Picker ItemsSource
    private System.Collections.ObjectModel.ObservableCollection<string>? _availableTypes;
    private int _selectedIndex = -1;
    private bool _suppressSelectedIndexChange = false;

  [JsonPropertyName("id")]
    public string Id
    {
    get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("perm_id")]
    public string PermId
    {
        get => _permId;
        set { _permId = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("type")]
    public string Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNumber)); OnPropertyChanged(nameof(IsText)); OnPropertyChanged(nameof(IsTextArea)); OnPropertyChanged(nameof(IsBoolean)); OnPropertyChanged(nameof(IsSelect)); OnPropertyChanged(nameof(IsMultiSelect)); }
    }

    [JsonPropertyName("required")]
    public bool Required
    {
  get => _required;
  set { _required = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("validation")]
  public PitValidation? Validation
    {
  get => _validation;
        set { _validation = value; OnPropertyChanged(); }
 }

    [JsonPropertyName("options")]
    public System.Collections.ObjectModel.ObservableCollection<PitOption>? Options
    {
        get => _options;
     set { _options = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("placeholder")]
    public string? Placeholder
    {
        get => _placeholder;
        set { _placeholder = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("default")]
    public object? DefaultValue
    {
        get => _defaultValue;
        set { _defaultValue = value; OnPropertyChanged(); }
  }

    // Expose available types for Picker binding
    [JsonIgnore]
    public System.Collections.ObjectModel.ObservableCollection<string>? AvailableTypes
    {
      get => _availableTypes;
      set { _availableTypes = value; OnPropertyChanged(); }
    }

    // SelectedIndex for Picker binding to avoid SelectedItem equality issues
    [JsonIgnore]
    public int SelectedIndex
  {
        get => _selectedIndex;
        set
        {
if (_selectedIndex == value) return;
            _selectedIndex = value;
        OnPropertyChanged();
            try
            {
      if (!_suppressSelectedIndexChange)
          {
     if (_availableTypes != null && _selectedIndex >=0 && _selectedIndex < _availableTypes.Count)
          {
        // Update Type to the selected available type
    Type = _availableTypes[_selectedIndex];
     }
      }
 }
          catch { }
        }
    }

    // Helper to set SelectedIndex without triggering Type update
    public void SetSelectedIndexSilently(int idx)
    {
        try
        {
         _suppressSelectedIndexChange = true;
            SelectedIndex = idx;
        }
        finally
      {
      _suppressSelectedIndexChange = false;
      }
    }

    [JsonIgnore]
    public bool IsNumber => Type == "number";

 [JsonIgnore]
    public bool IsText => Type == "text";

    [JsonIgnore]
    public bool IsTextArea => Type == "textarea";

    [JsonIgnore]
    public bool IsBoolean => Type == "boolean";

    [JsonIgnore]
    public bool IsSelect => Type == "select";

    [JsonIgnore]
    public bool IsMultiSelect => Type == "multiselect";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
   PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PitValidation : INotifyPropertyChanged
{
    private int? _min;
    private int? _max;

    [JsonPropertyName("min")]
    public int? Min
    {
 get => _min;
     set { _min = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("max")]
    public int? Max
    {
 get => _max;
      set { _max = value; OnPropertyChanged(); }
  }

 public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PitOption : INotifyPropertyChanged
{
    private string _value = string.Empty;
    private string _label = string.Empty;

    [JsonPropertyName("value")]
    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("label")]
    public string Label
    {
        get => _label;
  set { _label = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PitConfigResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("config")]
    public PitConfig? Config { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class PitScoutingSubmission
{
    [JsonPropertyName("team_id")]
    public int TeamId { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object?> Data { get; set; } = new();

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }
    
}

public class PitScoutingSubmitResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("pit_scouting_id")]
    public int PitScoutingId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }
}

// NEW: Pit Scouting Entry Model
public class PitScoutingEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("team_id")]
    public int TeamId { get; set; }

    [JsonPropertyName("team_number")]
    public int TeamNumber { get; set; }

    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public Dictionary<string, object?>? Data { get; set; }

    [JsonPropertyName("scout_name")]
    public string ScoutName { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    // UI state
    [JsonIgnore]
    public bool HasLocalChanges { get; set; } = false;

    [JsonIgnore]
    public bool UploadInProgress { get; set; } = false;

    [JsonIgnore]
    public string UploadStatus
    {
        get
        {
            if (UploadInProgress) return "Uploading...";
            if (HasLocalChanges) return "Modified (not uploaded)";
            if (Id > 0) return "Uploaded";
            return "Pending";
        }
    }

    [JsonIgnore]
    public bool IsUploaded => Id > 0;

    [JsonIgnore]
    public bool IsPending => Id == 0;

    [JsonIgnore]
    public bool CanUpload => !IsUploaded && !IsPending;

    [JsonIgnore]
    public bool CanEdit => !IsUploaded && !IsPending;
}

// NEW: Pit Scouting List Response
public class PitScoutingListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("entries")]
    public List<PitScoutingEntry> Entries { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
