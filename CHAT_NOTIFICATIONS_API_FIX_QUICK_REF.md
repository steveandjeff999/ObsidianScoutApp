# ?? Chat Notifications - Quick Fix Reference

## ?? Problem

**Logs show:**
```
[BackgroundNotifications] Fetching DM messages with user: 5454
[API] Status: 404 NotFound
[API] Response: "Other user not found"
```

**Symptoms:**
- ? Shows generic "New message from 5454" instead of actual message
- ? Clicking notification doesn't open chat
- ? API returns 404 error

**Root Cause:** Server returns team number (5454) instead of username in chat state

---

## ?? Fix Required: SERVER-SIDE

### Check Server Response

```bash
curl -H "Authorization: Bearer $TOKEN" \
  "https://server/api/mobile/chat/state"
```

**If you see (WRONG):**
```json
{
  "lastDmUser": "5454",  // ? Team number!
  "lastSource": { "id": "5454" }  // ? Team number!
}
```

**Should be (CORRECT):**
```json
{
  "lastDmUser": "alice",  // ? Username!
  "lastSource": { "id": "alice" }  // ? Username!
}
```

### Server Fix

**File:** `server/routes/mobile/chat.py`

```python
@mobile_bp.route('/chat/state', methods=['GET'])
def get_chat_state():
    # ... existing code ...
    
    # FIX: Validate lastDmUser is username, not team number
    if 'lastDmUser' in state and state['lastDmUser'].isdigit():
        logger.warning(f"Chat state has team number: {state['lastDmUser']}")
  # Resolve to username or remove
        
    # FIX: Validate lastSource.id is username
    if 'lastSource' in state:
        source_id = state['lastSource'].get('id')
        if source_id and source_id.isdigit():
         logger.warning(f"lastSource has team number: {source_id}")
     # Resolve to username or remove
    
    return jsonify({'success': True, 'state': state})
```

---

## ?? Mobile App Status

### ? Already Fixed

- Detects team number in response
- Logs warning messages
- Falls back to generic notification
- Provides diagnostic info

### ? Waiting For

- Server to return usernames instead of team numbers
- Then notifications will show message text
- Then deep linking will work

---

## ?? Testing

### Step 1: Verify Problem

```powershell
# Check mobile logs
adb logcat | findstr "WARNING.*team number"
```

**If you see:**
```
WARNING: lastSource.Id '5454' appears to be a team number!
This will likely cause API 404 error.
```

Then server needs fix.

### Step 2: Test Server Fix

```bash
# After server fix, test again
curl -H "Authorization: Bearer $TOKEN" \
  "https://server/api/mobile/chat/state"
```

Should return username, not team number.

### Step 3: Test Mobile

1. Have someone send message
2. Wait 60-120 seconds
3. Should see notification with actual message text
4. Tap notification
5. Should open chat with that person

---

## ? Success Indicators

- [ ] `/api/mobile/chat/state` returns usernames
- [ ] No "team number" warnings in mobile logs
- [ ] Notifications show actual message text
- [ ] Tapping notification opens correct chat
- [ ] No 404 errors in API logs

---

## ?? Summary

**Problem:** Server API returns team numbers where it should return usernames

**Mobile App:** ? Updated with diagnostics  
**Server:** ? Needs fix to return usernames  
**Deploy:** Fix server ? Restart ? Test

**See:** `CHAT_NOTIFICATIONS_API_FIX_COMPLETE.md` for full details

---

**Status:** Diagnosed + Mobile Enhanced ?  
**Next:** Server fix required ?
