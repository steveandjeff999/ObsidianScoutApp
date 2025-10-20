# ENHANCED DEBUG LOGGING - Submit Fix

## What Was Added

### Comprehensive Debug Output

The submit functionality now includes extensive logging to help diagnose issues:

### 1. ScoutingViewModel Logging

**Before Submit:**
- Team ID and number
- Match ID and number
- Field count
- Each field name, value, and type
- JSON preview of entire submission

**After Submit:**
- Success/failure status
- Error messages
- Error codes
- Scouting ID (if successful)
- Full exception details with stack traces

### 2. ApiService Logging

**Request Details:**
- Timestamp
- Full endpoint URL
- Team ID and Match ID
- Data field count
- Offline ID
- Authentication header (truncated)
- Request body JSON (first 500 chars)

**Response Details:**
- Response time in milliseconds
- HTTP status code
- Response headers
- Response body content
- Parsed response fields

**Error Handling:**
- Network errors
- JSON parsing errors
- Timeout errors
- General exceptions
- Inner exceptions

## How to Use Debug Output

### Step 1: Open Output Window

1. Run app in Debug mode (F5)
2. View ? Output (Ctrl+Alt+O)
3. Show output from: **Debug**

### Step 2: Perform Submit

1. Fill out scouting form
2. Click Submit button
3. Watch Output window

### Step 3: Analyze Debug Log

You'll see output like this:

```
=== SUBMIT SCOUTING DATA ===
Team ID: 5
Team Number: 16
Match ID: 528
Match Number: 1
Data Fields: 15
Offline ID: abc123-def456
  Field: elem_auto_1 = 5 (Type: Int32)
  Field: elem_auto_2 = True (Type: Boolean)
  ... (more fields)
JSON Preview:
{
  "team_id": 5,
  "match_id": 528,
  "data": {
    "elem_auto_1": 5,
    ...
  },
  "offline_id": "abc123-def456"
}

=== API: SUBMIT SCOUTING DATA ===
Timestamp: 2025-01-18 15:30:45.123
Endpoint: https://your-server/api/mobile/scouting/submit
Team ID: 5
Match ID: 528
Data fields: 15
Offline ID: abc123-def456
Auth: Bearer eyJhbGciOiJ...
Request Body:
{
  "team_id": 5,
  ...
}
Sending POST request...
Response received in 234ms
Status Code: 200 OK
Success: True
Response Headers:
  Content-Type: application/json
  ...
Response Body: {"success":true,"scouting_id":42,"message":"Created"}
Parsed Success: True
Parsed Message: Created
Parsed Error: 
Parsed Scouting ID: 42

=== SUBMIT RESULT ===
Success: True
Message: Created
Error: 
Error Code: 
Scouting ID: 42
SUCCESS! Scouting entry created with ID: 42
=== END SUBMIT ===
```

## Interpreting Debug Output

### Success Scenario

Look for:
```
Status Code: 200 OK
Parsed Success: True
SUCCESS! Scouting entry created with ID: [number]
```

Status message shows: `? Scouting data submitted successfully!`

### Failure Scenarios

#### Scenario 1: HTTP Error (401 Unauthorized)

```
Status Code: 401 Unauthorized
Error Response Body: {"success":false,"error":"Invalid token"}
```

**Fix:** Re-login to get a fresh authentication token

#### Scenario 2: HTTP Error (400 Bad Request)

```
Status Code: 400 BadRequest
Error Response Body: {"success":false,"error":"Missing required field: xxx"}
```

**Fix:** Check what fields the server expects vs what you're sending

#### Scenario 3: Network Error

```
=== HTTP REQUEST EXCEPTION ===
Message: No connection could be made because the target machine actively refused it
```

**Fix:** 
- Check server is running
- Verify server URL in settings
- Check firewall settings

#### Scenario 4: JSON Serialization Error

```
Failed to serialize preview: Cannot serialize JsonElement...
```

**Fix:** There's still a JsonElement in the data (shouldn't happen with current code)

#### Scenario 5: Server Returns Success=False

```
Status Code: 200 OK
Response Body: {"success":false,"error":"Database error"}
Parsed Success: False
Parsed Error: Database error
```

**Fix:** Server-side issue, check server logs

#### Scenario 6: Timeout

```
=== TIMEOUT EXCEPTION ===
Message: The request was canceled due to the configured HttpClient.Timeout
```

**Fix:**
- Server is slow or stuck
- Increase timeout
- Check server performance

## Common Issues & Solutions

### Issue: "X Failed to submit scouting data"

**Check debug output for:**

1. **HTTP Status Code**
   - 200 = Look at response body
   - 401 = Re-login
   - 400 = Fix data format
   - 404 = Check URL
   - 500 = Server error

2. **Error Message**
   - Look at "Parsed Error:" line
   - Contains reason from server

3. **Error Code**
   - Custom error code from server
   - Helps identify specific issue

### Issue: No debug output appears

