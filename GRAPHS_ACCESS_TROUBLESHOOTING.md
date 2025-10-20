# Graphs Page Access Troubleshooting Guide

## Issue: Admin Account Can't See Graphs Page

### Possible Causes

1. **Role name case mismatch** - The API might return "admin" but the code checks for "analytics_admin"
2. **Roles not being stored** - Roles might not be saved during login
3. **Roles cleared on app restart** - Secure storage might not persist
4. **Shell not updating** - The HasAnalyticsAccess property might not update after login

---

## Quick Diagnostic Steps

### Step 1: Check What Roles Are Stored

Add this temporary diagnostic code to check stored roles:

**In `ObsidianScout\Views\LoginPage.xaml.cs`** (or create a test button):

```csharp
private async void OnCheckRolesClicked(object sender, EventArgs e)
{
    var settingsService = Handler.MauiContext.Services.GetService<ISettingsService>();
    var roles = await settingsService.GetUserRolesAsync();
    
    var rolesText = roles.Count > 0 
        ? string.Join(", ", roles) 
        : "No roles found";
    
    await DisplayAlert("Stored Roles", $"Your roles: {rolesText}", "OK");
}
```

### Step 2: Check Debug Output

When you login, check the Output window for these debug messages:

```
Auth Status - IsLoggedIn: True, HasAnalyticsAccess: True/False
? Auth state updated: IsLoggedIn = True, HasAnalyticsAccess = True/False
```

### Step 3: Check API Response

The API login response should include roles like this:

```json
{
  "success": true,
  "user": {
    "roles": ["admin", "analytics_admin"]
  }
}
```

---

## Quick Fix: Case-Insensitive Role Checking

The most common issue is case sensitivity. Update the role checking to be case-insensitive:

### Fix 1: Update AppShell.xaml.cs

**File:** `ObsidianScout\AppShell.xaml.cs`

**Find this code (around line 68-71):**
```csharp
var roles = await _settingsService.GetUserRolesAsync();
HasAnalyticsAccess = roles.Contains("analytics") || 
                    roles.Contains("analytics_admin") || 
                    roles.Contains("superadmin");
```

**Replace with case-insensitive comparison:**
```csharp
var roles = await _settingsService.GetUserRolesAsync();
HasAnalyticsAccess = roles.Any(r => 
    r.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
    r.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) ||
    r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
    r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));
```

**Also update the same code in `UpdateAuthenticationState` method (around line 114-117):**
```csharp
var roles = await _settingsService.GetUserRolesAsync();
HasAnalyticsAccess = roles.Any(r => 
    r.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
    r.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) ||
    r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
    r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));
```

---

## Fix 2: Add Role Debug Logging

Add diagnostic logging to see exactly what's happening:

**File:** `ObsidianScout\Services\ApiService.cs`

**In the `LoginAsync` method, after storing roles:**

```csharp
// Store user roles
if (result.User != null && result.User.Roles != null && result.User.Roles.Count > 0)
{
    await _settingsService.SetUserRolesAsync(result.User.Roles);
    
    // DEBUG: Log roles
    System.Diagnostics.Debug.WriteLine($"LOGIN: Stored {result.User.Roles.Count} roles:");
    foreach (var role in result.User.Roles)
    {
        System.Diagnostics.Debug.WriteLine($"  - {role}");
    }
}
else
{
    System.Diagnostics.Debug.WriteLine("LOGIN: No roles returned from API");
}
```

---

## Fix 3: Force Shell Update After Login

Sometimes the Shell UI doesn't update immediately. Force a manual update:

**File:** `ObsidianScout\ViewModels\LoginViewModel.cs`

**Update the LoginAsync method:**

```csharp
if (result.Success)
{
    // Update AppShell authentication state
    if (Shell.Current is AppShell appShell)
    {
        appShell.UpdateAuthenticationState(true);
        
        // Force UI refresh
        await Task.Delay(100);
        appShell.UpdateAuthenticationState(true);
    }
    
    // Navigate to main page
    await Shell.Current.GoToAsync("//MainPage");
}
```

---

## Fix 4: Add Manual Refresh Button (Temporary)

Add a test button to manually refresh the menu:

**File:** `ObsidianScout\MainPage.xaml`

Add this button temporarily:

```xaml
<Button Text="?? Refresh Menu Access"
        Clicked="OnRefreshAccessClicked"
        Margin="10" />
```

**File:** `ObsidianScout\MainPage.xaml.cs`

Add this handler:

