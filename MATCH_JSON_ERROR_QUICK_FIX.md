# ?? MATCH JSON ERROR - QUICK FIX

## Problem
```
The JSON value could not be converted to System.Int32. 
Path: $.matches[87].match_number
```

**Cause**: Server returning invalid `match_number` (empty string, null, or non-numeric).

---

## ? Solution Applied

### Custom JSON Converters
Added safe converters that handle bad data:

```csharp
// For required integers (defaults to 0)
[JsonPropertyName("match_number")]
[JsonConverter(typeof(SafeIntJsonConverter))]
public int MatchNumber { get; set; }

// For optional integers (defaults to null)
[JsonPropertyName("red_score")]
[JsonConverter(typeof(SafeNullableIntJsonConverter))]
public int? RedScore { get; set; }
```

---

## ?? What It Does

### SafeIntJsonConverter
- Valid number ? parse normally
- String number ? convert to int
- Empty/null/invalid ? returns `0`
- Logs issue to Debug Output

### SafeNullableIntJsonConverter
- Valid number ? parse normally
- String number ? convert to int
- Empty/null/invalid ? returns `null`
- Logs issue to Debug Output

---

## ?? Test Results

| Input | SafeInt | SafeNullableInt |
|-------|---------|-----------------|
| `5` | `5` | `5` |
| `"5"` | `5` | `5` |
| `""` | `0` | `null` |
| `null` | `0` | `null` |
| `"abc"` | `0` | `null` |

---

## ?? Quick Test

1. Open app
2. Go to Matches page
3. Select Event ID 4 (previously crashed)
4. ? Should load without error
5. Check Debug Output for warnings

---

## ?? Debug Output Example

```
[SafeIntConverter] Could not parse 'abc' as int, defaulting to 0
[SafeNullableIntConverter] Could not parse 'xyz' as int, returning null
```

---

## ?? Server Fix Needed

Check database for bad data:
```sql
SELECT id, match_number, match_type 
FROM matches 
WHERE match_number IS NULL 
   OR CAST(match_number AS VARCHAR) = '';
```

Fix:
```sql
UPDATE matches 
SET match_number = 0 
WHERE match_number IS NULL;
```

---

## ? Status

- ? Fix applied to Match.cs
- ? Build successful
- ? App won't crash on bad data
- ? Debug logging added
- ?? Server should fix source data

**Result**: App now handles invalid JSON gracefully!
