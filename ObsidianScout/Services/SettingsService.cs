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
    Task<string> GetThemeAsync();
    Task SetThemeAsync(string theme);
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
    private const string ThemeKey = "app_theme";
    
    private const string DefaultProtocol = "https";
    private const string DefaultServerAddress = "your-server.com";
    // Default port is optional now - empty means use standard port for scheme
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
        // Return empty string if not set
        return await SecureStorage.GetAsync(ServerPortKey) ?? DefaultServerPort;
    }

    public async Task SetServerPortAsync(string port)
    {
        // Allow empty port to indicate default for protocol
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
        // If port is empty or matches standard for protocol, omit it
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
            // Build URL from components if not set
            await UpdateServerUrlAsync();
            url = await SecureStorage.GetAsync(ServerUrlKey);
        }
        return url ?? $"{DefaultProtocol}://{DefaultServerAddress}";
    }

    public async Task SetServerUrlAsync(string url)
    {
        await SecureStorage.SetAsync(ServerUrlKey, url);
        
        // Try to parse the URL and update components
        try
        {
            var uri = new Uri(url);
            await SecureStorage.SetAsync(ProtocolKey, uri.Scheme);
            await SecureStorage.SetAsync(ServerAddressKey, uri.Host);
            // If port is default for the scheme, store empty to indicate no explicit port
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
            // If parsing fails, just store the URL as-is
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

    public async Task ClearAuthDataAsync()
    {
        SecureStorage.Remove(TokenKey);
        SecureStorage.Remove(TokenExpirationKey);
        SecureStorage.Remove(UsernameKey);
        SecureStorage.Remove(UserRolesKey);
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
}