**Solutions:**
1. Make sure running in Debug mode (not Release)
2. Check Output window is set to "Debug"
3. Try View ? Other Windows ? Output
4. Rebuild solution

### Issue: Request never completes

**Check:**
```
Sending POST request...
[no response after]
```

- Server might be down
- Network connectivity issue
- Firewall blocking

### Issue: JSON parsing error

```
ERROR: JSON deserialization failed
```

- Server returned invalid JSON
- Check "Response Body:" in debug output
- Server might be returning HTML error page

## Debug Output Checklist

When submit fails, check these in order:

- [ ] **Timestamp** - Is request actually being sent?
- [ ] **Endpoint** - Is URL correct?
- [ ] **Auth** - Is Bearer token present?
- [ ] **Request Body** - Does JSON look valid?
- [ ] **Status Code** - What did server return?
- [ ] **Response Body** - What did server say?
- [ ] **Parsed Error** - Specific error message?
- [ ] **Exception Type** - Network/JSON/Timeout?

## Advanced Debugging

### Compare with QR Code

Since QR works, compare the data:

1. Generate QR code
2. Decode QR (use phone app or online tool)
3. Check debug output from Submit
4. Compare the two JSON structures

They should be nearly identical except:
- QR has extra metadata (generated_at, etc.)
- Submit has team_id, match_id at root
- Submit has offline_id

### Test with Minimal Data

Temporarily simplify:

```csharp
// In SubmitAsync, before API call:
submission.Data = new Dictionary<string, object?>
{
    ["test"] = "value"
};
```

If this works ? Issue with field data
If this fails ? Issue with auth/server

### Use Network Analyzer

Tools like Fiddler can show:
- Exact HTTP request
- All headers
- Full request body
- Full response body
- Timing details

## Expected Flow

### Normal Successful Submit

```
1. User clicks Submit
   ? ViewModel validates (team & match selected)
   
2. ViewModel converts field values
   ? JsonElement ? simple types
   ? Logs each field
   
3. ViewModel creates ScoutingSubmission
   ? team_id, match_id, data, offline_id
   ? Logs JSON preview
   
4. ApiService sends POST request
   ? Adds auth header
   ? Serializes to JSON
   ? Logs request details
   
5. Server processes request
   ? Validates data
   ? Saves to database
   ? Returns success response
   
6. ApiService receives response
   ? Parses JSON
   ? Logs response details
   ? Returns ScoutingSubmitResponse
   
7. ViewModel receives result
   ? Checks Success=true
   ? Shows success message
   ? Waits 3 seconds
   ? Resets form
```

### Where It Can Fail

**Step 2:** Field conversion issues
- Debug: Check field type logs
- Fix: Update ConvertValueForSerialization

**Step 4:** Network/Auth issues
- Debug: Check "Auth:" and "Endpoint:" logs
- Fix: Re-login or check server URL

**Step 5:** Server-side validation
- Debug: Check "Response Body:" log
- Fix: Adjust data to match server expectations

**Step 6:** JSON parsing
- Debug: Check for JSON exceptions
- Fix: Ensure server returns valid JSON

## Quick Diagnosis Guide

### Symptom: "X Failed to submit scouting data"

**Look for this in debug:**

1. **"Status Code: 401"**
   ? Authentication failed ? Re-login

2. **"Status Code: 400"**
   ? Bad data ? Check request body vs server expectations

3. **"Status Code: 404"**
   ? Wrong URL ? Check settings

4. **"Status Code: 500"**
   ? Server crashed ? Check server logs

5. **"Parsed Success: False"**
   ? Server rejected ? Check "Parsed Error:"

6. **"Network error:"**
   ? Can't reach server ? Check server running

7. **"JSON error:"**
   ? Bad response format ? Check response body

8. **"Timeout"**
   ? Server too slow ? Check server performance

## What to Share for Help

If you're still stuck, share these from debug output:

1. The "=== SUBMIT SCOUTING DATA ===" section
2. The "=== API: SUBMIT SCOUTING DATA ===" section
3. The "Status Code:" line
4. The "Response Body:" line
5. The "=== SUBMIT RESULT ===" section
6. Any exception sections

## Success Indicators

You'll know it works when you see:

```
Status Code: 200 OK
Parsed Success: True
SUCCESS! Scouting entry created with ID: [number]
```

And the status bar shows:
```
? Scouting data submitted successfully!
```

Form resets automatically after 3 seconds.

## Code Changes Summary

**Files Modified:**
1. `ScoutingViewModel.cs` - Enhanced SubmitAsync with detailed logging
2. `ApiService.cs` - Enhanced SubmitScoutingDataAsync with comprehensive logging

**What's Logged:**
- ? All field names, values, and types
- ? Complete request JSON
- ? HTTP request details
- ? Authentication status
- ? Response status and timing
- ? Response headers and body
- ? Parsed response fields
- ? All exceptions with stack traces
- ? Error codes and messages

**Total Log Lines:** ~50-100 lines per submit attempt

This should give us everything needed to diagnose the issue!
