# Settings Page - Quick Reference

## What Was Added

? **Settings Page** with:
- ?? Dark/Light theme switching
- ?? Cache management (view & clear)
- ?? App information

## How to Use

### Change Theme
1. Open menu ? **Settings**
2. Toggle **Dark Mode** switch
3. Theme changes instantly ?

### Clear Cache
1. Open menu ? **Settings**
2. View cache status
3. Click **Clear Cache**
4. Confirm when prompted

## Key Files

```
Created:
??? ViewModels/SettingsViewModel.cs      (Logic)
??? Views/SettingsPage.xaml     (UI)
??? Views/SettingsPage.xaml.cs      (Code-behind)

Modified:
??? Services/SettingsService.cs       (Theme storage)
??? App.xaml.cs (Theme init)
??? AppShell.xaml             (Menu item)
??? MauiProgram.cs (DI registration)
```

## Features

### Theme Switching
- **Persists** across app restarts
- **Instant** theme changes
- **System aware** (follows platform)

### Cache Management
- Shows **cache age** 
- **One-click clear**
- **Auto-refresh** status
- Clears all: Events, Teams, Matches, Scouting Data, etc.

## UI Highlights

```
Settings
??? ?? Appearance
?   ??? Dark Mode Toggle
?   ??? Current Theme Display
??? ?? Cache Management  
?   ??? Cache Status
?   ??? Refresh Button
?   ??? Clear Cache Button
??? ?? About
    ??? App Name
    ??? Description
    ??? Version
```

## Code Examples

### Access Settings (User)
```
Menu ? Settings
```

### Change Theme (Code)
```csharp
await _settingsService.SetThemeAsync("Dark");
Application.Current.UserAppTheme = AppTheme.Dark;
```

### Clear Cache (Code)
```csharp
await _cacheService.ClearAllCacheAsync();
```

## Status Messages

- ? Success (auto-clears after 2s)
- ? Error (persists)
- ? Loading (shows spinner)

## Design

- **Glass-morphism** styling
- **Theme-aware** colors
- **Responsive** layout
- **Consistent** with app design

## Testing

Quick test checklist:
- [ ] Theme switch works
- [ ] Theme persists after restart
- [ ] Cache clears
- [ ] Status messages appear
- [ ] Menu shows "Settings"
- [ ] UI looks good in both themes

## Build Status

? **Build Successful**
- No errors
- All files compile
- DI properly configured
