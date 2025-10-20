# Submit Debug - Quick Reference

## What to Do

1. **Run in Debug mode** (F5)
2. **Open Output window** (Ctrl+Alt+O)
3. **Set to "Debug"** output
4. **Click Submit**
5. **Copy the debug output**

## What You'll See

```
=== SUBMIT SCOUTING DATA ===
Team ID: X
Match ID: Y
Data Fields: N
[... field details ...]

=== API: SUBMIT SCOUTING DATA ===
Endpoint: https://...
Status Code: XXX
Response Body: {...}
Parsed Success: true/false
Parsed Error: [message]
```

## Quick Diagnosis

| Debug Line | Status | What It Means | Fix |
|------------|--------|---------------|-----|
| `Status Code: 200` + `Parsed Success: True` | ? | Works! | - |
| `Status Code: 401` | ? | No auth | Re-login |
| `Status Code: 400` | ? | Bad data | Check request body |
| `Status Code: 404` | ? | Wrong URL | Check settings |
| `Status Code: 500` | ? | Server crash | Check server logs |
| `Parsed Success: False` | ? | Server rejected | Check "Parsed Error" |
| `Network error` | ? | Can't connect | Check server running |
| `JSON error` | ? | Bad response | Check response body |
| `Timeout` | ? | Too slow | Check server perf |

## Key Debug Lines

**Most Important:**
```
Status Code: [number]
Response Body: [json]
Parsed Success: [true/false]
Parsed Error: [message]
```

**These tell you exactly what failed!**

## Common Errors

### "Status Code: 401"
? Token expired ? Logout & login again

### "Status Code: 400"  
? Wrong data format ? Compare with QR data

### "Parsed Success: False"
? Server said no ? Check "Parsed Error:" line

### "Network error"
? Can't reach server ? Is it running?

## Share This for Help

Copy these sections from Output window:

1. `=== SUBMIT SCOUTING DATA ===` section
2. `Status Code:` line
3. `Response Body:` line
4. `Parsed Error:` line
5. Any exception sections

## Success Looks Like

```
Status Code: 200 OK
Parsed Success: True
SUCCESS! Scouting entry created with ID: 42
```

Status bar: `? Scouting data submitted successfully!`

---

**TL;DR:** Run in debug, look at Output window, check Status Code and Parsed Error, that tells you what's wrong.
