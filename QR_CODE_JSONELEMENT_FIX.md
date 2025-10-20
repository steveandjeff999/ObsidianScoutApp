# QR Code JsonElement Casting Fix

## Problem

When trying to generate a QR code, the following error occurred:

```
? Error generating QR code: Unable to cast object of type 'System.Text.Json.JsonElement' to type 'System.IConvertible'.
```

## Root Cause

The issue was that some field values in the `fieldValues` dictionary were stored as `JsonElement` objects (from JSON deserialization). When trying to use `Convert.ToInt32()`, `Convert.ToBoolean()`, or `ToString()` on these values during QR code generation, the conversion failed because `JsonElement` doesn't implement `IConvertible`.

## Solution

Added three safe conversion helper methods that properly handle `JsonElement` types:

### 1. `ConvertValueForSerialization(object? value)`
Converts any value (including JsonElement) to a simple serializable type:
- Numbers ? int or double
- Strings ? string
- Booleans ? bool
- Null ? null

### 2. `SafeConvertToInt(object? value)`
Safely converts values to integers, handling:
- `JsonElement` with number values
- `JsonElement` with string values
- Regular int values
- String values
- Fallback to 0 on error

### 3. `SafeConvertToBool(object? value)`
Safely converts values to booleans, handling:
- `JsonElement` with True/False
- `JsonElement` with string "true"/"false"
- Regular bool values
- String values
- Fallback to false on error

### 4. `SafeConvertToString(object? value)`
Safely converts values to strings, handling:
- `JsonElement` with string values
- `JsonElement` of any type
- Regular string values
- Fallback to empty string on error

## Changes Made

### File: `ObsidianScout\ViewModels\ScoutingViewModel.cs`

1. **Added using directive:**
   ```csharp
   using System.Text.Json;
   ```

2. **Added helper methods** (lines ~330-430):
   - `ConvertValueForSerialization()`
   - `SafeConvertToInt()`
   - `SafeConvertToBool()`
   - `SafeConvertToString()`

3. **Updated `SaveWithQRCodeAsync()` method** to use safe conversions:
   
   **Before:**
   ```csharp
   foreach (var kvp in fieldValues)
   {
       qrData[kvp.Key] = kvp.Value; // Could be JsonElement!
   }
   
   var count = Convert.ToInt32(value ?? 0); // Throws exception on JsonElement
   ```
   
   **After:**
   ```csharp
   foreach (var kvp in fieldValues)
   {
       qrData[kvp.Key] = ConvertValueForSerialization(kvp.Value); // Handles JsonElement
   }
   
   var count = SafeConvertToInt(value); // Safely handles JsonElement
   ```

## How It Works

### JsonElement Detection and Conversion

The helper methods check if a value is a `JsonElement` using pattern matching:

```csharp
if (value is JsonElement jsonElement)
{
    return jsonElement.ValueKind switch
    {
        JsonValueKind.Number => jsonElement.GetInt32(),
        JsonValueKind.String => jsonElement.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        // ... etc
    };
}
```

### Safe Fallbacks

Each method has multiple fallback strategies:
1. Check for `JsonElement` first
2. Check for native type (int, bool, string)
3. Try string parsing
4. Try general Convert class
5. Return safe default (0, false, empty string)

## Benefits

? **Handles JsonElement gracefully** - No more casting exceptions  
? **Maintains type safety** - Proper conversions for each data type  
? **Robust error handling** - Never throws exceptions on conversion  
? **Works with all field types** - Counters, booleans, text, multiple choice  
? **Preserves data** - Accurate conversion of all value types  

## Testing

To verify the fix:

1. Fill out a scouting form with various field types:
   - Counter fields (auto/teleop scoring)
   - Boolean fields (checkboxes)
   - Multiple choice fields (dropdown)
   - Text fields (comments)

2. Click "Save with QR"

3. Verify:
   - ? No error message appears
   - ? QR code displays correctly
   - ? "QR Code generated successfully!" message shows
   - ? Scan QR code to verify data is correct

## Example QR Code Data

After the fix, QR codes properly contain all field data:

```json
{
  "team_id": 5,
  "team_number": 16,
  "match_id": 528,
  "match_number": 1,
  "alliance": "unknown",
  "scout_name": "John Doe",
  "elem_auto_1": true,           // Boolean converted correctly
  "elem_auto_2": 5,              // Counter converted correctly
  "elem_teleop_1": 12,           // Counter converted correctly
  "elem_endgame_2": "Climb",     // String converted correctly
  "defense_rating": 3,           // Rating converted correctly
  "general_comments": "Great!",  // Text converted correctly
  "auto_points_points": 25,
  "teleop_points_points": 60,
  "endgame_points_points": 15,
  "total_points_points": 100,
  "generated_at": "2025-01-18T23:59:33.657Z",
  "offline_generated": true
}
```

## Technical Notes

### Why JsonElement?

When the game configuration is loaded from the API, default values for form fields may be stored as `JsonElement` objects. This happens when:
- The JSON deserializer stores dynamic/unknown types as `JsonElement`
- Default values from the config are used to initialize `fieldValues`
- The type isn't known at compile time

### Performance

The helper methods add minimal overhead:
- Pattern matching is very fast in .NET 10
- Only processes values once during QR generation
- No reflection or complex parsing needed

### Future Improvements

Consider:
- Cache converted values to avoid repeated conversions
- Add logging to track conversion issues
- Support more complex types (arrays, nested objects)

## Related Files

- `Services/QRCodeService.cs` - QR code generation service
- `Views/ScoutingPage.xaml.cs` - UI that triggers QR generation
- `Models/GameConfig.cs` - Source of default values

## Summary

The fix ensures that all field values, regardless of their internal representation (JsonElement or native types), are properly converted to simple, serializable types before generating the QR code. This eliminates the casting exception and produces valid QR codes with complete scouting data.
