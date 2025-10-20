# JsonElement Conversion Fix

## The Problem

When loading game configuration from the API, the `element.Default` property was being deserialized as a `System.Text.Json.JsonElement` object rather than a native C# type. When the code tried to convert this using `Convert.ToBoolean()`, it threw:

```
System.InvalidCastException: Unable to cast object of type 'System.Text.Json.JsonElement' to type 'System.IConvertible'.
```

This happened specifically with boolean defaults like:
```json
{
  "id": "elem_auto_1",
  "name": "Leave Starting Line",
  "type": "boolean",
  "default": false
}
```

## The Solution

Added three helper methods to safely convert `JsonElement` objects to native C# types:

### 1. `ConvertToBoolean(object? value)`

Handles:
- `JsonElement` with `ValueKind.True` or `ValueKind.False`
- Direct `bool` values
- String representations ("true"/"false")
- General conversion with fallback

```csharp
private bool ConvertToBoolean(object? value)
{
    if (value == null) return false;

    try
    {
        // Handle JsonElement
        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.True) return true;
            if (jsonElement.ValueKind == JsonValueKind.False) return false;
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                var str = jsonElement.GetString();
                return bool.TryParse(str, out var result) && result;
            }
            return false;
        }

        // Handle direct boolean
        if (value is bool boolValue) return boolValue;

        // Try string conversion
        if (value is string strValue)
        {
            return bool.TryParse(strValue, out var result) && result;
        }

        // Try general conversion
        return Convert.ToBoolean(value);
    }
    catch
    {
        return false;
    }
}
```

### 2. `ConvertToInt(object? value, int defaultValue = 0)`

Handles:
- `JsonElement` with `ValueKind.Number`
- Direct `int` values
- String representations
- Fallback to default value

### 3. `ConvertToString(object? value, string defaultValue = "")`

Handles:
- `JsonElement` with `ValueKind.String`
- Direct string values
- ToString() conversion
- Fallback to default value

## Where Applied

### Boolean Elements
```csharp
var checkBox = new CheckBox
{
    IsChecked = ConvertToBoolean(element.Default) // Was: Convert.ToBoolean(element.Default)
};
```

### Multiple Choice Elements
```csharp
if (element.Default != null)
{
    var defaultName = ConvertToString(element.Default); // Was: element.Default.ToString()
    defaultIndex = element.Options.FindIndex(o => o.Name == defaultName);
}
```

## Why This Happened

When System.Text.Json deserializes JSON, dynamic properties (like `object? Default`) are deserialized as `JsonElement` objects that hold the raw JSON value. These need to be explicitly converted to the target type using the appropriate `JsonElement` methods:

- `GetBoolean()` for booleans
- `GetInt32()` for integers
- `GetString()` for strings

## Benefits

1. **Type Safety**: Handles JsonElement properly without casting errors
2. **Fallback Logic**: Returns sensible defaults when conversion fails
3. **Flexibility**: Works with multiple input types (JsonElement, native types, strings)
4. **No Crashes**: Try-catch ensures app doesn't crash on unexpected values

## Testing

Test with different game configurations:

**Boolean defaults:**
```json
{"default": false}  // Works
{"default": true}   // Works
{"default": "false"} // Works (string conversion)
```

**Integer defaults:**
```json
{"default": 0}      // Works
{"default": "5"}    // Works (string conversion)
```

**String defaults:**
```json
{"default": "Park"} // Works
{"default": null}   // Works (returns empty string)
```

## Build Status

? **Build**: Successful  
? **JsonElement Handling**: Implemented  
? **No Cast Exceptions**: Fixed  
? **All Default Types**: Supported

The form should now load without any `InvalidCastException` errors!
