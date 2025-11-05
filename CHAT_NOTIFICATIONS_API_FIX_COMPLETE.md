# ?? Chat Notifications - API Integration Fix (Complete)

## ?? Root Cause Analysis

### Issue: API Returns 404 "Other user not found"

**Evidence from logs:**
```
[BackgroundNotifications] Fetching DM messages with user: 5454
[API] GET https://server/api/mobile/chat/messages?type=dm&limit=50&offset=0&user=5454
[API] Status: 404 NotFound
[API] Response: {
  "error": "Other user not found",
  "error_code": "USER_NOT_FOUND",
  "success": false
}
```

**What's happening:**
1. App receives chat state from server: `lastDmUser: "5454"`
2. App tries to fetch messages: `GET /messages?user=5454`
3. Server responds with 404 because "5454" is a **team number**, not a **username**

**Root cause:** Server's `/api/mobile/chat/state` endpoint is returning team numbers instead of usernames in the `lastDmUser` and `lastSource.id` fields.

---

## ?? Server-Side Fix Required

### Problem: Chat State Returns Team Number

**Current server response (WRONG):**
```json
{
  "success": true,
  "state": {
    "lastDmUser": "5454",// ? Team number, not username!
    "lastSource": {
  "type": "dm",
      "id": "5454"  // ? Team number, not username!
    },
    "unreadCount": 1
  }
}
```

**Expected server response (CORRECT):**
```json
{
  "success": true,
  "state": {
    "lastDmUser": "scout123",  // ? Actual username
    "lastSource": {
      "type": "dm",
      "id": "scout123"  // ? Actual username
    },
    "unreadCount": 1
  }
}
```

### Server Fix Implementation

**File:** `server/routes/mobile/chat.py` or equivalent

```python
@mobile_bp.route('/chat/state', methods=['GET'])
@mobile_auth_required
def get_chat_state():
    user = request.mobile_user
    team_number = user.scouting_team_number
    username = normalize_username(user.username)
    
  # Read state file
    state_file = get_chat_state_file(team_number, username)
    state = load_json_file(state_file, default={})
    
    # CRITICAL FIX: Validate lastDmUser is username, not team number
    if 'lastDmUser' in state:
      last_dm_user = state['lastDmUser']
        
        # If it's all digits, it's likely a team number (WRONG!)
        if last_dm_user.isdigit():
       logger.warning(f"Chat state for {username} has team number in lastDmUser: {last_dm_user}")
          logger.warning("This will cause mobile API to fail. Fix: store username instead.")
      
            # Try to resolve to actual username from recent messages
      # (This is a fallback - the real fix is to prevent storing team numbers)
   try:
         # Look up the most recent DM conversation
                chat_dir = get_chat_dir(team_number, username)
            recent_dm = find_most_recent_dm_partner(chat_dir, username)
       if recent_dm:
        state['lastDmUser'] = recent_dm
        logger.info(f"Resolved team number {last_dm_user} to username {recent_dm}")
  except Exception as e:
       logger.error(f"Failed to resolve team number to username: {e}")
    
    # CRITICAL FIX: Validate lastSource.id is username
    if 'lastSource' in state and state['lastSource'].get('type') == 'dm':
        source_id = state['lastSource'].get('id', '')
        
        if source_id.isdigit():
      logger.warning(f"Chat state for {username} has team number in lastSource.id: {source_id}")
   
            # Same resolution attempt as above
       try:
        chat_dir = get_chat_dir(team_number, username)
                recent_dm = find_most_recent_dm_partner(chat_dir, username)
              if recent_dm:
        state['lastSource']['id'] = recent_dm
           logger.info(f"Resolved lastSource team number {source_id} to username {recent_dm}")
            except Exception as e:
      logger.error(f"Failed to resolve lastSource team number: {e}")
 
    # Ensure unreadCount exists
    if 'unreadCount' not in state:
        state['unreadCount'] = 0
    
    return jsonify({
        'success': True,
        'state': state
    })

def find_most_recent_dm_partner(chat_dir, username):
    """
    Find the username of the most recent DM conversation partner.
    Scans chat history files in chat_dir and returns the other user's name.
    """
    # Implementation depends on your file structure
    # Example: look for files named "{username}_{other}_chat_history.json"
    #        and return the most recent "other"
    pass
```

