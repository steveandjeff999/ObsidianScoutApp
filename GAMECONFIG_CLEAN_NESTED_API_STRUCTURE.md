# GameConfig Model - Clean Nested API Structure

## Summary

Removed all deprecated flat API properties from `GameConfig` model. Now **only uses proper nested structure** matching the server.

## What Was Removed

### **? Deleted Properties:**
```csharp
// These are GONE - no longer in model
[JsonPropertyName("first_api_username")]
public string? FirstApiUsername { get; set; }

[JsonPropertyName("first_api_key")]
public string? FirstApiKey { get; set; }

[JsonPropertyName("tba_api_key")]
public string? TbaApiKey { get; set; }
```

### **? What Remains (Clean Structure):**
```csharp
public class GameConfig
{
    // ... other properties ...

    // ONLY nested API settings
    [JsonPropertyName("api_settings")]
    public FirstApiSettings? ApiSettings { get; set; }

    [JsonPropertyName("tba_api_settings")]
    public TbaApiSettings? TbaApiSettings { get; set; }

    [JsonPropertyName("preferred_api_source")]
    public string? PreferredApiSource { get; set; }
}

public class FirstApiSettings
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("auth_token")]
    public string? AuthToken { get; set; }

    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = "https://frc-api.firstinspires.org";

    [JsonPropertyName("auto_sync_enabled")]
    public bool AutoSyncEnabled { get; set; } = true;
}

public class TbaApiSettings
{
    [JsonPropertyName("auth_key")]
    public string? AuthKey { get; set; }

    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = "https://www.thebluealliance.com/api/v3";
}
```

## JSON Output Comparison

### **Before (Had Extra Null Fields):**
```json
{
  "version": "1.0.3",
  "api_settings": {
    "username": "jeffstumps",
    "auth_token": "1dd5bcdb-5b9e-4269-a45d-234af783813a",
    "base_url": "https://frc-api.firstinspires.org",
    "auto_sync_enabled": true
  },
  "tba_api_settings": {
    "auth_key": "hae7pfixkaYpROTHhMx6XQ5qLkjT5v7jX7IymIp3sFadVOTsboxkSVJlYu4yoq9a",
    "base_url": "https://www.thebluealliance.com/api/v3"
  },
  "preferred_api_source": "first",
  "first_api_username": null,     // ? UNWANTED
  "first_api_key": null,           // ? UNWANTED
  "tba_api_key": null          // ? UNWANTED
}
```

### **After (Clean, No Nulls):**
```json
{
  "version": "1.0.3",
  "api_settings": {
    "username": "jeffstumps",
    "auth_token": "1dd5bcdb-5b9e-4269-a45d-234af783813a",
    "base_url": "https://frc-api.firstinspires.org",
    "auto_sync_enabled": true
  },
  "tba_api_settings": {
    "auth_key": "hae7pfixkaYpROTHhMx6XQ5qLkjT5v7jX7IymIp3sFadVOTsboxkSVJlYu4yoq9a",
    "base_url": "https://www.thebluealliance.com/api/v3"
  },
  "preferred_api_source": "first"
  // ? No more null flat properties!
}
```

## Benefits

? **Clean JSON**: No more `null` values for deprecated properties
? **Proper Structure**: Only nested `api_settings` and `tba_api_settings`
? **Matches Server**: Exactly matches server's expected structure
? **Smaller Payload**: 3 fewer properties in every JSON response
? **No Confusion**: Developers only see the correct structure

## Migration Notes

### **Breaking Change?**
**No** - If server still sends old flat properties, they'll just be ignored by the deserializer. The server will see the new nested structure from the app.

### **Backward Compatibility**
If you need to support old server versions that expect flat properties:

```csharp
// In SaveAsync, before sending to server:
if (CurrentConfig != null && CurrentConfig.ApiSettings != null)
{
    // Map nested to flat for old servers
    CurrentConfig.FirstApiUsername = CurrentConfig.ApiSettings.Username;
    CurrentConfig.FirstApiKey = CurrentConfig.ApiSettings.AuthToken;
}
```

But based on your server JSON, it already uses the nested structure, so this is not needed.

## Testing

Test that these scenarios work:

### **1. Load Config from Server**
```json
Server sends:
{
  "api_settings": { "username": "test" },
  "tba_api_settings": { "auth_key": "key123" }
}

App receives:
? ApiSettings.Username = "test"
? TbaApiSettings.AuthKey = "key123"
? No null properties in model
```

### **2. Save Config to Server**
```json
App sends:
{
  "api_settings": { "username": "test" },
  "tba_api_settings": { "auth_key": "key123" },
  "preferred_api_source": "first"
}

? Does NOT send:
{
  "first_api_username": null,
  "first_api_key": null,
  "tba_api_key": null
}
```

### **3. Form Editor**
- Load config ? API fields populate
- Edit FIRST username ? saves to `ApiSettings.Username`
- Edit TBA key ? saves to `TbaApiSettings.AuthKey`
- Save ? sends only nested structure

## Files Modified

1. ? `ObsidianScout/Models/GameConfig.cs`
   - **Removed**: `FirstApiUsername` property
   - **Removed**: `FirstApiKey` property
   - **Removed**: `TbaApiKey` property
   - **Kept**: `ApiSettings` (nested)
   - **Kept**: `TbaApiSettings` (nested)
   - **Kept**: `PreferredApiSource`

## Example Usage

### **Accessing FIRST API Username:**
```csharp
// ? OLD WAY (no longer exists)
// var username = config.FirstApiUsername;

// ? NEW WAY (correct)
var username = config.ApiSettings?.Username;
```

### **Accessing TBA API Key:**
```csharp
// ? OLD WAY (no longer exists)
// var key = config.TbaApiKey;

// ? NEW WAY (correct)
var key = config.TbaApiSettings?.AuthKey;
```

### **Setting API Values:**
```csharp
// ? Initialize if null
if (config.ApiSettings == null)
    config.ApiSettings = new FirstApiSettings();

// ? Set values
config.ApiSettings.Username = "jeffstumps";
config.ApiSettings.AuthToken = "1dd5bcdb-5b9e-4269-a45d-234af783813a";
config.ApiSettings.AutoSyncEnabled = true;
```

## Verification Checklist

- [ ] Load config from server - no null flat properties
- [ ] Save config to server - JSON doesn't include flat properties
- [ ] Form Editor shows nested API settings correctly
- [ ] Editing FIRST API username saves to `ApiSettings.Username`
- [ ] Editing TBA API key saves to `TbaApiSettings.AuthKey`
- [ ] Raw JSON view shows clean nested structure
- [ ] No compilation errors related to removed properties

---

**Status**: ? **CLEAN MODEL - NO MORE DEPRECATED PROPERTIES**
**Impact**: JSON output is now clean with only proper nested API structure
**Breaking Changes**: None (server already uses nested structure)
