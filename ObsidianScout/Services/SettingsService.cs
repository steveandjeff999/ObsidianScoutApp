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

    // Update preferences
    Task<bool> GetAutoUpdateCheckAsync();
    Task SetAutoUpdateCheckAsync(bool enabled);

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
    private const string AutoUpdateCheckKey = "auto_update_check";

    private const string DefaultProtocol = "https";
    private static readonly string DefaultServerAddress = "beta.obsidianscout.com";
    private const string DefaultServerPort = "";
    private const string DefaultTheme = "Light";
    private const int DefaultNetworkTimeout = 8; // 8 seconds default

    // Thread-safety lock for cache operations
    private readonly object _cacheLock = new object();

    // Cache fields
    private string? _serverUrl;
    private string? _protocol;
    private string? _serverAddress;
    private string? _serverPort;
    
    private string? _token;
    private bool _tokenLoaded;
    
    private DateTime? _tokenExpiration;
    private bool _tokenExpirationLoaded;
    
    private string? _username;
    private bool _usernameLoaded;
    
    private List<string>? _userRoles;
    
    private int? _teamNumber;
    private bool _teamNumberLoaded;
    
    private string? _theme;
    
    private string? _email;
    private bool _emailLoaded;
    
    private bool? _offlineMode;
    private bool? _notificationsEnabled;
    private int? _networkTimeout;
    private bool? _autoUpdateCheck;

    public event EventHandler<bool>? OfflineModeChanged;

    public async Task<string> GetProtocolAsync()
    {
        if (_protocol != null) return _protocol;
        try
        {
            _protocol = await SecureStorage.GetAsync(ProtocolKey) ?? DefaultProtocol;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetProtocolAsync error: {ex.Message}");
            _protocol = DefaultProtocol;
        }
        return _protocol;
    }

    public async Task SetProtocolAsync(string protocol)
    {
        _protocol = protocol ?? DefaultProtocol;
        try
        {
            await SecureStorage.SetAsync(ProtocolKey, _protocol);
            await UpdateServerUrlAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetProtocolAsync error: {ex.Message}");
        }
    }

    public async Task<string> GetServerAddressAsync()
    {
        if (_serverAddress != null) return _serverAddress;
        try
        {
            _serverAddress = await SecureStorage.GetAsync(ServerAddressKey) ?? DefaultServerAddress;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetServerAddressAsync error: {ex.Message}");
            _serverAddress = DefaultServerAddress;
        }
        return _serverAddress;
    }

    public async Task SetServerAddressAsync(string address)
    {
        _serverAddress = address ?? DefaultServerAddress;
        try
        {
            await SecureStorage.SetAsync(ServerAddressKey, _serverAddress);
            await UpdateServerUrlAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetServerAddressAsync error: {ex.Message}");
        }
    }

    public async Task<string> GetServerPortAsync()
    {
        if (_serverPort != null) return _serverPort;
        try
        {
            _serverPort = await SecureStorage.GetAsync(ServerPortKey) ?? DefaultServerPort;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetServerPortAsync error: {ex.Message}");
            _serverPort = DefaultServerPort;
        }
        return _serverPort;
    }

    public async Task SetServerPortAsync(string port)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(port))
            {
                _serverPort = DefaultServerPort;
                SecureStorage.Remove(ServerPortKey);
            }
            else
            {
                _serverPort = port;
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
            
            _serverUrl = url;
            await SecureStorage.SetAsync(ServerUrlKey, url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] UpdateServerUrlAsync error: {ex.Message}");
        }
    }

    public async Task<string> GetServerUrlAsync()
    {
        if (_serverUrl != null) return _serverUrl;
        try
        {
            var url = await SecureStorage.GetAsync(ServerUrlKey);
            if (string.IsNullOrEmpty(url))
            {
                await UpdateServerUrlAsync();
                // UpdateServerUrlAsync sets _serverUrl
                if (_serverUrl != null) return _serverUrl;
                
                url = await SecureStorage.GetAsync(ServerUrlKey);
            }
            _serverUrl = url ?? $"{DefaultProtocol}://{DefaultServerAddress}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetServerUrlAsync error: {ex.Message}");
            _serverUrl = $"{DefaultProtocol}://{DefaultServerAddress}";
        }
        return _serverUrl;
    }

    public async Task SetServerUrlAsync(string url)
    {
        try
        {
            var newUrl = url ?? $"{DefaultProtocol}://{DefaultServerAddress}";
            _serverUrl = newUrl;
            await SecureStorage.SetAsync(ServerUrlKey, newUrl);
            
            try
            {
                var uri = new Uri(newUrl);
                
                _protocol = uri.Scheme;
                await SecureStorage.SetAsync(ProtocolKey, uri.Scheme);
                
                _serverAddress = uri.Host;
                await SecureStorage.SetAsync(ServerAddressKey, uri.Host);
                
                if (uri.IsDefaultPort)
                {
                    _serverPort = DefaultServerPort;
                    SecureStorage.Remove(ServerPortKey);
                }
                else
                {
                    _serverPort = uri.Port.ToString();
                    await SecureStorage.SetAsync(ServerPortKey, _serverPort);
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
        if (_tokenLoaded) return _token;
        try
        {
            _token = await SecureStorage.GetAsync(TokenKey);
            _tokenLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetTokenAsync error: {ex.Message}");
            _token = null;
        }
        return _token;
    }

    public async Task SetTokenAsync(string? token)
    {
        _token = token;
        _tokenLoaded = true;
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
        if (_tokenExpirationLoaded) return _tokenExpiration;
        try
        {
            var expirationStr = await SecureStorage.GetAsync(TokenExpirationKey);
            if (string.IsNullOrEmpty(expirationStr))
            {
                _tokenExpiration = null;
            }
            else if (DateTime.TryParse(expirationStr, out var expiration))
            {
                _tokenExpiration = expiration;
            }
            else
            {
                _tokenExpiration = null;
            }
            _tokenExpirationLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetTokenExpirationAsync error: {ex.Message}");
            return null;
        }
        return _tokenExpiration;
    }

    public async Task SetTokenExpirationAsync(DateTime? expiration)
    {
        _tokenExpiration = expiration;
        _tokenExpirationLoaded = true;
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
        if (_usernameLoaded) return _username;
        try
        {
            _username = await SecureStorage.GetAsync(UsernameKey);
            _usernameLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetUsernameAsync error: {ex.Message}");
            return null;
        }
        return _username;
    }

    public async Task SetUsernameAsync(string? username)
    {
        _username = username;
        _usernameLoaded = true;
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
        if (_userRoles != null) return _userRoles;
        try
        {
            var rolesJson = await SecureStorage.GetAsync(UserRolesKey);
            if (string.IsNullOrEmpty(rolesJson))
            {
                _userRoles = new List<string>();
            }
            else
            {
                try
                {
                    _userRoles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rolesJson) ?? new List<string>();
                }
                catch
                {
                    _userRoles = new List<string>();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetUserRolesAsync error: {ex.Message}");
            return new List<string>();
        }
        return _userRoles;
    }

    public async Task SetUserRolesAsync(List<string> roles)
    {
        _userRoles = roles ?? new List<string>();
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
        if (_teamNumberLoaded) return _teamNumber;
        try
        {
            var val = await SecureStorage.GetAsync(TeamNumberKey);
            if (string.IsNullOrEmpty(val)) 
            {
                _teamNumber = null;
            }
            else if (int.TryParse(val, out var n)) 
            {
                _teamNumber = n;
            }
            else
            {
                _teamNumber = null;
            }
            _teamNumberLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetTeamNumberAsync error: {ex.Message}");
            return null;
        }
        return _teamNumber;
    }

    public async Task SetTeamNumberAsync(int? teamNumber)
    {
        _teamNumber = teamNumber;
        _teamNumberLoaded = true;
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
            SafeRemoveSecureStorage(TokenKey);
            SafeRemoveSecureStorage(TokenExpirationKey);
            SafeRemoveSecureStorage(UsernameKey);
            SafeRemoveSecureStorage(UserRolesKey);
            SafeRemoveSecureStorage(TeamNumberKey);
            SafeRemoveSecureStorage(EmailKey);
            
            // Clear cache thread-safely
            lock (_cacheLock)
            {
                _token = null;
                _tokenLoaded = true;
                
                _tokenExpiration = null;
                _tokenExpirationLoaded = true;
                
                _username = null;
                _usernameLoaded = true;
                
                _userRoles = null;
                
                _teamNumber = null;
                _teamNumberLoaded = true;
                
                _email = null;
                _emailLoaded = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] ClearAuthDataAsync error: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    // Helper method to safely remove a key from SecureStorage
    private static void SafeRemoveSecureStorage(string key)
    {
        try
        {
            SecureStorage.Remove(key);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SafeRemoveSecureStorage({key}) error: {ex.Message}");
        }
    }

    public async Task<string> GetThemeAsync()
    {
        if (_theme != null) return _theme;
        try
        {
            _theme = await SecureStorage.GetAsync(ThemeKey) ?? DefaultTheme;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetThemeAsync error: {ex.Message}");
            _theme = DefaultTheme;
        }
        return _theme;
    }

    public async Task SetThemeAsync(string theme)
    {
        _theme = theme ?? DefaultTheme;
        try
        {
            await SecureStorage.SetAsync(ThemeKey, _theme);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetThemeAsync error: {ex.Message}");
        }
    }

    public async Task<string?> GetEmailAsync()
    {
        if (_emailLoaded) return _email;
        try
        {
            _email = await SecureStorage.GetAsync(EmailKey);
            _emailLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetEmailAsync error: {ex.Message}");
            return null;
        }
        return _email;
    }

    public async Task SetEmailAsync(string? email)
    {
        _email = email;
        _emailLoaded = true;
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
        if (_offlineMode.HasValue) return _offlineMode.Value;
        try
        {
            var val = await SecureStorage.GetAsync(OfflineModeKey);
            if (string.IsNullOrEmpty(val)) 
            {
                _offlineMode = false;
            }
            else
            {
                _offlineMode = bool.TryParse(val, out var b) && b;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetOfflineModeAsync error: {ex.Message}");
            return false;
        }
        return _offlineMode.Value;
    }

    public async Task SetOfflineModeAsync(bool enabled)
    {
        _offlineMode = enabled;
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
        if (_notificationsEnabled.HasValue) return _notificationsEnabled.Value;
        try
        {
            var v = await SecureStorage.GetAsync(NotificationsEnabledKey);
            if (string.IsNullOrEmpty(v)) 
            {
                _notificationsEnabled = true; // default enabled
            }
            else
            {
                _notificationsEnabled = v == "1";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetNotificationsEnabledAsync error: {ex.Message}");
            return true;
        }
        return _notificationsEnabled.Value;
    }

    public async Task SetNotificationsEnabledAsync(bool enabled)
    {
        _notificationsEnabled = enabled;
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
        if (_networkTimeout.HasValue) return _networkTimeout.Value;
        try
        {
            var val = await SecureStorage.GetAsync(NetworkTimeoutKey);
            if (string.IsNullOrEmpty(val)) 
            {
                _networkTimeout = DefaultNetworkTimeout;
            }
            else if (int.TryParse(val, out var timeout))
            {
                // Clamp between 5 and 60 seconds for safety
                _networkTimeout = Math.Clamp(timeout, 5, 60);
            }
            else
            {
                _networkTimeout = DefaultNetworkTimeout;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetNetworkTimeoutAsync error: {ex.Message}");
            return DefaultNetworkTimeout;
        }
        return _networkTimeout.Value;
    }

    public async Task SetNetworkTimeoutAsync(int timeoutSeconds)
    {
        // Clamp between 5 and 60 seconds for safety
        var clamped = Math.Clamp(timeoutSeconds, 5, 60);
        _networkTimeout = clamped;
        try
        {
            await SecureStorage.SetAsync(NetworkTimeoutKey, clamped.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetNetworkTimeoutAsync error: {ex.Message}");
        }
    }

    public async Task<bool> GetAutoUpdateCheckAsync()
    {
        if (_autoUpdateCheck.HasValue) return _autoUpdateCheck.Value;
        try
        {
            var v = await SecureStorage.GetAsync(AutoUpdateCheckKey);
            if (string.IsNullOrEmpty(v))
            {
                _autoUpdateCheck = false; // default off
            }
            else
            {
                _autoUpdateCheck = v == "1";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] GetAutoUpdateCheckAsync error: {ex.Message}");
            return false;
        }
        return _autoUpdateCheck.Value;
    }

    public async Task SetAutoUpdateCheckAsync(bool enabled)
    {
        _autoUpdateCheck = enabled;
        try
        {
            await SecureStorage.SetAsync(AutoUpdateCheckKey, enabled ? "1" : "0");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] SetAutoUpdateCheckAsync error: {ex.Message}");
        }
    }
}
