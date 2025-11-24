using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ObsidianScout.Models;

public class Match
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("match_number")]
    [JsonConverter(typeof(SafeIntJsonConverter))]
    public int MatchNumber { get; set; }
    
    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = string.Empty;
    
    [JsonPropertyName("red_alliance")]
    public string RedAlliance { get; set; } = string.Empty;
    
    [JsonPropertyName("blue_alliance")]
    public string BlueAlliance { get; set; } = string.Empty;
    
    [JsonPropertyName("red_score")]
    [JsonConverter(typeof(SafeNullableIntJsonConverter))]
    public int? RedScore { get; set; }
    
    [JsonPropertyName("blue_score")]
    [JsonConverter(typeof(SafeNullableIntJsonConverter))]
    public int? BlueScore { get; set; }
    
    [JsonPropertyName("winner")]
    public string? Winner { get; set; }

    [JsonPropertyName("scheduled_time")]
    public DateTime? ScheduledTime { get; set; }

    [JsonPropertyName("predicted_time")]
    public DateTime? PredictedTime { get; set; }

    [JsonPropertyName("actual_time")]
    public DateTime? ActualTime { get; set; }

    // Match type ordering: Practice, Qualification, Quarterfinals, Semifinals, Finals, Playoff
    public int MatchTypeOrder
    {
        get
        {
 var matchTypeLower = MatchType.ToLowerInvariant();
            return matchTypeLower switch
 {
       "practice" => 1,
                "qualification" => 2,
   "quarterfinal" => 3,
    "quarterfinals" => 3,
     "semifinal" => 4,
    "semifinals" => 4,
     "final" => 5,
            "finals" => 5,
           "playoff" => 6,
  "playoffs" => 6,
 _ => 999 // Unknown types go last
     };
        }
    }

    // Computed properties for display
    public string RedTeams => RedAlliance.Replace(",", ", ");
    public string BlueTeams => BlueAlliance.Replace(",", ", ");
    
    public string TimeDisplay
    {
        get
  {
    var time = PredictedTime ?? ScheduledTime;
      if (time.HasValue)
     {
    var localTime = time.Value.ToLocalTime();
    return localTime.ToString("h:mm tt");
            }
  return "TBD";
        }
    }

    public string DateDisplay
    {
        get
        {
     var time = PredictedTime ?? ScheduledTime;
            if (time.HasValue)
     {
     var localTime = time.Value.ToLocalTime();
       return localTime.ToString("ddd, MMM d");
        }
  return "";
    }
    }

    public bool HasTime => ScheduledTime.HasValue || PredictedTime.HasValue;

    public bool IsUpcoming
    {
        get
        {
var time = PredictedTime ?? ScheduledTime;
            return time.HasValue && time.Value > DateTime.UtcNow;
        }
    }

    public bool IsInProgress
    {
     get
     {
  var time = PredictedTime ?? ScheduledTime;
      if (!time.HasValue) return false;
            var now = DateTime.UtcNow;
     // Match is in progress if within 10 minutes of scheduled time
      return time.Value <= now && time.Value.AddMinutes(10) >= now;
        }
    }

    public bool IsCompleted => RedScore.HasValue || BlueScore.HasValue || !string.IsNullOrEmpty(Winner);
}

// Custom JSON converter to safely handle integer conversion
public class SafeIntJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
    {
          switch (reader.TokenType)
            {
          case JsonTokenType.Number:
 return reader.GetInt32();
                case JsonTokenType.String:
    var stringValue = reader.GetString();
           if (int.TryParse(stringValue, out int result))
           return result;
 // Try to extract first integer from formats like "3-1" or "QF3-1"
 if (!string.IsNullOrWhiteSpace(stringValue))
 {
 var m = Regex.Match(stringValue, @"\d+");
 if (m.Success && int.TryParse(m.Value, out result))
 return result;
 }
 System.Diagnostics.Debug.WriteLine($"[SafeIntConverter] Could not parse '{stringValue}' as int, defaulting to 0");
      return 0;
      case JsonTokenType.Null:
        return 0;
      default:
      System.Diagnostics.Debug.WriteLine($"[SafeIntConverter] Unexpected token type {reader.TokenType}, defaulting to 0");
     return 0;
            }
        }
        catch (Exception ex)
 {
            System.Diagnostics.Debug.WriteLine($"[SafeIntConverter] Exception: {ex.Message}, defaulting to 0");
            return 0;
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

// Custom JSON converter for nullable integers
public class SafeNullableIntJsonConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
 switch (reader.TokenType)
    {
         case JsonTokenType.Number:
                return reader.GetInt32();
      case JsonTokenType.String:
   var stringValue = reader.GetString();
        if (string.IsNullOrWhiteSpace(stringValue))
return null;
          if (int.TryParse(stringValue, out int result))
      return result;
 // Try to extract first integer from strings like "3-1"
 var m = Regex.Match(stringValue ?? string.Empty, @"\d+");
 if (m.Success && int.TryParse(m.Value, out result))
 return result;
 System.Diagnostics.Debug.WriteLine($"[SafeNullableIntConverter] Could not parse '{stringValue}' as int, returning null");
 return null;
            case JsonTokenType.Null:
      return null;
       default:
    System.Diagnostics.Debug.WriteLine($"[SafeNullableIntConverter] Unexpected token type {reader.TokenType}, returning null");
 return null;
            }
     }
        catch (Exception ex)
        {
     System.Diagnostics.Debug.WriteLine($"[SafeNullableIntConverter] Exception: {ex.Message}, returning null");
return null;
        }
}

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
     if (value.HasValue)
            writer.WriteNumberValue(value.Value);
  else
     writer.WriteNullValue();
    }
}

public class MatchesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("matches")]
    public List<Match> Matches { get; set; } = new();
    
 [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
