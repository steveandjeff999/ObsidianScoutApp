namespace ObsidianScout.Services;

public interface ISettingsService
{
    Task<string> GetServerUrlAsync();
    Task SetServerUrlAsync(string url);
    Task<string> GetProtocolAsync();
    Task SetProtocolAsync(string protocol);
    Task<string> GetServerAddressAsync();
    Task SetServerAddressAsync(string address);
    Task<string> GetServerPortAsync();
    Task SetServerPortAsync(string port);
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string? token);
    Task<DateTime?> GetTokenExpirationAsync();
    Task SetTokenExpirationAsync(DateTime? expiration);
    Task<string?> GetUsernameAsync();
    Task SetUsernameAsync(string? username);
    Task<List<string>> GetUserRolesAsync();
    Task SetUserRolesAsync(List<string> roles);
    Task ClearAuthDataAsync();
    Task<int?> GetTeamNumberAsync();
    Task SetTeamNumberAsync(int? teamNumber);
    Task<string> GetThemeAsync();
    Task SetThemeAsync(string theme);
    Task<string?> GetEmailAsync();
    Task SetEmailAsync(string? email);
    Task<bool> GetOfflineModeAsync();
    Task SetOfflineModeAsync(bool enabled);
    Task<bool> GetNotificationsEnabledAsync();
    Task SetNotificationsEnabledAsync(bool enabled);
    Task<int> GetNetworkTimeoutAsync();
    Task SetNetworkTimeoutAsync(int timeoutSeconds);

    // Event fired when offline mode is changed via the settings service
    event EventHandler<bool> OfflineModeChanged;
}

public class SettingsService : ISettingsService
{
    private const string ServerUrlKey = "server_url";
    private const string ProtocolKey = "server_protocol";
    private const string ServerAddressKey = "server_address";
    private const string ServerPortKey = "server_port";
    private const string TokenKey = "auth_token";
    private const string TokenExpirationKey = "token_expiration";
    private const string UsernameKey = "username";
    private const string UserRolesKey = "user_roles";
    private const string TeamNumberKey = "team_number";
    private const string ThemeKey = "app_theme";
    private const string EmailKey = "email";
    private const string OfflineModeKey = "offline_mode";
    private const string NotificationsEnabledKey = "notifications_enabled";
    private const string NetworkTimeoutKey = "network_timeout";

    private const string DefaultProtocol = "https";
    private const string DefaultServerAddress = "your-server.com";
    private const string DefaultServerPort = "";
    private const string DefaultTheme = "Light";
    private const int DefaultNetworkTimeout = 8; // 8 seconds default

    public event EventHandler<bool>? OfflineModeChanged;

    public async Task<string> GetProtocolAsync()
    {
        try
        {
            return await SecureStorage.GetAsync(ProtocolKey) ?? DefaultProtocol;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetProtocolAsync error: {ex.Message}");
            return DefaultProtocol;
        }
    }

    public async Task SetProtocolAsync(string protocol)
    {
        try
        {
            await SecureStorage.SetAsync(ProtocolKey, protocol ?? DefaultProtocol);
            await UpdateServerUrlAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetProtocolAsync error: {ex.Message}");
        }
    }

    public async Task<string> GetServerAddressAsync()
    {
        try
        {
            return await SecureStorage.GetAsync(ServerAddressKey) ?? DefaultServerAddress;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetServerAddressAsync error: {ex.Message}");
            return DefaultServerAddress;
        }
    }

    public async Task SetServerAddressAsync(string address)
    {
        try
        {
            await SecureStorage.SetAsync(ServerAddressKey, address ?? DefaultServerAddress);
            await UpdateServerUrlAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetServerAddressAsync error: {ex.Message}");
        }
    }

    public async Task<string> GetServerPortAsync()
    {
        try
        {
            return await SecureStorage.GetAsync(ServerPortKey) ?? DefaultServerPort;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetServerPortAsync error: {ex.Message}");
            return DefaultServerPort;
        }
    }

