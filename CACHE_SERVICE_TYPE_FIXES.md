# Cache Service Type Fixes

## Problem
Compilation errors due to incorrect type usage in the CacheService:
- `MetricDefinition` was incorrectly referenced as `string` 
- `ScoutingEntry` was incorrectly referenced as `ScoutingData`

## Errors Fixed

### Type Mismatch Errors (6 total)
1. **Available Metrics Interface**: Changed from `List<string>` to `List<MetricDefinition>`
2. **Available Metrics Get Method**: Changed return type from `List<string>?` to `List<MetricDefinition>?`
3. **Available Metrics Cache Method**: Changed parameter from `List<string>` to `List<MetricDefinition>`
4. **Scouting Data Interface**: Changed from `List<ScoutingData>` to `List<ScoutingEntry>`
5. **Scouting Data Get Method**: Changed return type from `List<ScoutingData>?` to `List<ScoutingEntry>?`
6. **Scouting Data Cache Method**: Changed parameter from `List<ScoutingData>` to `List<ScoutingEntry>`

## Changes Made

### File: `ObsidianScout/Services/CacheService.cs`

#### Interface Update
```csharp
// BEFORE (incorrect)
Task<List<string>?> GetCachedAvailableMetricsAsync();
Task CacheAvailableMetricsAsync(List<string> metrics);
Task<List<ScoutingData>?> GetCachedScoutingDataAsync();
Task CacheScoutingDataAsync(List<ScoutingData> scoutingData);

// AFTER (correct)
Task<List<MetricDefinition>?> GetCachedAvailableMetricsAsync();
Task CacheAvailableMetricsAsync(List<MetricDefinition> metrics);
Task<List<ScoutingEntry>?> GetCachedScoutingDataAsync();
Task CacheScoutingDataAsync(List<ScoutingEntry> scoutingData);
```

#### Implementation Update
```csharp
// Available Metrics methods now use MetricDefinition
public async Task<List<MetricDefinition>?> GetCachedAvailableMetricsAsync()
{
    var metrics = JsonSerializer.Deserialize<List<MetricDefinition>>(json, _jsonOptions);
    // ...
}

public async Task CacheAvailableMetricsAsync(List<MetricDefinition> metrics)
{
    var json = JsonSerializer.Serialize(metrics, _jsonOptions);
    // ...
}

// Scouting Data methods now use ScoutingEntry
public async Task<List<ScoutingEntry>?> GetCachedScoutingDataAsync()
{
    var data = JsonSerializer.Deserialize<List<ScoutingEntry>>(json, _jsonOptions);
    // ...
}

public async Task CacheScoutingDataAsync(List<ScoutingEntry> scoutingData)
{
    var json = JsonSerializer.Serialize(scoutingData, _jsonOptions);
    // ...
}
```

## Model Definitions (Confirmed)

### MetricDefinition
Defined in `ObsidianScout/Models/TeamMetrics.cs`:
```csharp
public class MetricDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("higher_is_better")]
    public bool HigherIsBetter { get; set; }
}
```

### ScoutingEntry  
Defined in `ObsidianScout/Models/ScoutingData.cs`:
```csharp
public class ScoutingEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("team_id")]
    public int TeamId { get; set; }

    [JsonPropertyName("team_number")]
    public int TeamNumber { get; set; }

    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;

    [JsonPropertyName("match_id")]
    public int MatchId { get; set; }

    [JsonPropertyName("match_number")]
    public int MatchNumber { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();
    
    // ... additional properties
}
```

### ScoutingData (Different Class)
Also in `ObsidianScout/Models/ScoutingData.cs`:
```csharp
public class ScoutingData
{
    // Game-specific scoring fields
    [JsonPropertyName("auto_speaker_scored")]
    public int AutoSpeakerScored { get; set; }
    // ...
}
```

**Note**: `ScoutingData` is a game-specific data structure, while `ScoutingEntry` is the full scouting entry with metadata (team, match, timestamp, etc.) AND a generic `Data` dictionary for flexible game configurations.

## Verification

Build completed successfully with no errors:
- ? All type mismatches resolved
- ? Proper serialization/deserialization types
- ? Interface matches implementation
- ? No warnings about type conversions

## Related Systems

These cache methods are used by:
- **ApiService**: Caches API responses for offline functionality
- **GraphsViewModel**: Uses metrics definitions for dropdown
- **Various ViewModels**: Use scouting entries for data display

## Best Practices Followed

1. **Strong Typing**: Use specific model types instead of primitive types
2. **Consistency**: Interface and implementation use the same types
3. **JSON Serialization**: Types match what's expected from the API
4. **Documentation**: Clear naming distinguishes `ScoutingData` from `ScoutingEntry`