### Root Cause: Where Team Numbers Get Stored

**Problem location:** Likely in the web UI's chat state update logic

**File:** `server/routes/chat.py` (web UI routes)

```python
# WRONG - Storing team number:
state['lastDmUser'] = recipient_team_number  # ?

# CORRECT - Store username:
state['lastDmUser'] = recipient_username  # ?
```

**Check these functions:**
1. Chat message send handler
2. Chat state update on message receive
3. Any Socket.IO handlers that update chat state

---

## ?? Mobile App Enhancements

### Enhancement 1: Better Error Detection

**Already implemented** in BackgroundNotificationService.cs:

```csharp
// Check if lastSource.Id looks like a team number
if (System.Text.RegularExpressions.Regex.IsMatch(lastSource.Id, @"^\d+$"))
{
    System.Diagnostics.Debug.WriteLine($"WARNING: lastSource.Id '{lastSource.Id}' appears to be a team number!");
    System.Diagnostics.Debug.WriteLine("This will likely cause API 404 error.");
}

// Check API response for 404
if (messagesResponse != null && !messagesResponse.Success)
{
    if (messagesResponse.Error?.Contains("not found") == true)
 {
        System.Diagnostics.Debug.WriteLine($"User '{lastSource.Id}' not found in API.");
        System.Diagnostics.Debug.WriteLine("Possible causes:");
        System.Diagnostics.Debug.WriteLine("  1. lastSource.Id is a team number instead of username");
        System.Diagnostics.Debug.WriteLine("  2. Server's chat state is returning incorrect data");
        System.Diagnostics.Debug.WriteLine("3. The other user doesn't exist");
    }
}
```

### Enhancement 2: Fallback to Generic Notification

**Current behavior (GOOD):**
- If message fetch fails ? shows generic notification
- User still gets notified
- Better than no notification

**Generic notification format:**
```
Title: "New Message"  
Body: "From 5454"  // Shows team number since that's what we have
```

---

## ?? Testing & Diagnosis

### Test 1: Verify Server Returns Correct Data

```bash
# Get auth token
curl -X POST "https://server/api/mobile/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"scout123","password":"pass","team_number":5454}'

# Check chat state
curl -X GET "https://server/api/mobile/chat/state" \
  -H "Authorization: Bearer $TOKEN"
```

**Expected (CORRECT):**
```json
{
  "state": {
  "lastDmUser": "alice",  // ? Username
    "lastSource": {
      "type": "dm",
      "id": "alice"  // ? Username
    }
  }
}
```

**If you see (WRONG):**
```json
{
  "state": {
    "lastDmUser": "5454",  // ? Team number!
    "lastSource": {
    "type": "dm",
    "id": "5454"  // ? Team number!
    }
  }
}
```

Then the server needs the fix above.

### Test 2: Verify Message Fetch Works

```bash
# Try fetching messages with username (should work)
curl -X GET "https://server/api/mobile/chat/messages?type=dm&user=alice&limit=50" \
  -H "Authorization: Bearer $TOKEN"

# Try fetching with team number (will fail with 404)
curl -X GET "https://server/api/mobile/chat/messages?type=dm&user=5454&limit=50" \
  -H "Authorization: Bearer $TOKEN"
```

**Expected error for team number:**
```json
{
  "success": false,
  "error": "Other user not found",
  "error_code": "USER_NOT_FOUND"
}
```

### Test 3: Check Mobile Logs

```powershell
adb logcat | findstr "BackgroundNotifications"
```

**Look for these warnings:**
```
WARNING: lastSource.Id '5454' appears to be a team number!
User '5454' not found in API.
Possible causes:
  1. lastSource.Id is a team number instead of username
```

---

## ?? Temporary Workaround (If Server Can't Be Fixed Immediately)

If you can't fix the server right away, add this workaround to the mobile app:

**File:** `ObsidianScout/Services/BackgroundNotificationService.cs`

