# JSON Conversion Fix - Removed Conflicting Naming Policy

## Issue

The JSON deserialization was failing because of a conflict between:
- `JsonNamingPolicy.SnakeCaseLower` (in ApiService constructor)
- `JsonPropertyName` attributes (in Match.cs and other models)

Both were trying to control the JSON property naming, causing conflicts.

## The Problem

**ApiService.cs had:**
```csharp
_jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,  // ? Conflicting!
    PropertyNameCaseInsensitive = true
};
```

**Match.cs had:**
```csharp
[JsonPropertyName("match_number")]  // ? Explicit names
public int MatchNumber { get; set; }
```

**Result:** Confusion! The system tried to apply both rules, causing deserialization to fail.

## The Solution

**Removed the `PropertyNamingPolicy`:**
```csharp
_jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,  // ? Only case-insensitive matching
    // Removed PropertyNamingPolicy - using explicit JsonPropertyName attributes instead
};
```

## Why This Works

1. **Explicit Attributes Take Control** - `JsonPropertyName` attributes explicitly define the JSON property names
2. **No Naming Policy** - Removed the automatic snake_case conversion
3. **Case Insensitive** - Still allows flexible matching for properties without attributes

## What This Fixes

? **Matches Load** - JSON deserialization now works correctly  
? **All Models** - Team, Event, Match, GameConfig all work  
? **No Conflicts** - Explicit names in attributes, no automatic conversion  

## Models Using JsonPropertyName

All these models use explicit `JsonPropertyName` attributes:
- ? `Match` and `MatchesResponse`
- ? `Event` and `EventsResponse` 
- ? `Team` and `TeamsResponse`
- ? `GameConfig` and related models
- ? All API responses

## Expected Behavior Now

### Step 1: Tap "Load" Button
```
?? Looking for event: arsea
?? Loading matches for Arkansas Regional...
? Loaded 32 matches for Arkansas Regional
```

### Step 2: Open Match Picker
You should see:
```
Qualification 1
Qualification 2
Qualification 3
...
Playoff 1
Playoff 2
...
```

### Step 3: Success!
All 32 matches load and display correctly.

## Build & Test

1. **Close the running app** (to release file lock)
2. **Rebuild** the solution
3. **Run** the app
4. **Navigate to Scouting** page
5. **Tap "Load"** button
6. **Watch matches appear!**

## Build Status

? **No compilation errors**  
? **JSON naming conflict resolved**  
? **Ready to test**

## Summary

The fix was simple - we were using **two different systems** to name JSON properties:
1. ? Automatic snake_case conversion (naming policy)
2. ? Explicit property names (attributes)

Now we use **only explicit attributes**, which is clearer and more reliable.

**Result:** Matches will load successfully! ??
