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

    private const string DefaultProtocol = "https";
    private const string DefaultServerAddress = "your-server.com";
    private const string DefaultServerPort = "";
    private const string DefaultTheme = "Light";

    public async Task<string> GetProtocolAsync()
    {
        return await SecureStorage.GetAsync(ProtocolKey) ?? DefaultProtocol;
    }

    public async Task SetProtocolAsync(string protocol)
    {
        await SecureStorage.SetAsync(ProtocolKey, protocol);
        await UpdateServerUrlAsync();
    }

    public async Task<string> GetServerAddressAsync()
    {
        return await SecureStorage.GetAsync(ServerAddressKey) ?? DefaultServerAddress;
    }

    public async Task SetServerAddressAsync(string address)
    {
        await SecureStorage.SetAsync(ServerAddressKey, address);
        await UpdateServerUrlAsync();
    }

    public async Task<string> GetServerPortAsync()
    {
        return await SecureStorage.GetAsync(ServerPortKey) ?? DefaultServerPort;
    }

    public async Task SetServerPortAsync(string port)
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

    private async Task UpdateServerUrlAsync()
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

    public async Task<string> GetServerUrlAsync()
    {
        var url = await SecureStorage.GetAsync(ServerUrlKey);
        if (string.IsNullOrEmpty(url))
        {
            await UpdateServerUrlAsync();
            url = await SecureStorage.GetAsync(ServerUrlKey);
        }
        return url ?? $"{DefaultProtocol}://{DefaultServerAddress}";
    }

    public async Task SetServerUrlAsync(string url)
    {
        await SecureStorage.SetAsync(ServerUrlKey, url);
        
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
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await SecureStorage.GetAsync(TokenKey);
    }

    public async Task SetTokenAsync(string? token)
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

    public async Task<DateTime?> GetTokenExpirationAsync()
    {
        var expirationStr = await SecureStorage.GetAsync(TokenExpirationKey);
        if (string.IsNullOrEmpty(expirationStr))
            return null;

        if (DateTime.TryParse(expirationStr, out var expiration))
            return expiration;

        return null;
    }

    public async Task SetTokenExpirationAsync(DateTime? expiration)
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

    public async Task<string?> GetUsernameAsync()
    {
        return await SecureStorage.GetAsync(UsernameKey);
    }

    public async Task SetUsernameAsync(string? username)
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

    public async Task<List<string>> GetUserRolesAsync()
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

    public async Task SetUserRolesAsync(List<string> roles)
    {
        if (roles == null || roles.Count ==0)
        {
            SecureStorage.Remove(UserRolesKey);
        }
        else
        {
            var rolesJson = System.Text.Json.JsonSerializer.Serialize(roles);
            await SecureStorage.SetAsync(UserRolesKey, rolesJson);
        }
    }

    public async Task<int?> GetTeamNumberAsync()
    {
        var val = await SecureStorage.GetAsync(TeamNumberKey);
        if (string.IsNullOrEmpty(val)) return null;
        if (int.TryParse(val, out var n)) return n;
        return null;
    }

    public async Task SetTeamNumberAsync(int? teamNumber)
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
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to clear secure storage: {ex}");
        }

        await Task.CompletedTask;
    }

    public async Task<string> GetThemeAsync()
    {
        return await SecureStorage.GetAsync(ThemeKey) ?? DefaultTheme;
    }

    public async Task SetThemeAsync(string theme)
    {
        await SecureStorage.SetAsync(ThemeKey, theme);
    }

    public async Task<string?> GetEmailAsync()
    {
        return await SecureStorage.GetAsync(EmailKey);
    }

    public async Task SetEmailAsync(string? email)
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

    public async Task<bool> GetOfflineModeAsync()
    {
        try
        {
            var val = await SecureStorage.GetAsync(OfflineModeKey);
            if (string.IsNullOrEmpty(val)) return false;
            return bool.TryParse(val, out var b) && b;
        }
        catch
        {
            return false;
        }
    }

    public async Task SetOfflineModeAsync(bool enabled)
    {
        try
        {
            await SecureStorage.SetAsync(OfflineModeKey, enabled ? "true" : "false");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to set offline mode: {ex}");
        }
    }
}
