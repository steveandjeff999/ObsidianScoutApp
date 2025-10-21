# Settings Page Implementation

## Summary
Added a comprehensive Settings page to the ObsidianScout app with cache management and theme switching functionality.

## Features Implemented

### 1. **Theme Switching**
- Toggle between Light and Dark mode
- Theme preference persists across app restarts
- Immediately applies theme changes
- Visual feedback with current theme display (?? Light / ?? Dark)

### 2. **Cache Management**
- View cache status with age information
- Clear all cached data with one button
- Refresh cache status on demand
- Visual feedback during cache operations
- Clears: Events, Teams, Matches, Scouting Data, Team Metrics, Game Config

### 3. **User Experience**
- Modern glass-morphism UI design
- Responsive status messages
- Loading indicators for async operations
- Consistent with app's Liquid Glass design language
- Theme-aware colors throughout

## Files Created

### ViewModels
- `ObsidianScout/ViewModels/SettingsViewModel.cs`
  - Handles theme switching logic
  - Manages cache clearing operations
  - Updates cache status display
  - Provides status messages with auto-clear

### Views
- `ObsidianScout/Views/SettingsPage.xaml`
  - Settings UI with sections for Appearance, Cache, and About
  - Theme-aware styling with AppThemeBinding
  - Modern card-based layout
  - Responsive design

- `ObsidianScout/Views/SettingsPage.xaml.cs`
  - Code-behind with ViewModel binding

## Files Modified

### Services
- `ObsidianScout/Services/SettingsService.cs`
  - Added `GetThemeAsync()` method
  - Added `SetThemeAsync(string theme)` method
  - Theme preference stored in SecureStorage
  - Default theme: Light

### App Infrastructure
- `ObsidianScout/App.xaml.cs`
  - Added `InitializeThemeAsync()` method
  - Loads saved theme preference on app startup
  - Applies theme before UI is shown

- `ObsidianScout/AppShell.xaml`
  - Added Settings menu item
  - Positioned between Match Prediction and Logout
  - Only visible when user is logged in

- `ObsidianScout/MauiProgram.cs`
  - Registered `SettingsViewModel` in DI container
  - Registered `SettingsPage` in DI container

## Usage

### Accessing Settings
1. Login to the app
2. Open the hamburger menu (?)
3. Select "Settings"

### Changing Theme
1. Navigate to Settings page
2. Toggle the "Dark Mode" switch
3. Theme changes immediately
4. Preference is saved automatically

### Clearing Cache
1. Navigate to Settings page
2. View current cache status
3. Click "Clear Cache" button
4. Confirmation message appears
5. Cache is cleared and status updates

## Technical Details

### Theme Storage
- Theme preference stored in `SecureStorage` with key: `app_theme`
- Values: "Light" or "Dark"
- Default: "Light"
- Applied via `Application.Current.UserAppTheme`

### Cache Clearing
- Uses `ICacheService.ClearAllCacheAsync()`
- Clears all cache keys and timestamps
- Updates cache status after clearing
- Shows loading indicator during operation

### Status Messages
- Auto-clear after 2 seconds for success messages
- Persist for errors
- Visual feedback: ? for success, ? for errors

## UI Components

### Appearance Section
- **Dark Mode Switch**: Toggle theme
- **Status Label**: Shows current theme
- **Visual Indicators**: ?? (Light) / ?? (Dark)

### Cache Management Section
- **Cache Status Display**: Shows cache age
- **Refresh Button**: Update cache status
- **Clear Cache Button**: Remove all cached data
- **Loading Indicator**: Shows during operations
- **Help Text**: Explains what cache clearing does

### About Section
- **App Name**: ObsidianScout
- **Description**: FRC Scouting System
- **Version**: 1.0.0

## Benefits

1. **User Control**: Users can customize their experience
2. **Data Management**: Clear cache when needed
3. **Performance**: Theme applied before UI renders
4. **Persistence**: Settings survive app restarts
5. **Feedback**: Clear visual feedback for all actions
6. **Consistency**: Matches app's design language

## Testing Checklist

- [ ] Theme switches between Light and Dark
- [ ] Theme persists after app restart
- [ ] Cache status displays correctly
- [ ] Cache clears successfully
- [ ] Status messages appear and auto-clear
- [ ] Settings menu item appears when logged in
- [ ] Settings menu item hidden when logged out
- [ ] UI is responsive to theme changes
- [ ] All text is readable in both themes

## Future Enhancements

Potential additions:
- Language selection
- Notification preferences
- Data export/import
- App update checker
- Debug mode toggle
- Server configuration access
- Account management
- About page with licenses
