# Scouting Submission Fix

## Problem
The scouting form submission was failing when clicking the Submit button.

## Root Cause
According to the Mobile API Documentation, the server expects scouting submissions to include a `scout_name` field in the data. The current implementation was not including this field in the submission payload.

## What Was Changed

### ScoutingViewModel.cs - SubmitAsync Method
Added the `scout_name` field to the submission data:

```csharp
// Add scout_name to the data as required by the API
if (!string.IsNullOrEmpty(ScoutName))
{
    convertedData["scout_name"] = ScoutName;
}
```

This ensures that when the user enters their name in the Scout Name field, it gets included in the submission payload sent to the server.

## API Requirements

According to the documentation, the `/api/mobile/scouting/submit` endpoint expects:

```json
{
  "team_id": 1,
  "match_id": 5,
  "data": {
    // Dynamic scouting fields...
    "scout_name": "John Doe",  // This was missing!
    "auto_speaker_scored": 3,
    "auto_amp_scored": 2,
    // ...other fields
  },
  "offline_id": "550e8400-e29b-41d4-a716-446655440000"
}
```

## Server Processing Steps (from documentation)

When the server receives a submission:

1. **Validates Required Fields**: `team_id`, `match_id`, `data`
2. **Verifies Team & Match**: Ensures they exist and belong to the authenticated scouting team
3. **Creates ScoutingData Record** with fields:
   - `team_id` - provided team_id
   - `match_id` - provided match_id
   - `data` - the JSON object from the request (including scout_name)
   - `scout` - username from authenticated user
   - `scouting_team_number` - from token
   - `timestamp` - current server time

## Testing the Fix

1. **Run the application**
2. **Navigate to the Scouting page**
3. **Fill in the form**:
   - Select a team
   - Select a match
   - **Enter your name in the Scout Name field** (important!)
   - Fill in scoring data
4. **Click Submit**
5. **Verify success**: You should see "? Scouting data submitted successfully!"

## Debugging Tips

If submission still fails, check the debug output for:

```
=== SUBMIT SCOUTING DATA ===
Team ID: [number]
Match ID: [number]
Scout Name: [your name]  // Should not be empty
Data Fields: [count]
```

Also check:
- **Authentication**: Make sure you're logged in (token is valid)
- **Team & Match**: Ensure the selected team and match exist on the server
- **Server Logs**: Check the server console for detailed error messages
- **Network**: Verify the app can reach the server

## Error Codes You Might See

- `MISSING_DATA` - No data provided in request
- `MISSING_FIELD` - Required field missing (team_id, match_id, or data)
- `TEAM_NOT_FOUND` - Team doesn't exist or doesn't belong to your scouting team
- `MATCH_NOT_FOUND` - Match doesn't exist or doesn't belong to your scouting team
- `SUBMIT_ERROR` - Database error on the server

## Additional Notes

- The `scout_name` field is stored within the `data` JSON object, not as a top-level field in the submission
- The server also records the authenticated username separately in the `scout` field
- Make sure to fill in the Scout Name field on the form before submitting
