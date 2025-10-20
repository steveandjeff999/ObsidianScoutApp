# Scouting Submission Error Troubleshooting Guide

## Current Error
`? [SUBMIT_ERROR] Failed to submit scouting data`

This error indicates a **server-side database error** during submission. The error is being returned by the server, not a client-side issue.

---

## Debugging Steps

### Step 1: Check Debug Output Window
1. **Run the app in Debug mode** (F5)
2. **Open the Output window** in Visual Studio (View ? Output)
3. **Select "Debug"** from the "Show output from:" dropdown
4. **Fill out and submit the form**
5. **Look for these sections** in the output:

```
=== SUBMIT SCOUTING DATA ===
Team ID: <number> (Team #<team_number>)
Match ID: <number> (Match #<match_number> - <type>)
Scout Name: '<name>'
Data Fields Count: <count>

Field Details:
  - <field_id>: <value> (<type>)
  ...

JSON Preview:
{
  "team_id": <id>,
  "match_id": <id>,
  "data": { ... },
  "offline_id": "<guid>"
}

=== API: SUBMIT SCOUTING DATA ===
Endpoint: https://<server>/api/mobile/scouting/submit
Response Body: <IMPORTANT - THIS SHOWS THE ACTUAL ERROR>

=== SUBMIT RESULT ===
Success: false
Error: <detailed error message>
Error Code: SUBMIT_ERROR
```

### Step 2: Analyze the Server Response
The **"Response Body:"** line contains the ACTUAL error from the server. Common errors:

#### Error: "Team not found"
- **Problem:** The selected team doesn't exist or doesn't belong to your scouting team
- **Check:** In the web UI, verify the team exists and you can see it
- **Fix:** Make sure you're logged in with the correct team credentials

#### Error: "Match not found"
- **Problem:** The selected match doesn't exist or doesn't belong to your scouting team
- **Check:** In the web UI, verify the match exists for the selected event
- **Fix:** Make sure the event has matches imported and you selected the right event

#### Error: "IntegrityError" or "UNIQUE constraint failed"
- **Problem:** Duplicate submission (same team + match already scouted)
- **Check:** In the web UI, look for existing scouting data for this team/match
- **Fix:** The server may not allow duplicate submissions. Check server settings.

#### Error: "ForeignKeyViolation"
- **Problem:** The team_id or match_id references don't exist in the database
- **Check:** The IDs you're sending may be from a different server/database
- **Fix:** Re-sync teams and matches from the server

#### Error: "Missing required field: scout"
- **Problem:** Authentication token doesn't contain user information
- **Check:** Your token may be expired or invalid
- **Fix:** Log out and log back in

---

## Common Causes and Fixes

### 1. Authentication Issue
**Symptom:** Server can't identify the authenticated user  
**Check:**
```
In Debug Output, look for:
  Auth: Bearer eyJ... (should show token)
```
**Fix:**
- Log out and log back in
- Verify your username and team number are correct

### 2. Wrong Server/Database
**Symptom:** Team and Match IDs don't exist on the server  
**Check:**
- Are you connecting to the correct server URL?
- Did you log in with the correct credentials?
**Fix:**
- Verify server URL in Settings
- Ensure you're logged in to the correct scouting team

### 3. Duplicate Submission
**Symptom:** You already submitted data for this team/match  
**Check:**
- In web UI, go to Scouting Data and search for the team/match
**Fix:**
- Server may prevent duplicates (this is normal)
- Or, server needs to be configured to allow updates

### 4. Missing Event Code
**Symptom:** Matches loaded but submissions fail  
**Check:**
```
Debug Output should show:
  Event: <event_code>
```
**Fix:**
- Ensure the game config has `current_event_code` set
- Reload the game configuration

---

## How to Get Detailed Server Logs

If the mobile app debug output doesn't show enough detail:

### On the Server Side:
1. **SSH/RDP into the server** running the OBSIDIAN Scout API
2. **Check the application logs:**
   ```bash
   # For systemd services
   journalctl -u obsidian-scout -f
   
   # For Docker
   docker logs -f obsidian-scout-api
   
   # For Python/Flask apps
   tail -f /path/to/logs/app.log
   ```
3. **Reproduce the submission** from the mobile app
4. **Look for Python traceback or SQL errors**

Common server-side errors you might see:
```python
sqlalchemy.exc.IntegrityError: UNIQUE constraint failed: scouting_data.team_id, scouting_data.match_id
# ? You already submitted data for this team/match

sqlalchemy.exc.IntegrityError: FOREIGN KEY constraint failed
# ? The team_id or match_id doesn't exist in the database

AttributeError: 'NoneType' object has no attribute 'username'
# ? Authentication issue - user not found in token

KeyError: 'scout'
# ? Server code bug - missing scout field extraction
```

---

## Request Payload Verification

The mobile app is sending this JSON structure:
```json
{
  "team_id": 1,
  "match_id": 5,
  "data": {
    "auto_speaker_scored": 3,
    "auto_amp_scored": 2,
    "teleop_speaker_scored": 10,
    "teleop_amp_scored": 5,
    "endgame_climb": "successful",
    "notes": "Great performance!",
    "scout_name": "John Doe"
  },
  "offline_id": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Verify in Debug Output:**
1. `team_id` and `match_id` are integers (not 0, not null)
2. `data` contains all your form fields
3. `scout_name` is included if you filled it in

---

## Quick Diagnostic Checklist

- [ ] **I can see teams and matches** (they load successfully)
- [ ] **I selected a team and match** (not empty/default)
- [ ] **I'm logged in** (token exists and is valid)
- [ ] **The server URL is correct** (can access web UI at same URL)
- [ ] **The game config loaded** (I can see form fields)
- [ ] **I checked the Debug Output** (looked for "Response Body")
- [ ] **I checked server logs** (if I have access)

---

## Next Steps

1. **Run the app in Debug mode**
2. **Submit the form and capture the full debug output**
3. **Look for "Response Body:" in the API section**
4. **Share that specific error message for further help**

If the error persists, you need the **actual server response body** to diagnose further. The `SUBMIT_ERROR` code is just a generic wrapper - the real error is in the server's JSON response.

---

## Testing with Postman/curl

You can also test the API directly to isolate the issue:

```bash
# 1. Get your auth token (from settings or by logging in)
TOKEN="your_jwt_token_here"

# 2. Test submission
curl -X POST https://your-server.com/api/mobile/scouting/submit \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "team_id": 1,
    "match_id": 5,
    "data": {
      "auto_speaker_scored": 3,
      "scout_name": "Test Scout"
    },
    "offline_id": "test-123"
  }'
```

This will show you the EXACT server response without the mobile app in the way.
