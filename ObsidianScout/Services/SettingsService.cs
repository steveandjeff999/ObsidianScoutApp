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
    
    private const string DefaultProtocol = "https";
    private const string DefaultServerAddress = "your-server.com";
    private const string DefaultServerPort = "443";

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
        await SecureStorage.SetAsync(ServerPortKey, port);
        await UpdateServerUrlAsync();
    }

    private async Task UpdateServerUrlAsync()
    {
        var protocol = await GetProtocolAsync();
        var address = await GetServerAddressAsync();
        var port = await GetServerPortAsync();
        
        var url = $"{protocol}://{address}:{port}";
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
        return url ?? $"{DefaultProtocol}://{DefaultServerAddress}:{DefaultServerPort}";
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
            await SecureStorage.SetAsync(ServerPortKey, uri.Port.ToString());
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
}
