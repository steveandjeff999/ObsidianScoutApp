# What to Look For in Debug Output

## The Golden Lines

These 4 lines tell you everything:

```
Status Code: [XXX] [Status Name]
Response Body: {"success":true/false,...}
Parsed Success: [true/false]
Parsed Error: [error message]
```

## Scenarios

### ? SUCCESS

```
Status Code: 200 OK
Response Body: {"success":true,"scouting_id":42,"message":"Created"}
Parsed Success: True
Parsed Error: 
```

**You'll see:** `? Scouting data submitted successfully!`

---

### ? FAILURE: Need to Re-Login

```
Status Code: 401 Unauthorized
Response Body: {"success":false,"error":"Invalid or expired token"}
Parsed Success: False
Parsed Error: Invalid or expired token
```

**Fix:** Click Logout, then Login again

---

### ? FAILURE: Wrong Data Format

```
Status Code: 400 BadRequest
Response Body: {"success":false,"error":"Missing required field: alliance"}
Parsed Success: False
Parsed Error: Missing required field: alliance
```

**Fix:** Server expects different data structure

---

### ? FAILURE: Server Down

```
=== HTTP REQUEST EXCEPTION ===
Message: No connection could be made
```

**Fix:** Start your server

---

### ? FAILURE: Wrong URL

```
Status Code: 404 NotFound
Response Body: Cannot GET /api/mobile/scouting/submit
```

**Fix:** Check server URL in settings

---

### ? FAILURE: Server Error

```
Status Code: 500 InternalServerError
Response Body: {"success":false,"error":"Database connection failed"}
Parsed Success: False
Parsed Error: Database connection failed
```

**Fix:** Check server logs, fix server issue

---

## Step-by-Step Debugging

### 1. Is request being sent?

Look for:
```
=== SUBMIT SCOUTING DATA ===
```

If missing ? Button not working

### 2. What's the endpoint?

Look for:
```
Endpoint: https://192.168.1.100:3000/api/mobile/scouting/submit
```

Verify:
- Protocol (http/https)
- IP address
- Port
- Path

### 3. Is auth present?

Look for:
```
Auth: Bearer eyJhbGciOiJ...
```

If `Auth: NONE` ? Need to login

### 4. What's being sent?

Look at:
```
Request Body:
{
  "team_id": 5,
  "match_id": 528,
  "data": { ... }
}
```

Compare with QR code data

### 5. What did server say?

Look at:
```
Status Code: [number]
Response Body: [json]
```

This tells you what went wrong

### 6. Did parsing work?

Look at:
```
Parsed Success: [true/false]
Parsed Error: [message]
```

If parsing failed ? Server returned bad JSON

## Pro Tips

### Compare with Working QR

1. Generate QR code (works)
2. Decode QR to see JSON
3. Look at Submit request JSON in debug
4. They should be similar

### Test Minimal Submit

Try this temporarily:

```csharp
// In SubmitAsync, after creating submission:
submission.Data.Clear();
submission.Data["test"] = "value";
```

If minimal works ? Problem with field data
If minimal fails ? Problem with auth/server

### Check Server Logs

Look at your server console/logs when submit happens:
- Does server receive the request?
- What error does server log?
- Does it show the data?

## Copy This to Share

When asking for help, copy these from Output window:

```
=== SUBMIT SCOUTING DATA ===
[full section]

=== API: SUBMIT SCOUTING DATA ===
Endpoint: [url]
Team ID: [id]
Match ID: [id]
Auth: [token preview]
Request Body:
[json]

Status Code: [code]
Response Body: [response]

=== SUBMIT RESULT ===
Success: [true/false]
Error: [message]
```

## Checklist

Before asking for help, verify:

- [ ] Server is running
- [ ] Correct server URL in settings
- [ ] Logged in (Auth header present)
- [ ] Team and Match selected
- [ ] Form has data filled in
- [ ] Output window shows debug logs
- [ ] Checked Status Code
- [ ] Read Parsed Error message
- [ ] Compared request with QR data

## Most Likely Issues

Based on symptoms:

**"X Failed to submit scouting data"** with no details
? Check if debug logging appears at all

**Status 401**
? 90% chance: Token expired, need to re-login

**Status 400**
? 90% chance: Data format doesn't match server expectations

**Status 500**
? 90% chance: Server-side bug, check server logs

**Network error**
? 90% chance: Server not running or wrong URL

**Parsed Success: False with 200 status**
? 90% chance: Server validation failed, check Parsed Error

---

**Bottom Line:** The debug output will tell you EXACTLY what's wrong. Just read it carefully!
