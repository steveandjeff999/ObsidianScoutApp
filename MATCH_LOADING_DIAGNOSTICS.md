# Match Loading Diagnostics - "arsea" Event Issue

## Current Situation

From your screenshot, I can see:
- **Current Event:** "arsea" (shown at bottom)
- **Team Picker:** Working (shows "5454 - Obsidian")
- **Match Picker:** Empty (shows "Select Match" but no matches)
- **Status:** No visible error message

## What I've Added

### 1. Visual Status Messages

The app will now show detailed status messages with emoji icons:

- ?? **"Looking for event: arsea"** - Starting search
- ?? **"Searching X events..."** - Found events, now searching
- ? **"Loaded X matches for [Event Name]"** - Success!
- ? **"Event 'arsea' not found. Available: 'event1', 'event2'"** - Event doesn't exist
- ?? **"No matches found for [Event Name]"** - Event exists but no matches
- ? **"Failed to load events from server"** - API error

### 2. Match Count Display

New label shows:
- **"X matches available"** - When matches are loaded
- **"No matches loaded"** - When Matches collection is empty

### 3. Status Message in Match Info Section

Status messages now appear directly in the Match Information box for better visibility.

## Steps to Diagnose

### Step 1: Tap "Load" Button Again

Watch for the status message that appears. It will tell you exactly what's happening:

**Possible Messages:**

#### ? Success
```
? Loaded 50 matches for Greater Kansas City Regional
```
**Action:** Matches should appear in picker

#### ? Event Not Found
```
? Event 'arsea' not found. Available: '2024moks', '2024txda', '2024cacv'
```
**Action:** Check your game config's `current_event_code`

#### ?? No Matches
```
?? No matches found for Arkansas Regional
```
**Action:** Import matches for this event

#### ? API Error
```
? Failed to load events from server
```
**Action:** Check server connection and authentication

### Step 2: Check Your Game Configuration

The event code "arsea" needs to match an actual event in your database.

**Check server-side game config:**
```bash
curl -H "Authorization: Bearer YOUR_TOKEN" \
  https://your-server.com/api/mobile/config/game
```

Look for:
```json
{
  "current_event_code": "arsea",
  ...
}
```

### Step 3: Verify Events Exist

**Check available events:**
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
      "name": "Arkansas Regional",
      "code": "2024arsea",  // Note: might be "2024arsea" not "arsea"
      ...
    }
  ]
}
```

### Step 4: Common Issue - Event Code Mismatch

**Problem:** Game config says `"arsea"` but database has `"2024arsea"`

**Solution 1:** Update game config to match:
```json
{
  "current_event_code": "2024arsea"
}
```

**Solution 2:** Update database event code to match game config

### Step 5: Check if Matches Exist

Once you find the correct event code/ID:
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

## Quick Fix Checklist

When matches don't load, check these in order:

1. **[ ] Tap "Load" and read the status message**
   - What does it say?

2. **[ ] Check event code format**
   - Game config: `"arsea"` or `"2024arsea"`?
   - Database: Does it match?

3. **[ ] Verify event exists in database**
   - Query `/api/mobile/events`
   - Look for your event code

4. **[ ] Check matches exist for event**
   - Query `/api/mobile/matches?event_id=X`
   - Do any matches return?

5. **[ ] Check authentication**
   - Is your token valid?
   - Can you access other API endpoints?

## Most Likely Issues

### Issue 1: Event Code Format Mismatch
**Symptom:** Status shows "Event 'arsea' not found"  
**Cause:** Game config has `"arsea"` but database has `"2024arsea"`  
**Fix:** Make them match (update one to match the other)

### Issue 2: Event Not Imported
**Symptom:** Status shows available events, but not "arsea"  
**Cause:** Event hasn't been added to database  
**Fix:** Import event data or add event manually

### Issue 3: No Matches Imported
**Symptom:** Status shows "No matches found for [Event]"  
**Cause:** Event exists but has no matches  
**Fix:** Run match import/sync for that event

## Expected Behavior After Fix

Once working, you should see:

```
???????????????????????????????????????
? Match Information                   ?
???????????????????????????????????????
? ? Loaded 50 matches for Arkansas... ?
?                                     ?
? Team                            [?] ?
? [5454 - Obsidian              ?]   ?
?                                     ?
? Match                       [Load]  ?
? [Qualification 1              ?]   ?
?                                     ?
? 50 matches available                ?
? Current Event: 2024arsea            ?
???????????????????????????????????????
```

## Debug Output

With the new changes, tapping "Load" will show a sequence of messages:

1. **"Loading matches..."** - Starting
2. **"?? Looking for event: arsea"** - Checking event code
3. **"?? Searching X events..."** - Got events list
4. **"?? Loading matches for [Name]..."** - Found event, getting matches
5. **"? Loaded X matches..."** - Success! (or an error message)

## Next Steps

1. **Run the app** with these changes
2. **Tap "Load"** button
3. **Read the status message** that appears
4. **Report back** what status message you see
5. I can then give you specific instructions based on the exact error

The status messages will tell us exactly what's wrong!

## Build Status

? **No compilation errors**  
? **Status messages added**  
? **Match count display added**  
? **Better error reporting**

Ready to test! Tap "Load" and tell me what status message you see.