```csharp
private async Task<int> FetchAndShowUnreadMessagesAsync(ChatState chatState)
{
    var lastSource = chatState.LastSource;
    
// WORKAROUND: If lastSource.Id looks like a team number,
    // try to resolve it to a username
    if (lastSource.Type == "dm" && !string.IsNullOrEmpty(lastSource.Id))
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(lastSource.Id, @"^\d+$"))
   {
            System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Detected team number {lastSource.Id}, attempting to resolve to username...");
     
     // Option 1: Look up team members and find one with matching team number
      var membersResponse = await _apiService.GetChatMembersAsync(scope: "team");
            if (membersResponse?.Success == true && membersResponse.Members != null)
       {
     var teamNumber = int.Parse(lastSource.Id);
                var member = membersResponse.Members.FirstOrDefault(m => m.TeamNumber == teamNumber);
    
         if (member != null && !string.IsNullOrEmpty(member.Username))
  {
           System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Resolved team number {teamNumber} to username: {member.Username}");
        lastSource.Id = member.Username;  // Replace team number with username
       }
 else
          {
          System.Diagnostics.Debug.WriteLine($"[BackgroundNotifications] Could not resolve team number {teamNumber} to username");
   }
            }
 }
    }
    
    // Continue with normal message fetching...
    var messagesResponse = await _apiService.GetChatMessagesAsync(
        type: "dm",
        user: lastSource.Id,  // Now uses username instead of team number
        limit: 50
    );
    
    // Rest of existing code...
}
```

**Pros:** Works around server issue  
**Cons:** Extra API call, slower, not a real fix

---

## ? Complete Fix Checklist

### Server-Side (REQUIRED)

- [ ] Update `/api/mobile/chat/state` endpoint to validate data
- [ ] Add check: if `lastDmUser` is all digits ? log warning
- [ ] Add check: if `lastSource.id` is all digits ? log warning
- [ ] Attempt to resolve team numbers to usernames (fallback)
- [ ] Find where team numbers are being stored in state files
- [ ] Fix chat message handlers to store usernames, not team numbers
- [ ] Test: verify state endpoint returns usernames
- [ ] Test: verify message fetch works with returned usernames

### Mobile App (COMPLETED)

- [x] Add diagnostic logging for team number detection
- [x] Add better error messages when 404 occurs
- [x] Fallback to generic notification when fetch fails
- [x] Log comprehensive troubleshooting info

### Testing

- [ ] Send message from web UI ? verify state has username
- [ ] Check mobile logs for team number warnings
- [ ] Verify message fetch succeeds
- [ ] Verify notifications show actual message text
- [ ] Verify tapping notification opens chat

---

## ?? Expected Behavior After Fix

### Before Fix ?

```
1. Server state returns: lastDmUser: "5454" (team number)
2. App tries: GET /messages?user=5454
3. API returns: 404 "Other user not found"
4. App shows: Generic notification "New Message - From 5454"
5. Tap notification: Doesn't open chat (invalid user)
```

### After Fix ?

```
1. Server state returns: lastDmUser: "alice" (username)
2. App tries: GET /messages?user=alice
3. API returns: 200 with messages array
4. App shows: Individual notifications with message text
5. Tap notification: Opens DM with alice
```

---

## ?? Deploy

### Server

```bash
# 1. Apply server fix
vim server/routes/mobile/chat.py  # Add validation/resolution logic

# 2. Restart server
sudo systemctl restart obsidian-scout
# OR
python app.py

# 3. Test endpoint
curl -H "Authorization: Bearer $TOKEN" \
  "https://server/api/mobile/chat/state"

# Verify response has usernames, not team numbers
```

### Mobile App

```powershell
# Already updated - just redeploy
dotnet clean
dotnet build -f net10.0-android
dotnet build -t:Run -f net10.0-android
```

---

## ?? Summary

**Root Cause:** Server returns team numbers instead of usernames in chat state

**Impact:**
- Message fetch returns 404
- Only generic notifications shown
- Deep linking doesn't work

**Fix:**
1. **Server (REQUIRED):** Update `/api/mobile/chat/state` to return usernames
2. **Server (REQUIRED):** Fix wherever team numbers are being stored in state
3. **Mobile (DONE):** Better error detection and logging

**Status:**
- Mobile diagnostics: ? Complete
- Server fix needed: ? Pending
- Workaround available: ? Optional

---

**Deploy server fix, then test! ??**
