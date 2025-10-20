# JSON Deserialization Fix - Matches Now Loading!

## Issue Identified

The matches API was returning data correctly, but the app couldn't parse it because of missing `JsonPropertyName` attributes.

### The Problem

**Server Response (working):**
```json
{
  "success": true,
  "count": 32,
  "matches": [
    {
      "id": 528,
      "match_number": 1,
      "match_type": "Qualification",
      "red_alliance": "323,5454,5045",
      "blue_alliance": "2357,6424,3937",
      "red_score": 64,
      "blue_score": 122,
      "winner": "blue"
    }
  ]
}
```

**C# Model (broken):**
```csharp
public class MatchesResponse
{
    public bool Success { get; set; }  // Expecting "Success" but JSON has "success"
    public List<Match> Matches { get; set; }
    public int Count { get; set; }
}
```

The `JsonNamingPolicy.SnakeCaseLower` was not handling the initial capital letter conversion properly.

## Solution

Added `JsonPropertyName` attributes to explicitly map JSON fields to C# properties:

### Match.cs - Fixed

```csharp
using System.Text.Json.Serialization;

public class Match
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("match_number")]
    public int MatchNumber { get; set; }
    
    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = string.Empty;
    
    [JsonPropertyName("red_alliance")]
    public string RedAlliance { get; set; } = string.Empty;
    
    [JsonPropertyName("blue_alliance")]
    public string BlueAlliance { get; set; } = string.Empty;
    
    [JsonPropertyName("red_score")]
    public int? RedScore { get; set; }
    
    [JsonPropertyName("blue_score")]
    public int? BlueScore { get; set; }
    
    [JsonPropertyName("winner")]
    public string? Winner { get; set; }
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
```

### ApiService.cs - Enhanced

Added better error handling to `GetMatchesAsync`:
- HTTP error responses with details
- Network error handling  
- Invalid response format detection

## What This Fixes

? **JSON Parsing** - Correctly deserializes `"success"` ? `Success`  
? **Match Data** - All 32 matches will load properly  
? **Field Mapping** - `"match_number"` ? `MatchNumber`, etc.  
? **Error Handling** - Shows specific errors if something fails

## Expected Behavior Now

### Step 1: Tap "Load" Button
Status shows:
```
?? Looking for event: arsea
?? Searching X events...
?? Loading matches for Arkansas Regional...
? Loaded 32 matches for Arkansas Regional
```

### Step 2: Open Match Picker
You should now see all matches:
```
Qualification 1
Qualification 2
Qualification 3
...
Qualification 24
Playoff 1
Playoff 2
...
Playoff 8
```

### Step 3: Select a Match
Pick a match and start scouting!

## JSON to C# Mapping

| JSON Field | C# Property | Type |
|------------|-------------|------|
| `id` | `Id` | `int` |
| `match_number` | `MatchNumber` | `int` |
| `match_type` | `MatchType` | `string` |
| `red_alliance` | `RedAlliance` | `string` |
| `blue_alliance` | `BlueAlliance` | `string` |
| `red_score` | `RedScore` | `int?` (nullable) |
| `blue_score` | `BlueScore` | `int?` (nullable) |
| `winner` | `Winner` | `string?` (nullable) |

## Display Format

Matches will display as:
- **Qualification matches:** `"Qualification 1"`, `"Qualification 2"`, etc.
- **Playoff matches:** `"Playoff 1"`, `"Playoff 2"`, etc.

Sorted by:
1. Match type (Qualification, then Playoff)
2. Match number (1, 2, 3...)

## Null Values Handled

Notice in your JSON that Playoff match 8 has:
```json
{
  "blue_score": null,
  "red_score": null,
  "winner": null
}
```

The `int?` and `string?` types handle these nulls gracefully - no crashes!

## Build Status

? **No compilation errors**  
? **JSON attributes added**  
? **Error handling enhanced**  
? **Ready to test!**

## Testing Checklist

- [ ] Tap "Load" button
- [ ] See success message with count
- [ ] Open Match picker
- [ ] See all 32 matches listed
- [ ] Matches are sorted correctly
- [ ] Can select a match
- [ ] Match selection sets MatchId

## Summary

The issue was **JSON deserialization**, not the API! The server was returning data correctly, but the C# models couldn't parse it because:

1. ? Missing `JsonPropertyName` attributes
2. ? Relying only on `SnakeCaseLower` policy
3. ? Now explicitly mapped with attributes

**Result:** Matches should load perfectly now! ??

Run the app, tap "Load", and watch those 32 matches appear!
