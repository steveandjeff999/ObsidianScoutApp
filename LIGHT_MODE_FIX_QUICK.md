# Light Mode Fix - Quick Summary

## Problem
White text on white backgrounds in Light mode makes text unreadable.

## Solution
The SettingsPage.xaml has backwards color bindings.

## Quick Fix (30 seconds)

1. Open `ObsidianScout/Views/SettingsPage.xaml`
2. Press `Ctrl+H` (Find & Replace)
3. **Find:** `Light={StaticResource DarkText}, Dark={StaticResource LightText}`
4. **Replace:** `Light={StaticResource LightText}, Dark={StaticResource DarkText}`
5. Click **Replace All**
6. Save (`Ctrl+S`)
7. Rebuild (`Ctrl+Shift+B`)

## What Changed
- **Colors.xaml** - Added `LightText` and `DarkText` aliases ? (Already done)
- **SettingsPage.xaml** - Needs color bindings corrected ?? (Needs manual fix)

## The Pattern

**Remember:**
- **Light** mode ? Use **Light** prefix colors
- **Dark** mode ? Use **Dark** prefix colors

```xml
<!-- CORRECT -->
TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}"

<!-- WRONG (causes white-on-white) -->
TextColor="{AppThemeBinding Light={StaticResource DarkText}, Dark={StaticResource LightText}}"
```

## Test
1. Set Light mode ? Text should be dark
2. Set Dark mode ? Text should be light

## Files Modified

### ? Already Fixed
- `ObsidianScout/Resources/Styles/Colors.xaml`

### ?? Needs Manual Fix
- `ObsidianScout/Views/SettingsPage.xaml`

## Why Manual Fix Needed
The XAML file has formatting issues that prevented automated editing. The file exists but needs the Find & Replace operation performed manually in Visual Studio.

## Alternative: Delete & Recreate
If Find & Replace doesn't work:
1. Delete `ObsidianScout/Views/SettingsPage.xaml`
2. Copy content from `LIGHT_MODE_TEXT_FIX.md`  
3. Create new file with corrected content

## Result
? Dark text on light backgrounds (Light mode)
? Light text on dark backgrounds (Dark mode)
? Everything readable
