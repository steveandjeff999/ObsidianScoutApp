# "Failed to Load Events" Error - Fix Applied

## Issue Identified

From your screenshot, the status shows **"Failed to load events"** which means:
- ? Teams are loading (you see "5002 - Dragon Robotics")
- ? Event code exists ("arsea")
- ? **Events API call is failing**
- ? Can't load matches because event lookup fails

## Root Cause

The `/api/mobile/events` endpoint is either:
1. Returning an error response
2. Timing out
3. Authentication issue
4. Server not responding

## Changes Made

### 1. Better Error Messages

The app will now show the **exact error** from the server:

**Before:**
```
? Failed to load events from server
```

**After:**
```
? Failed to load events from server. Error: HTTP 401: Unauthorized
```
or
```
? Failed to load events from server. Error: Network error: Connection refused
```

### 2. Enhanced Error Handling

Added specific error types:
- **HttpRequestException** - Network/connection errors
- **HTTP Status Codes** - Server returned error (401, 404, 500, etc.)
- **Parse Errors** - Invalid response format

### 3. Added Error Property

`EventsResponse` now includes an `Error` property that captures:
- HTTP status codes
- Error messages from server
- Network error details

## What to Do Next

### Step 1: Run the App Again

1. Tap the **"Load"** button
2. Read the new detailed error message
3. Report back what it says

### Step 2: Check Common Issues

#### Issue A: Authentication Error (401)
**Message:** `HTTP 401: Unauthorized`

**Cause:** Authentication token expired or invalid

**Fix:**
1. Log out and log back in
2. Check token expiration
3. Verify token is being sent in request

#### Issue B: Server Not Responding
**Message:** `Network error: Connection refused` or `Connection timeout`

**Cause:** Server is down or not reachable

**Fix:**
1. Check server is running
2. Verify server URL in settings
3. Check network connection
4. Try accessing server in browser

#### Issue C: Wrong Endpoint
**Message:** `HTTP 404: Not Found`

**Cause:** Events endpoint doesn't exist or wrong URL

**Fix:**
1. Verify endpoint: `{server_url}/api/mobile/events`
2. Check server routing configuration
3. Verify API version

#### Issue D: Server Error
**Message:** `HTTP 500: Internal Server Error`

**Cause:** Server-side error

**Fix:**
1. Check server logs
2. Verify database connection
3. Check for server-side exceptions

### Step 3: Test Events Endpoint Manually

Try accessing the endpoint directly:

```bash
curl -v -H "Authorization: Bearer YOUR_TOKEN" \
  https://your-server.com/api/mobile/events
```

**Look for:**
- Status code (200 = success, 401 = auth error, 404 = not found, 500 = server error)
- Response body (JSON data or error message)
- Connection errors

## Quick Debug Checklist

When you tap "Load" and see the error, check:

- [ ] **What's the exact error message?**
  - Contains HTTP status code?
  - Says "Network error"?
  - Says "Connection refused"?

- [ ] **Can teams load?**
  - Yes = Auth is working (issue is specific to events)
  - No = Auth might be the problem

- [ ] **Can you access other pages?**
  - Teams page works?
  - Events page works?

- [ ] **Is the server URL correct?**
  - Check app settings
  - Try in browser
  - Verify SSL/HTTPS

## Expected Error Messages

After the fix, you'll see one of these:

### Network Issues
```
? Failed to load events from server. 
Error: Network error: Connection refused
```
? Server not reachable

```
? Failed to load events from server. 
Error: Network error: The operation timed out
```
? Server too slow or not responding

### Authentication Issues
```
? Failed to load events from server. 
Error: HTTP 401: {"error":"Invalid token"}
```
? Need to log in again

### Server Issues
```
? Failed to load events from server. 
Error: HTTP 500: Internal Server Error
```
? Check server logs

### Endpoint Issues
```
? Failed to load events from server. 
Error: HTTP 404: Not Found
```
? Wrong URL or endpoint missing

## Build Status

? **No compilation errors**  
? **Detailed error messages added**  
? **Error property added to EventsResponse**  
? **Better exception handling**

## Next Steps

1. **Run the app**
2. **Tap "Load" button**
3. **Read the detailed error message**
4. **Tell me the exact error**
5. I'll give you specific fix instructions

The error message will now tell us exactly what's wrong with the events endpoint!

## Most Likely Scenarios

Based on "Failed to load events" with working teams:

### Scenario 1: Token Refresh Needed
Teams loaded initially, but token expired before loading events.
**Fix:** Log out and log in again

### Scenario 2: Events Endpoint Different
Events endpoint might be at different URL than teams.
**Fix:** Verify all endpoints use same base URL

### Scenario 3: Permission Issue
User has permission for teams but not events.
**Fix:** Check server-side permissions

### Scenario 4: Events Endpoint Missing
Server doesn't have `/api/mobile/events` implemented.
**Fix:** Implement or check endpoint exists

**Run it now and tell me the exact error message you see!**
