# Quick Guide: Authentication & Menu Access Control

## What Was Done

? **Menu items now show/hide based on login status**
? **Logged-out users cannot access protected pages**
? **Navigation blocked with user-friendly alerts**

---

## How It Works

### Logged Out:
```
Menu:
?? ?? Login (only visible)
```

**Protected pages:** ? Hidden and blocked

### Logged In:
```
Menu:
?? ?? Home
?? ?? Scouting
?? ?? Teams
?? ?? Events
?? ?? Logout
```

**All pages:** ? Visible and accessible

---

## What Happens

### If User Tries to Access Protected Page While Logged Out:

1. Navigation is **blocked** ?
2. Alert shown: "Authentication Required"
3. Redirected to **Login page**
4. Must log in to continue

---

## Files Changed

1. **AppShell.xaml** - Added menu visibility bindings
2. **AppShell.xaml.cs** - Added auth state management
3. **App.xaml.cs** - Added auth check on startup
4. **LoginViewModel.cs** - Update auth state on login
5. **MainViewModel.cs** - Update auth state on logout

---

## Key Features

### ?? Security:
- Token validation with expiration
- Navigation guards on all protected pages
- Menu items completely hidden (not just disabled)

### ??? User Experience:
- Clear menu - only show what's accessible
- Helpful alerts when navigation blocked
- Instant menu updates on login/logout

### ?? Developer Experience:
- Centralized auth logic in AppShell
- Comprehensive debug logging
- Easy to add new protected pages

---

## Adding New Protected Pages

To protect a new page:

1. **Add to AppShell.xaml:**
```xaml
<FlyoutItem Title="NewPage"
            IsVisible="{Binding IsLoggedIn}"
            Route="NewPage">
    <ShellContent ContentTemplate="{DataTemplate views:NewPage}" />
</FlyoutItem>
```

2. **Add to navigation guard in AppShell.xaml.cs:**
```csharp
if (!IsLoggedIn && (
    // ...existing checks...
    || target.Contains("NewPage", StringComparison.OrdinalIgnoreCase)))
{
    // Block navigation
}
```

Done! ?

---

## Debug Output

### Successful Navigation:
```
Navigating to: MainPage, IsLoggedIn: true
? Navigation allowed
```

### Blocked Navigation:
```
Navigating to: TeamsPage, IsLoggedIn: false
? Navigation blocked - User not logged in
? Redirecting to LoginPage
```

---

## Testing

### Quick Test:
1. Open app without logging in
2. Check menu - only "Login" visible ?
3. Try to navigate to any page ? Blocked ?
4. Log in successfully
5. Check menu - all pages visible ?
6. Can navigate to all pages ?
7. Log out
8. Menu hides protected pages again ?

---

## Summary

**Before**: Logged-out users could access all pages through menu
**After**: Only logged-in users can access protected pages

?? **Secure** | ??? **User-Friendly** | ?? **Maintainable**

Your app is now properly secured! ??
