# Submit Functionality Fix & Debugging Guide

## Issue
Regular submit says "failed" while QR code generation works perfectly.

## Root Cause Analysis

The QR code works because it only generates data locally. The regular submit fails because it needs to send data to the server via HTTP POST.

### Possible Causes

1. **JsonElement Serialization** ? FIXED
   - Field values stored as `JsonElement` can't be serialized
   - **Solution:** Convert all values using `ConvertValueForSerialization()`

2. **Network/Server Issues** ?? TO CHECK
   - Server endpoint not responding
   - Authentication token expired
   - Network connectivity

3. **Data Format Issues** ?? TO CHECK
   - Server expects different data format
   - Missing required fields
   - Invalid data types

## Changes Made

### 1. ScoutingViewModel.cs - Enhanced Submit Method

**Before:**
```csharp
var submission = new ScoutingSubmission
{
    TeamId = TeamId,
    MatchId = MatchId,
    Data = new Dictionary<string, object?>(fieldValues) // JsonElement objects!
};
```

**After:**
```csharp
// Convert all field values to simple types (handle JsonElement)
var convertedData = new Dictionary<string, object?>();
foreach (var kvp in fieldValues)
{
    convertedData[kvp.Key] = ConvertValueForSerialization(kvp.Value);
}

var submission = new ScoutingSubmission
{
    TeamId = TeamId,
    MatchId = MatchId,
    Data = convertedData // Simple types only
};
```

**Added Debug Logging:**
- Logs submission details (Team ID, Match ID, field count)
- Logs API response (success/failure)
- Logs detailed error messages
- Logs exceptions with stack traces

### 2. ApiService.cs - Enhanced Error Handling

**Improvements:**
- Logs HTTP request URL
- Logs submission data summary
- Logs HTTP response status code
- Reads error content before parsing JSON
- Handles JSON parsing failures gracefully
- Returns detailed error messages

## Debugging Steps

### Step 1: Check Debug Output

Run the app in Debug mode and watch the **Output Window** (View ? Output ? Show output from: Debug).

Look for these messages when you click Submit:

```
Submitting to: https://your-server/api/mobile/scouting/submit
Team ID: 5, Match ID: 528, Data fields: 15
Response status: 200 (or error code)
```

### Step 2: Analyze Response Status

**If you see:**

#### `Response status: 200` (Success)
? Server received data successfully
- Check if `Success=false` in response body
- Look for error message in response

#### `Response status: 401` (Unauthorized)
? Authentication token expired or invalid
- **Fix:** Re-login to get new token
- Check token expiration in settings

#### `Response status: 400` (Bad Request)
? Data format is wrong
- **Fix:** Check what data server expects
- Compare with QR code data format

#### `Response status: 404` (Not Found)
? Endpoint doesn't exist
- **Fix:** Verify server URL in settings
- Check if `/api/mobile/scouting/submit` is correct path

#### `Response status: 500` (Server Error)
? Server crashed processing request
- **Fix:** Check server logs
- May need to fix server-side code

#### `Network error: ...`
? Can't reach server
- **Fix:** Check internet connection
- Verify server is running
- Check firewall/SSL settings

### Step 3: Compare Working vs Non-Working

**QR Code Data (Works):**
```json
{
  "team_id": 5,
  "team_number": 16,
  "match_id": 528,
  "match_number": 1,
  "alliance": "unknown",
  "scout_name": "John Doe",
  "elem_auto_1": true,
  "elem_auto_2": 5,
  // ... all fields converted to simple types
}
```

**Submit Data (Should Match):**
- Same field structure
- Same data types
- Same conversion logic
- Only difference: sent via HTTP instead of QR

## Quick Fixes to Try

### Fix 1: Verify Authentication

```csharp
// In SubmitAsync, before calling API:
var token = await _settingsService.GetTokenAsync();
System.Diagnostics.Debug.WriteLine($"Auth token: {token?.Substring(0, 20)}...");
```

If token is null or expired ? Re-login

### Fix 2: Test with Minimal Data

Temporarily simplify the submission:

```csharp
var submission = new ScoutingSubmission
{
    TeamId = TeamId,
    MatchId = MatchId,
    Data = new Dictionary<string, object?>
    {
        ["test_field"] = "test_value"
    }
};
```

If this works ? Problem is with field data conversion
If this fails ? Problem is with authentication or server

### Fix 3: Check Server Logs

On your server, check the API logs for:
- Incoming POST requests to `/scouting/submit`
- Any error messages
- What data the server actually received

### Fix 4: Verify Content-Type