```csharp
private async void OnRefreshAccessClicked(object sender, EventArgs e)
{
    if (Shell.Current is AppShell appShell)
    {
        appShell.UpdateAuthenticationState(true);
        await DisplayAlert("Refreshed", "Menu access updated. Check the menu now.", "OK");
    }
}
```

---

## Testing Steps

### Test 1: Check Stored Roles After Login

1. Login with your admin account
2. Add a test button or breakpoint
3. Check what roles are stored using:
   ```csharp
   var roles = await _settingsService.GetUserRolesAsync();
   ```

### Test 2: Verify API Response

Check your API's login endpoint response. It should return:

```json
{
  "user": {
    "roles": ["admin"]
  }
}
```

Or similar. The exact role name matters!

### Test 3: Check Case Sensitivity

If your API returns `"Admin"` (capital A) but the code checks for `"admin"` (lowercase), it won't match.

---

## Common Role Names by API Implementation

Different servers use different role names:

| Server | Likely Role Names |
|--------|-------------------|
| Python/Django | `admin`, `superadmin`, `analytics_admin` |
| Node.js | `Admin`, `SuperAdmin`, `AnalyticsAdmin` |
| .NET | `Admin`, `Superadmin`, `AnalyticsAdmin` |

---

## Complete Fix Implementation

Here's the complete updated `CheckAuthStatus` method with all fixes:

```csharp
private async void CheckAuthStatus()
{
    try
    {
        var token = await _settingsService.GetTokenAsync();
        var expiration = await _settingsService.GetTokenExpirationAsync();
        
        IsLoggedIn = !string.IsNullOrEmpty(token) && 
                    expiration.HasValue && 
                    expiration.Value > DateTime.UtcNow;
        
        // Check user roles for analytics access
        if (IsLoggedIn)
        {
            var roles = await _settingsService.GetUserRolesAsync();
            
            // DEBUG: Log roles
            System.Diagnostics.Debug.WriteLine($"DEBUG: Found {roles.Count} roles:");
            foreach (var role in roles)
            {
                System.Diagnostics.Debug.WriteLine($"  - '{role}'");
            }
            
            // Case-insensitive check for multiple role variations
            HasAnalyticsAccess = roles.Any(r => 
                r.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("analytics_admin", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));
            
            System.Diagnostics.Debug.WriteLine($"DEBUG: HasAnalyticsAccess = {HasAnalyticsAccess}");
        }
        else
        {
            HasAnalyticsAccess = false;
        }
        
        System.Diagnostics.Debug.WriteLine($"Auth Status - IsLoggedIn: {IsLoggedIn}, HasAnalyticsAccess: {HasAnalyticsAccess}");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error checking auth status: {ex.Message}");
        IsLoggedIn = false;
        HasAnalyticsAccess = false;
    }
}
```

---

## Alternative: Simplified Role Check

If you want to give **all admin users** access to graphs, simplify the check:

```csharp
// Give analytics access to any role containing "admin"
HasAnalyticsAccess = roles.Any(r => 
    r.Contains("admin", StringComparison.OrdinalIgnoreCase));
```

---

## What to Check in Output Window

After applying fixes, login and look for these messages:

```
LOGIN: Stored 1 roles:
  - admin
DEBUG: Found 1 roles:
  - 'admin'
DEBUG: HasAnalyticsAccess = True
Auth Status - IsLoggedIn: True, HasAnalyticsAccess: True
? Auth state updated: IsLoggedIn = True, HasAnalyticsAccess = True
```

If you see `HasAnalyticsAccess = False`, that's your problem!

---

## Still Not Working?

### Nuclear Option: Grant Access to All Logged-In Users (Temporary)

For testing only, temporarily give all logged-in users access:

```csharp
// TEMPORARY - REMOVE IN PRODUCTION
HasAnalyticsAccess = IsLoggedIn; // Give everyone access
```

This will help you determine if it's a role issue or something else.

---

## Summary

**Most Likely Fix:** Case-insensitive role comparison + adding "admin" to the accepted roles list.

**Apply these two changes:**

1. Change role checking to case-insensitive
2. Add "admin" to the list of roles that get analytics access

**Then:**
1. Rebuild and redeploy
2. Login again
3. Check the Output window for debug messages
4. The Graphs menu should now appear

---

## Next Steps After Fix

Once you can see the Graphs page:

1. Remove debug logging
2. Test with different role types
3. Update documentation with correct role names
4. Consider adding a "My Account" page showing current roles
