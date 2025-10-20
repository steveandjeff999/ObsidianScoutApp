# Submit Troubleshooting Quick Reference

## Problem
Submit button says "failed" but QR code works.

## Fixed
? JsonElement conversion issue - values now properly converted before sending

## To Debug

### 1. Run in Debug Mode
- Press F5 to run with debugger
- View ? Output ? Show output from: **Debug**

### 2. Fill Form & Submit
Watch for these messages:

```
Submitting to: https://your-server/api/mobile/scouting/submit
Team ID: X, Match ID: Y, Data fields: N
Response status: [CODE]
Result: Success=[true/false], Error=[message]
```

### 3. Check Status Code

| Code | Meaning | Fix |
|------|---------|-----|
| 200 | Success | Check if Success=true in response |
| 401 | No Auth | Re-login to get new token |
| 400 | Bad Data | Check data format |
| 404 | Not Found | Verify server URL |
| 500 | Server Error | Check server logs |

### 4. Common Solutions

**"HTTP 401"** ? Click logout and login again

**"HTTP 400"** ? Server doesn't like the data format

**"Network error"** ? Check server is running

**"Submission failed"** (no error code) ? Check server logs

### 5. Quick Test

Try this simplified submit:

```csharp
// Temporarily replace submission data with:
Data = new Dictionary<string, object?> { ["test"] = "value" }
```

If this works ? Field conversion issue
If this fails ? Auth or server issue

## What Was Changed

### ScoutingViewModel.cs
```csharp
// OLD - Direct copy (JsonElement problem)
Data = new Dictionary<string, object?>(fieldValues)

// NEW - Convert values first
var convertedData = new Dictionary<string, object?>();
foreach (var kvp in fieldValues)
{
    convertedData[kvp.Key] = ConvertValueForSerialization(kvp.Value);
}
Data = convertedData
```

### ApiService.cs
- Better error messages
- Debug logging
- JSON error handling

## Check These

- [ ] Auth token valid (try re-login)
- [ ] Server running and accessible
- [ ] URL correct in settings
- [ ] Server endpoint exists
- [ ] Debug output shows errors
- [ ] QR data decodes correctly

## Data Format

Both QR and Submit should use SAME format:

```json
{
  "team_id": 5,
  "match_id": 528,
  "elem_field_1": 0,
  "elem_field_2": true,
  "text_field": "comment"
}
```

All values = simple types (int, bool, string, null)
No JsonElement objects

## Still Failing?

Copy the Output window text and check:
1. What's the HTTP status code?
2. What's the error message?
3. Does the server receive the request?

---

**TL;DR**: Run in debug mode, watch Output window, note the HTTP status code, that tells you what's wrong.