The API uses `PostAsJsonAsync` which sets:
- `Content-Type: application/json`
- Serializes using `JsonSerializerOptions`

Verify your server accepts `application/json`.

## Expected vs Actual

### What SHOULD Happen

1. User fills form ? Field values stored in `fieldValues`
2. Click Submit ? Convert `JsonElement` to simple types
3. Create `ScoutingSubmission` object
4. POST to `/api/mobile/scouting/submit` with auth token
5. Server responds with `{ success: true, ... }`
6. Show success message, reset form

### What's PROBABLY Happening

Based on "submission failed" error:

**Scenario A: Server Rejects Data**
```
Client ? POST data ? Server
Server checks data ? Finds issue ? Returns error
Client shows: "? Submission failed"
```

**Scenario B: Network Issue**
```
Client ? POST data ? [Network Error]
Client shows: "? Error: ..."
```

**Scenario C: Authentication Failed**
```
Client ? POST data ? Server
Server checks token ? Invalid ? 401 Unauthorized
Client shows: "? Request failed with status 401"
```

## Testing Checklist

Run through this checklist:

- [ ] Debug output shows submission starting
- [ ] Debug output shows correct Team ID and Match ID
- [ ] Debug output shows field count > 0
- [ ] Debug output shows HTTP status code
- [ ] If 401: Token is valid and not expired
- [ ] If 400: Data format matches server expectations
- [ ] If 500: Check server logs for errors
- [ ] If network error: Server is running and accessible
- [ ] Same data works in QR code
- [ ] Server endpoint `/api/mobile/scouting/submit` exists
- [ ] Server accepts `application/json` content type

## Next Steps

### Immediate Action: Check Debug Output

1. Run the app in Debug mode
2. Fill out a scouting form
3. Click Submit
4. Look at Output window for debug messages
5. Note the exact error message

### Based on Error Message:

**"HTTP 401: ..."**
? Re-login to get fresh token

**"HTTP 400: ..."**
? Check what data server expects vs what we're sending

**"HTTP 500: ..."**
? Check server logs, may be server-side bug

**"Network error: ..."**
? Check server is running, firewall, SSL certificates

**"JSON serialization error: ..."**
? Still an issue with data conversion (shouldn't happen now)

**No error, just "Submission failed"**
? Server returned `success: false` - check server logs for reason

## Advanced Debugging

### Enable Fiddler/Charles Proxy

1. Install Fiddler or Charles Proxy
2. Configure app to use proxy
3. Watch actual HTTP request/response
4. Compare request body with QR code data

### Server-Side Debugging

1. Add logging to your server's `/scouting/submit` endpoint
2. Log received data
3. Log any validation errors
4. Log database insert success/failure

### Test with Postman

1. Export the exact request data from debug logs
2. Create a POST request in Postman
3. Set Content-Type: application/json
4. Set Authorization: Bearer [your-token]
5. Send request
6. Compare response with app's response

## Common Issues & Solutions

| Issue | Symptom | Solution |
|-------|---------|----------|
| Expired Token | HTTP 401 | Re-login |
| Wrong URL | HTTP 404 | Check server URL in settings |
| Missing Fields | HTTP 400 | Add required fields to submission |
| JsonElement | Serialization error | Already fixed ? |
| Server Down | Network error | Start server |
| SSL Issues | Connection error | Check SSL certificate configuration |
| CORS | Network error (web) | Enable CORS on server |

## Success Indicators

You'll know it's fixed when you see:

```
Debug Output:
Submitting: Team=5, Match=528, Fields=15
Response status: 200
Result: Success=True, Error=
```

Status Message:
```
? Scouting data submitted successfully!
```

Form automatically resets after 3 seconds.

## Code Changes Summary

**Files Modified:**
1. `ScoutingViewModel.cs` - Added value conversion and debug logging
2. `ApiService.cs` - Enhanced error handling and logging

**Key Changes:**
- ? Convert `JsonElement` to simple types before serialization
- ? Add debug logging throughout submit process
- ? Improve error message details
- ? Handle JSON parsing errors gracefully
- ? Log HTTP request/response details

## Still Not Working?

If you've tried everything above and it still fails:

1. **Share the debug output** - Copy the Output window text when you click Submit
2. **Check server logs** - What does the server see?
3. **Compare with QR** - Generate QR code, decode it, compare with submission data
4. **Test endpoint** - Use Postman/curl to test the endpoint directly
5. **Verify server code** - Check if the endpoint is implemented correctly

## Contact Info

If you need more help, provide:
- Debug output from Output window
- Error message from status bar
- Server logs (if available)
- HTTP status code
- Whether QR code data looks correct when decoded
