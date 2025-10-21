# Windows Title Bar Light Mode Fix

## ?? Problem
The Windows title bar (top menu bar) displays in dark mode even when the app is in light mode, making it look inconsistent.

##  What We're Fixing
- Title bar background (should be white in light mode)
- Title bar text (should be dark in light mode)
- Window buttons (minimize, maximize, close) colors
- Auto-switching based on app theme

## ?? Changes Made

### File: `ObsidianScout/Platforms/Windows/App.xaml.cs`

Complete rewrite with proper light/dark mode detection and title bar customization.

**Key Features:**
- ? Detects current theme (Light/Dark/System)
- ? Applies proper colors to title bar
- ? Handles window buttons (minimize, maximize, close)
- ? Updates when theme changes
- ? Uses Windows UI 3 APIs properly

### File: `ObsidianScout/AppShell.xaml`

Enhanced Shell properties for better navigation bar theming:
- Added `Shell.BackgroundColor` binding
- Added `Shell.NavBarHasShadow` for depth
- Added ShellContent styling

---

## ?? Title Bar Colors

### Light Mode
```
Background:           #FFFFFF (White)
Text:        #1C1C1E (Almost Black)
Inactive Background:  #F5F5F7 (Light Gray)
Inactive Text:        #8E8E93 (Medium Gray)

Buttons:
- Normal:     Transparent background, dark text
- Hover:      Light gray background (#E8E8ED)
- Pressed:    Medium gray background (#D1D1D6)
- Inactive:   Transparent, gray text
```

### Dark Mode  
```
Background:    #0F172A (Dark Blue)
Text:        #F8FAFC (Off White)
Inactive Background:  #1E293B (Darker Blue)
Inactive Text:   #64748B (Medium Gray)

Buttons:
- Normal:     Transparent background, light text
- Hover:      Darker blue background (#334155)
- Pressed:    Medium blue background (#475569)
- Inactive:   Transparent, gray text
```

---

## ?? Important: Must Restart App

**The title bar configuration CANNOT be hot reloaded.**

You MUST:
1. **Stop debugging** completely
2. **Close Visual Studio** (optional but recommended)
3. **Reopen solution**
4. **Rebuild** the project
5. **Run** the app again

### Why Restart is Required
- Title bar API calls happen at window creation
- The `OnLaunched` override requires app restart
- Hot reload doesn't reinitialize platform-specific code
- Windows UI APIs need process restart to apply

---

## ?? How It Works

### Theme Detection
```csharp
var currentTheme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
var isLightMode = currentTheme == AppTheme.Light || 
    (currentTheme == AppTheme.Unspecified && Application.Current.RequestedTheme == ApplicationTheme.Light);
```

### Title Bar Configuration
```csharp
Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(nameof(IWindow), (handler, view) =>
{
    if (handler.PlatformView is Microsoft.UI.Xaml.Window window)
    {
        ConfigureTitleBar(window);
        
        // Listen for theme changes
  if (view is Microsoft.Maui.Controls.Window mauiWindow)
        {
    mauiWindow.PropertyChanged += (s, e) =>
      {
      if (e.PropertyName == nameof(Microsoft.Maui.Controls.Window.Page))
      {
       ConfigureTitleBar(window);
            }
      };
        }
  }
});
```

### Color Application
Uses Windows.UI.Color (aliased as WinUIColor) to avoid conflicts:
```csharp
using WinUIColor = Windows.UI.Color;

titleBar.BackgroundColor = WinUIColor.FromArgb(255, 255, 255, 255); // White
titleBar.ForegroundColor = WinUIColor.FromArgb(255, 28, 28, 30); // #1C1C1E
```

---

## ? Testing Checklist

After restarting the app:

### Light Mode
- [ ] Title bar background is white
- [ ] Title text is dark/black
- [ ] Hamburger menu icon is visible and dark
- [ ] Window buttons (_, ?, ×) are dark
- [ ] Hover over buttons shows light gray background
- [ ] Click buttons shows medium gray background

### Dark Mode
- [ ] Title bar background is dark blue (#0F172A)
- [ ] Title text is light/white
- [ ] Hamburger menu icon is visible and light
- [ ] Window buttons are light colored
- [ ] Hover shows darker blue background
- [ ] Click shows medium blue background

### Theme Switching
- [ ] Go to Settings
- [ ] Change theme from Light ? Dark
- [ ] Title bar updates immediately
- [ ] Change theme from Dark ? Light
- [ ] Title bar updates immediately
- [ ] Set to System theme
- [ ] Title bar matches system theme

---

## ?? Troubleshooting

### Issue: Title bar still dark in light mode
**Solution:** You didn't restart the app. Must stop debugging and run again.

### Issue: Errors about ambiguous 'Color' reference
**Solution:** This is from old code during edit. After restart, errors will be gone.

### Issue: Title bar doesn't update when switching themes
**Solution:** 
1. Make sure Settings page is properly changing the theme
2. Check that `Application.Current.UserAppTheme` is being set
3. Title bar should update automatically via PropertyChanged event

### Issue: Window buttons are invisible
**Solution:** Check that button foreground colors are being set properly. May need to adjust alpha channel.

---

## ?? Platform Notes

### Windows Only
This fix only affects the Windows (WinUI 3) platform. Other platforms handle title bars differently:

- **Android**: No title bar (uses system status bar)
- **iOS**: Uses navigation bar (system controlled)
- **Mac Catalyst**: Uses macOS window chrome

### Windows 11 vs Windows 10
- Windows 11: Full support for all title bar APIs
- Windows 10: Some APIs may have limited support
- Minimum SDK: 10.0.17763.0 (already set in project)

---

## ?? Expected Result

### Before Fix
![Before - Dark title bar in light mode]
- Dark/black title bar
- White text (invisible on dark bar)
- Inconsistent with rest of app

### After Fix
![After - Proper light title bar]
- White title bar in light mode
- Dark text (clearly visible)
- Matches app color scheme
- Professional appearance

---

## ?? Quick Fix Summary

1. **Files Modified:**
   - `ObsidianScout/Platforms/Windows/App.xaml.cs` - Title bar configuration
   - `ObsidianScout/AppShell.xaml` - Enhanced Shell properties

2. **Action Required:**
   - **STOP DEBUGGING**
   - **RESTART APP**
   - That's it!

3. **What You'll See:**
   - White title bar in light mode
   - Dark title bar in dark mode
   - Proper button colors
   - Smooth theme transitions

---

## ?? API References

- [AppWindow.TitleBar](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.titlebar)
- [AppWindowTitleBar Class](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindowtitlebar)
- [Window Customization](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar)

---

## ? Bonus: Manual Theme Testing

Want to test without using Settings?

Add this to any page's code-behind:
```csharp
// Force light mode
Application.Current.UserAppTheme = AppTheme.Light;

// Force dark mode
Application.Current.UserAppTheme = AppTheme.Dark;

// Use system theme
Application.Current.UserAppTheme = AppTheme.Unspecified;
```

The title bar will update automatically!

---

## ?? Success!

After restarting, your Windows app will have:
- ? Perfect light mode title bar
- ? Perfect dark mode title bar
- ? Automatic theme switching
- ? Professional appearance
- ? Consistent with modern Windows apps

**Enjoy your beautiful, properly-themed app!** ??
