# ?? Individual Chat Notifications - Quick Reference

## ?? Deploy

```powershell
# STOP app if running (required for interface changes)
dotnet clean
dotnet build -f net10.0-android
dotnet build -t:Run -f net10.0-android
```

---

## ? What You Get

### Before
```
Single notification:
"2 New Messages - From alice"
```

### After
```
2 separate notifications:
"?? alice"
  "Hey, meet at the pit"

"?? alice"
  "In 5 minutes"
```

---

## ?? Features

| Feature | Status |
|---------|--------|
| Separate notification per message | ? |
| Shows actual message text | ? |
| Tap opens that chat | ? |
| Deep linking | ? |
| No duplicates | ? |
| Sound & vibration | ? |

---

## ?? Examples

### DM
```
Title: ?? alice
Body: Hey, meet at the pit
Tap ? Opens DM with alice
```

### Group
```
Title: ?? bob in scouting_team
Body: Strategy meeting after Match 15
Tap ? Opens group "scouting_team"
```

---

## ?? Quick Test

```
1. Have someone send you 2 messages
2. Wait 60-120 seconds
3. Should see 2 separate notifications
4. Tap one ? Opens that chat
```

---

## ? Success Indicators

- [ ] Multiple individual notifications
- [ ] Each shows message text
- [ ] Tapping opens correct chat
- [ ] No duplicate notifications

---

**Status:** ? Ready  
**Build:** ? Successful  
**Deploy:** Stop app ? Clean ? Build ? Run! ??
