# Cache Service

`ObsidianScout.Services.CacheService` is the centralized offline caching layer.

Summary

- Stores JSON payloads in `SecureStorage` when possible (encrypted) with a file fallback in the app data directory for large items.
- Maintains per-key timestamps: `_{created|updated}` and a legacy `_timestamp` for backward compatibility.
- Provides typed getters/setters for `GameConfig`, `PitConfig`, `Events`, `Teams`, `Matches`, `ScoutingData`, `TeamMetrics`, `AvailableMetrics`, and `ProfilePicture`.
- Uses `System.Text.Json` for serialization with case-insensitive property names.

Key behaviors

- `PreloadAllDataAsync` checks whether cached data exists and is younger than24h before triggering a background refresh.
- `ClearAllCacheAsync` removes known cache keys from `SecureStorage` and deletes `cache_*.json` files from the app data directory.
- Timestamps are saved in ISO8601 "O" format as UTC.

Implementation notes

- `SaveStringToCacheAsync` tries `SecureStorage.SetAsync` first and falls back to writing a `.json` file in `FileSystem.AppDataDirectory` on failure.
- `GetStringFromCacheAsync` tries `SecureStorage.GetAsync` first and falls back to reading `.json` files from `FileSystem.AppDataDirectory`.

Caveats

- `SecureStorage` may not be available in some environments (simulators or limited platforms); file fallback is used for reliability.
- Sensitive data should still be minimized even when SecureStorage is available.

For more details, see the source file: `ObsidianScout/Services/CacheService.cs`.
