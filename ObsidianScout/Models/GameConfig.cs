using System.Text.Json.Serialization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ObsidianScout.Models;

// Custom converter to handle List<ScoringOption> -> ObservableCollection<ScoringOption>
public class ScoringOptionCollectionConverter : JsonConverter<ObservableCollection<ScoringOption>?>
{
    public override ObservableCollection<ScoringOption>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
 {
     if (reader.TokenType == JsonTokenType.Null)
            return null;

        var list = JsonSerializer.Deserialize<List<ScoringOption>>(ref reader, options);
        return list == null ? null : new ObservableCollection<ScoringOption>(list);
    }

    public override void Write(Utf8JsonWriter writer, ObservableCollection<ScoringOption>? value, JsonSerializerOptions options)
    {
if (value == null)
        {
            writer.WriteNullValue();
          return;
        }

  JsonSerializer.Serialize(writer, value.ToList(), options);
    }
}

public class GameConfig
{
    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("game_name")]
    public string GameName { get; set; } = string.Empty;

    [JsonPropertyName("alliance_size")]
  public int AllianceSize { get; set; }

    [JsonPropertyName("match_types")]
    public List<string> MatchTypes { get; set; } = new();

[JsonPropertyName("current_event_code")]
    public string CurrentEventCode { get; set; } = string.Empty;

    [JsonPropertyName("auto_period")]
    public GamePeriod? AutoPeriod { get; set; }

    [JsonPropertyName("teleop_period")]
    public GamePeriod? TeleopPeriod { get; set; }

    [JsonPropertyName("endgame_period")]
    public GamePeriod? EndgamePeriod { get; set; }

    [JsonPropertyName("post_match")]
    public PostMatch? PostMatch { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

  // NEW: Nested API Settings
    [JsonPropertyName("api_settings")]
    public FirstApiSettings? ApiSettings { get; set; }

    [JsonPropertyName("tba_api_settings")]
  public TbaApiSettings? TbaApiSettings { get; set; }

    [JsonPropertyName("preferred_api_source")]
    public string? PreferredApiSource { get; set; }
}

// NEW: FIRST API Settings Model
public class FirstApiSettings
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("auth_token")]
    public string? AuthToken { get; set; }

    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = "https://frc-api.firstinspires.org";

    [JsonPropertyName("auto_sync_enabled")]
    public bool AutoSyncEnabled { get; set; } = true;
}

// NEW: TBA API Settings Model
public class TbaApiSettings
{
    [JsonPropertyName("auth_key")]
    public string? AuthKey { get; set; }

    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = "https://www.thebluealliance.com/api/v3";
}

public class GamePeriod
{
    [JsonPropertyName("duration_seconds")]
    public int DurationSeconds { get; set; }

    [JsonPropertyName("scoring_elements")]
    public List<ScoringElement> ScoringElements { get; set; } = new();
}

public class ScoringElement : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _permId = string.Empty;
    private string _name = string.Empty;
    private string _type = "counter"; // Default value - never null initially
    private double _points;
    private object? _default;
    private bool _displayInPredictions;
    private ObservableCollection<ScoringOption>? _options;
    private int? _min;
    private int? _max;
    private int _step = 1;
    private int? _altStep;
    private bool _altStepEnabled;

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
        set 
        { 
   // Allow all values to be set, normalization happens in ViewModel
     // Only reject completely null values
            if (value != null)
    {
    _type = value; 
       OnPropertyChanged(); 
 OnPropertyChanged(nameof(IsMultipleChoice)); 
  }
        }
    }

    [JsonPropertyName("points")]
    public double Points
    {
        get => _points;
        set { _points = value; OnPropertyChanged(); }
}

    [JsonPropertyName("default")]
    public object? Default
    {
        get => _default;
        set { _default = value; OnPropertyChanged(); }
    }

  [JsonPropertyName("display_in_predictions")]
    public bool DisplayInPredictions
    {
      get => _displayInPredictions;
      set { _displayInPredictions = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("options")]
    [JsonConverter(typeof(ScoringOptionCollectionConverter))]
    public ObservableCollection<ScoringOption>? Options
    {
   get => _options;
        set { _options = value; OnPropertyChanged(); }
    }

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

    [JsonPropertyName("step")]
    public int Step
    {
        get => _step;
        set { _step = value <= 0 ? 1 : value; OnPropertyChanged(); }
    }

    [JsonPropertyName("alt_step")]
    public int? AltStep
    {
        get => _altStep;
        set { _altStep = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("alt_step_enabled")]
    public bool AltStepEnabled
    {
        get => _altStepEnabled;
        set { _altStepEnabled = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public bool IsMultipleChoice => Type == "multiplechoice";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ScoringOption : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private double _points;

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("points")]
    public double Points
    {
        get => _points;
        set { _points = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PostMatch
{
    [JsonPropertyName("rating_elements")]
    public List<RatingElement> RatingElements { get; set; } = new();

    [JsonPropertyName("text_elements")]
    public List<TextElement> TextElements { get; set; } = new();
}

public class RatingElement : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _type = string.Empty;
    private int _default;
    private int _min;
    private int _max;

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

    [JsonPropertyName("type")]
    public string Type
    {
get => _type;
        set { _type = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("default")]
    public int Default
    {
     get => _default;
        set { _default = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("min")]
    public int Min
    {
        get => _min;
     set { _min = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("max")]
    public int Max
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

public class TextElement : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _type = string.Empty;
    private bool _multiline;

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

    [JsonPropertyName("type")]
    public string Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("multiline")]
    public bool Multiline
    {
        get => _multiline;
        set { _multiline = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class GameConfigResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("config")]
    public GameConfig? Config { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