    public async Task SetServerPortAsync(string port)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(port))
            {
                SecureStorage.Remove(ServerPortKey);
            }
            else
            {
                await SecureStorage.SetAsync(ServerPortKey, port);
            }
            await UpdateServerUrlAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetServerPortAsync error: {ex.Message}");
        }
    }

    private async Task UpdateServerUrlAsync()
    {
        try
        {
            var protocol = await GetProtocolAsync();
            var address = await GetServerAddressAsync();
            var port = await GetServerPortAsync();
            
            string url;
            if (string.IsNullOrWhiteSpace(port) || (protocol == "https" && port == "443") || (protocol == "http" && port == "80"))
            {
                url = $"{protocol}://{address}";
            }
            else
            {
                url = $"{protocol}://{address}:{port}";
            }
            await SecureStorage.SetAsync(ServerUrlKey, url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] UpdateServerUrlAsync error: {ex.Message}");
        }
    }

    public async Task<string> GetServerUrlAsync()
    {
        try
        {
            var url = await SecureStorage.GetAsync(ServerUrlKey);
            if (string.IsNullOrEmpty(url))
            {
                await UpdateServerUrlAsync();
                url = await SecureStorage.GetAsync(ServerUrlKey);
            }
            return url ?? $"{DefaultProtocol}://{DefaultServerAddress}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetServerUrlAsync error: {ex.Message}");
            return $"{DefaultProtocol}://{DefaultServerAddress}";
      }
    }

    public async Task SetServerUrlAsync(string url)
    {
        try
 {
     await SecureStorage.SetAsync(ServerUrlKey, url ?? $"{DefaultProtocol}://{DefaultServerAddress}");
            
   try
            {
       var uri = new Uri(url);
  await SecureStorage.SetAsync(ProtocolKey, uri.Scheme);
   await SecureStorage.SetAsync(ServerAddressKey, uri.Host);
          if (uri.IsDefaultPort)
     {
        SecureStorage.Remove(ServerPortKey);
         }
             else
    {
 await SecureStorage.SetAsync(ServerPortKey, uri.Port.ToString());
      }
         }
       catch
    {
     // Invalid URI format - just store the raw URL
      }
   }
        catch (Exception ex)
        {
      System.Diagnostics.Debug.WriteLine($"[SettingsService] SetServerUrlAsync error: {ex.Message}");
        }
 }

    public async Task<string?> GetTokenAsync()
    {
        try
  {
  return await SecureStorage.GetAsync(TokenKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetTokenAsync error: {ex.Message}");
return null;
  }
    }

    public async Task SetTokenAsync(string? token)
    {
        try
      {
         if (string.IsNullOrEmpty(token))
     {
        SecureStorage.Remove(TokenKey);
            }
            else
  {
                await SecureStorage.SetAsync(TokenKey, token);
        }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetTokenAsync error: {ex.Message}");
        }
    }

    public async Task<DateTime?> GetTokenExpirationAsync()
    {
        try
   {
 var expirationStr = await SecureStorage.GetAsync(TokenExpirationKey);
   if (string.IsNullOrEmpty(expirationStr))
 return null;

if (DateTime.TryParse(expirationStr, out var expiration))
   return expiration;

  return null;
        }
        catch (Exception ex)
  {
   System.Diagnostics.Debug.WriteLine($"[SettingsService] GetTokenExpirationAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task SetTokenExpirationAsync(DateTime? expiration)
    {
     try
        {
        if (expiration == null)
         {
           SecureStorage.Remove(TokenExpirationKey);
      }
    else
    {
   await SecureStorage.SetAsync(TokenExpirationKey, expiration.Value.ToString("O"));
            }
        }
      catch (Exception ex)
  {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetTokenExpirationAsync error: {ex.Message}");
     }
}

    public async Task<string?> GetUsernameAsync()
    {
        try
        {
 return await SecureStorage.GetAsync(UsernameKey);
 }
   catch (Exception ex)
   {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetUsernameAsync error: {ex.Message}");
 return null;
     }
    }

    public async Task SetUsernameAsync(string? username)
    {
        try
        {
            if (string.IsNullOrEmpty(username))
   {
  SecureStorage.Remove(UsernameKey);
            }
   else
            {
      await SecureStorage.SetAsync(UsernameKey, username);
     }
        }
    catch (Exception ex)
      {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetUsernameAsync error: {ex.Message}");
        }
    }

 public async Task<List<string>> GetUserRolesAsync()
    {
        try
   {
            var rolesJson = await SecureStorage.GetAsync(UserRolesKey);
    if (string.IsNullOrEmpty(rolesJson))
  return new List<string>();

            try
  {
                var roles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rolesJson);
                return roles ?? new List<string>();
            }
   catch
  {
           return new List<string>();
         }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetUserRolesAsync error: {ex.Message}");
            return new List<string>();
  }
    }

    public async Task SetUserRolesAsync(List<string> roles)
    {
        try
        {
            if (roles == null || roles.Count == 0)
   {
     SecureStorage.Remove(UserRolesKey);
         }
        else
            {
   var rolesJson = System.Text.Json.JsonSerializer.Serialize(roles);
await SecureStorage.SetAsync(UserRolesKey, rolesJson);
         }
    }
        catch (Exception ex)
{
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetUserRolesAsync error: {ex.Message}");
        }
    }

  public async Task<int?> GetTeamNumberAsync()
    {
        try
 {
          var val = await SecureStorage.GetAsync(TeamNumberKey);
    if (string.IsNullOrEmpty(val)) return null;
            if (int.TryParse(val, out var n)) return n;
       return null;
        }
        catch (Exception ex)
      {
       System.Diagnostics.Debug.WriteLine($"[SettingsService] GetTeamNumberAsync error: {ex.Message}");
  return null;
        }
    }

    public async Task SetTeamNumberAsync(int? teamNumber)
    {
        try
     {
            if (teamNumber == null)
            {
    SecureStorage.Remove(TeamNumberKey);
      }
  else
          {
       await SecureStorage.SetAsync(TeamNumberKey, teamNumber.Value.ToString());
    }
        }
        catch (Exception ex)
      {
       System.Diagnostics.Debug.WriteLine($"[SettingsService] SetTeamNumberAsync error: {ex.Message}");
        }
    }

    public async Task ClearAuthDataAsync()
    {
        try
        {
            SecureStorage.Remove(TokenKey);
            SecureStorage.Remove(TokenExpirationKey);
    SecureStorage.Remove(UsernameKey);
  SecureStorage.Remove(UserRolesKey);
        SecureStorage.Remove(TeamNumberKey);
       SecureStorage.Remove(EmailKey);
        }
        catch (Exception ex)
        {
  System.Diagnostics.Debug.WriteLine($"[SettingsService] ClearAuthDataAsync error: {ex.Message}");
        }

    await Task.CompletedTask;
    }

    public async Task<string> GetThemeAsync()
    {
    try
        {
            return await SecureStorage.GetAsync(ThemeKey) ?? DefaultTheme;
      }
        catch (Exception ex)
 {
        System.Diagnostics.Debug.WriteLine($"[SettingsService] GetThemeAsync error: {ex.Message}");
 return DefaultTheme;
        }
    }

    public async Task SetThemeAsync(string theme)
    {
     try
        {
         await SecureStorage.SetAsync(ThemeKey, theme ?? DefaultTheme);
        }
        catch (Exception ex)
        {
    System.Diagnostics.Debug.WriteLine($"[SettingsService] SetThemeAsync error: {ex.Message}");
  }
    }

    public async Task<string?> GetEmailAsync()
    {
      try
   {
          return await SecureStorage.GetAsync(EmailKey);
      }
        catch (Exception ex)
     {
       System.Diagnostics.Debug.WriteLine($"[SettingsService] GetEmailAsync error: {ex.Message}");
   return null;
        }
    }

    public async Task SetEmailAsync(string? email)
    {
        try
{
   if (string.IsNullOrEmpty(email))
         {
SecureStorage.Remove(EmailKey);
            }
            else
      {
             await SecureStorage.SetAsync(EmailKey, email);
            }
        }
        catch (Exception ex)
   {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetEmailAsync error: {ex.Message}");
        }
    }

    public async Task<bool> GetOfflineModeAsync()
    {
        try
        {
 var val = await SecureStorage.GetAsync(OfflineModeKey);
      if (string.IsNullOrEmpty(val)) return false;
            return bool.TryParse(val, out var b) && b;
        }
      catch (Exception ex)
        {
       System.Diagnostics.Debug.WriteLine($"[SettingsService] GetOfflineModeAsync error: {ex.Message}");
      return false;
        }
    }

    public async Task SetOfflineModeAsync(bool enabled)
    {
        try
 {
       await SecureStorage.SetAsync(OfflineModeKey, enabled ? "true" : "false");

    // Notify subscribers that offline mode changed
            try
            {
         OfflineModeChanged?.Invoke(this, enabled);
  }
       catch (Exception ex)
   {
         System.Diagnostics.Debug.WriteLine($"[SettingsService] OfflineModeChanged handler threw: {ex.Message}");
}
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetOfflineModeAsync error: {ex.Message}");
        }
}

    public async Task<bool> GetNotificationsEnabledAsync()
    {
        try
        {
          var v = await SecureStorage.GetAsync(NotificationsEnabledKey);
            if (string.IsNullOrEmpty(v)) return true; // default enabled
      return v == "1";
        }
        catch (Exception ex)
      {
  System.Diagnostics.Debug.WriteLine($"[SettingsService] GetNotificationsEnabledAsync error: {ex.Message}");
            return true;
 }
    }

  public async Task SetNotificationsEnabledAsync(bool enabled)
    {
        try
        {
         await SecureStorage.SetAsync(NotificationsEnabledKey, enabled ? "1" : "0");
    }
  catch (Exception ex)
  {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetNotificationsEnabledAsync error: {ex.Message}");
    }
    }

    public async Task<int> GetNetworkTimeoutAsync()
    {
   try
        {
            var val = await SecureStorage.GetAsync(NetworkTimeoutKey);
     if (string.IsNullOrEmpty(val)) return DefaultNetworkTimeout;
 if (int.TryParse(val, out var timeout))
            {
                // Clamp between 5 and 60 seconds for safety
   return Math.Clamp(timeout, 5, 60);
 }
 return DefaultNetworkTimeout;
        }
     catch (Exception ex)
 {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetNetworkTimeoutAsync error: {ex.Message}");
            return DefaultNetworkTimeout;
        }
    }

    public async Task SetNetworkTimeoutAsync(int timeoutSeconds)
    {
    try
        {
            // Clamp between 5 and 60 seconds for safety
       var clamped = Math.Clamp(timeoutSeconds, 5, 60);
  await SecureStorage.SetAsync(NetworkTimeoutKey, clamped.ToString());
        }
        catch (Exception ex)
        {
 System.Diagnostics.Debug.WriteLine($"[SettingsService] SetNetworkTimeoutAsync error: {ex.Message}");
        }
    }
}
