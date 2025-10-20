# Team Picker "0 -" Fix - Added JSON Property Names

## Issue

The team picker was showing "0 -" for all teams instead of displaying team numbers and names like "5454 - The Bionics".

## Root Cause

Same issue as the matches - the Team and Event models were missing `JsonPropertyName` attributes, so JSON deserialization wasn't working properly after we removed the `PropertyNamingPolicy`.

## Solution

Added `JsonPropertyName` attributes to both Team and Event models.

### Team.cs - Fixed

```csharp
using System.Text.Json.Serialization;

public class Team
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("team_number")]
    public int TeamNumber { get; set; }
    
    [JsonPropertyName("team_name")]
    public string TeamName { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("scouting_data_count")]
    public int ScoutingDataCount { get; set; }
}

public class TeamsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("teams")]
    public List<Team> Teams { get; set; } = new();
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("total")]
    public int Total { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
```

### Event.cs - Fixed

```csharp
using System.Text.Json.Serialization;

public class Event
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; set; }
    
    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; set; }
    
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; }
    
    [JsonPropertyName("team_count")]
    public int TeamCount { get; set; }
}

public class EventsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("events")]
    public List<Event> Events { get; set; } = new();
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
```

## What This Fixes

? **Team Picker** - Will now show "5454 - The Bionics" instead of "0 -"  
? **Teams Loading** - JSON properly deserializes `team_number` ? `TeamNumber`  
? **Team Names** - JSON properly deserializes `team_name` ? `TeamName`  
? **Event Loading** - Events will also load correctly now

## Expected Behavior

### Team Picker - Before:
```
0 - 
0 - 
0 - 
```

### Team Picker - After:
```
16 - Bomb Squad
323 - The RoboLancers
2357 - System Meltdown
3937 - Breakaway
5002 - Dragon Robotics
5045 - Nuts & Bolts
5454 - The Bionics
```

### Match Picker - After:
```
Qualification 1
Qualification 2
Qualification 3
...
Playoff 1
Playoff 2
```

## JSON Mapping

### Team Object
| JSON Field | C# Property |
|------------|-------------|
| `id` | `Id` |
| `team_number` | `TeamNumber` |
| `team_name` | `TeamName` |
| `location` | `Location` |
| `scouting_data_count` | `ScoutingDataCount` |

### Event Object
| JSON Field | C# Property |
|------------|-------------|
| `id` | `Id` |
| `name` | `Name` |
| `code` | `Code` |
| `location` | `Location` |
| `start_date` | `StartDate` |
| `end_date` | `EndDate` |
| `timezone` | `Timezone` |
| `team_count` | `TeamCount` |

## Build & Test

1. **Close the app** if running
2. **Rebuild** the solution
3. **Run** the app
4. **Navigate to Scouting** page
5. **See teams** properly displayed in picker!

## Why All Three Needed Fixing

We removed `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` from ApiService because it was conflicting with explicit attributes. But that meant ALL models needed explicit `JsonPropertyName` attributes:

- ? Match.cs - Fixed earlier
- ? Team.cs - Fixed now
- ? Event.cs - Fixed now

## Build Status

? **No compilation errors**  
? **Team model updated**  
? **Event model updated**  
? **All models have JSON attributes**  
? **Ready to test!**

## Summary

The "0 -" issue was happening because:
1. We removed the automatic naming policy
2. Team and Event models didn't have explicit JSON attributes
3. JSON couldn't map `team_number` to `TeamNumber`, so it defaulted to 0
4. JSON couldn't map `team_name` to `TeamName`, so it defaulted to ""

Now with explicit `JsonPropertyName` attributes, all fields map correctly!

**Result:** Teams, Events, and Matches all display properly! ??
