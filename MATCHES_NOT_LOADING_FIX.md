# Matches Not Loading - Troubleshooting Guide

## Issue Fixed

Matches weren't loading when the "Load Matches" button was clicked. Enhanced error handling and status messages to identify and fix the issue.

## Changes Made

### 1. Enhanced LoadMatchesAsync() Method

Added comprehensive error handling and status messages:

```csharp
[RelayCommand]
private async Task LoadMatchesAsync()
{
    IsLoading = true;
    StatusMessage = "Loading matches...";
    
    try
    {
        // Check game config exists
        if (GameConfig == null)
        {
            StatusMessage = "Game configuration not loaded";
            return;
        }

        // Check event code exists
        if (string.IsNullOrEmpty(GameConfig.CurrentEventCode))
        {
            StatusMessage = "No event selected in game configuration";
            return;
        }

        // Get events
        var eventsResponse = await _apiService.GetEventsAsync();
        
        // Validate events response
        if (!eventsResponse.Success || eventsResponse.Events == null)
        {
            StatusMessage = "Failed to load events";
            return;
        }

        // Find current event by code (case-insensitive)
        var currentEvent = eventsResponse.Events
            .FirstOrDefault(e => e.Code.Equals(
                GameConfig.CurrentEventCode, 
                StringComparison.OrdinalIgnoreCase));
        
        if (currentEvent == null)
        {
            StatusMessage = $"Event '{GameConfig.CurrentEventCode}' not found";
            return;
        }

        // Load matches for event
        var matchesResponse = await _apiService.GetMatchesAsync(currentEvent.Id);
        
        // Validate matches response
        if (!matchesResponse.Success || matchesResponse.Matches == null)
        {
            StatusMessage = $"Failed to load matches for {currentEvent.Name}";
            return;
        }

        // Populate matches
        Matches.Clear();
        foreach (var match in matchesResponse.Matches
            .OrderBy(m => m.MatchType)
            .ThenBy(m => m.MatchNumber))
        {
            Matches.Add(match);
        }

        StatusMessage = $"Loaded {Matches.Count} matches for {currentEvent.Name}";
    }
    catch (Exception ex)
    {
        StatusMessage = $"Error loading matches: {ex.Message}";
    }
    finally
    {
        IsLoading = false;
    }
}
```

### 2. Improved LoadTeamsAsync() Method

Added status messages and better error handling:

```csharp
private async void LoadTeamsAsync()
{
    try
    {
        StatusMessage = "Loading teams...";
        var response = await _apiService.GetTeamsAsync(limit: 500);
        
        if (response.Success && response.Teams != null && response.Teams.Count > 0)
        {
            Teams.Clear();
            foreach (var team in response.Teams.OrderBy(t => t.TeamNumber))
            {
                Teams.Add(team);
            }
            StatusMessage = $"Loaded {Teams.Count} teams";
        }
        else
        {
            StatusMessage = response.Success 
                ? "No teams found" 
                : "Failed to load teams";
        }
    }
    catch (Exception ex)
    {
        StatusMessage = $"Error loading teams: {ex.Message}";
    }
}
```

### 3. Added RefreshTeamsCommand

Users can now manually refresh the teams list:

```csharp
[RelayCommand]
private async Task RefreshTeamsAsync()
{
    LoadTeamsAsync();
    await Task.CompletedTask;
}
```

### 4. Improved UI Layout

Enhanced the match info section with:
- Section title
- Refresh button for teams (?)
- Load button for matches
- Current event display
- Better spacing and organization

## Common Issues and Solutions

### Issue 1: "Game configuration not loaded"

**Cause:** Game config hasn't loaded yet  
**Solution:** Wait for loading to complete or tap "Retry Loading Configuration"

### Issue 2: "No event selected in game configuration"

**Cause:** `current_event_code` is not set in the game config  
**Solution:** Set `current_event_code` in your server's game configuration:

```json
{
  "current_event_code": "2024moks",
  ...
}
```

### Issue 3: "Event 'xxxxx' not found"

**Cause:** The event code in game config doesn't match any event in the database  
**Solution:** 
1. Check available events at `/api/mobile/events`
2. Update game config with correct event code
3. Event codes are compared case-insensitively

### Issue 4: "Failed to load events"

**Cause:** API call to get events failed  
**Solution:**
1. Check server is running
2. Verify authentication token is valid
3. Check network connection
4. Check server logs for errors

### Issue 5: "No matches found for event X"

**Cause:** Event exists but has no matches  
**Solution:**
1. Check if matches exist in database for that event
2. Verify event ID is correct
3. Check match import/sync has run

### Issue 6: Teams load but matches don't

**Possible Causes:**
1. Event code mismatch
2. Event not in database
3. Matches not imported for that event
4. API authentication issue

**Debugging Steps:**
1. Check the status message that appears
2. Verify `current_event_code` in game config
3. Test `/api/mobile/events` endpoint manually
4. Test `/api/mobile/matches?event_id=X` endpoint manually

## Testing the Fix

### Step 1: Check Status Messages
Watch the status message area when you tap "Load Matches". It will show:
- "Loading matches..."
- Then one of:
  - "Loaded X matches for [Event Name]" (success)
  - An error message explaining what went wrong

### Step 2: Verify Game Config
Check your game configuration includes:
```json
{
  "current_event_code": "2024moks"
}
```

### Step 3: Test Manually

**Check events endpoint:**
```bash
curl -H "Authorization: Bearer YOUR_TOKEN" \
  https://your-server.com/api/mobile/events
```

Should return:
```json
{
  "success": true,
  "events": [
    {
      "id": 1,
      "name": "Greater Kansas City Regional",
      "code": "2024moks",
      ...
    }
  ]
}
```

**Check matches endpoint:**
```bash
curl -H "Authorization: Bearer YOUR_TOKEN" \
  https://your-server.com/api/mobile/matches?event_id=1
```

Should return:
```json
{
  "success": true,
  "matches": [
    {
      "id": 1,
      "match_number": 1,
      "match_type": "Qualification",
      ...
    }
  ],
  "count": 50
}
```

## New UI Features

### Match Information Section

```
???????????????????????????????????
? Match Information               ?
???????????????????????????????????
? Team                        [?] ?
? [Select Team              ?]   ?
?                                 ?
? Match                   [Load]  ?
? [Select Match             ?]   ?
?                                 ?
? Current Event: 2024moks         ?
???????????????????????????????????
```

### Buttons
- **? (Refresh Teams)** - Reloads the teams list
- **Load (Load Matches)** - Fetches matches for current event

### Status Messages
Status messages now appear at the bottom showing:
- Loading progress
- Success with count
- Specific error messages

## Build Status

? **Compilation:** No errors  
? **Error Handling:** Comprehensive  
? **Status Messages:** Clear and helpful  
? **UI:** Improved layout

## Debugging Checklist

When matches don't load, check:

- [ ] Status message shows what went wrong
- [ ] Game config has `current_event_code` set
- [ ] Event code matches an event in database
- [ ] Event has matches in database
- [ ] Authentication token is valid
- [ ] Network connection is working
- [ ] Server is running and accessible

## Summary

The match loading issue has been fixed with:
1. ? Comprehensive error handling
2. ? Clear status messages at each step
3. ? Better validation of API responses
4. ? Case-insensitive event code matching
5. ? Manual refresh buttons
6. ? Current event display
7. ? Improved UI layout

Now when matches don't load, you'll see exactly what the problem is!
