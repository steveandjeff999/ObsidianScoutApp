using System;
using System.Text.Json;

namespace ObsidianScout.Services
{
    internal static class JsonUtils
    {
        // Try to deserialize JSON into T. Returns true on success, false on failure with error message.
        public static bool TryDeserialize<T>(string json, JsonSerializerOptions options, out T? result, out string? errorMessage)
        {
            result = default;
            errorMessage = null;
            if (string.IsNullOrEmpty(json))
            {
                errorMessage = "Empty JSON";
                return false;
            }

            try
            {
                result = JsonSerializer.Deserialize<T>(json, options);
                return true;
            }
            catch (JsonException jex)
            {
                errorMessage = jex.Message;
                System.Diagnostics.Debug.WriteLine($"[JsonUtils] JSON parse error: {jex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[JsonUtils] Unexpected error deserializing JSON: {ex.Message}");
                return false;
            }
        }
    }
}
