# OBSIDIAN Scout Mobile App

A cross-platform .NET MAUI mobile application for scouting robotics competitions. This app integrates with the OBSIDIAN Scout Mobile API to provide comprehensive scouting capabilities for FRC (FIRST Robotics Competition) teams.

## Features

- ?? **Secure Authentication** - JWT-based authentication with automatic token management
- ?? **Configurable Server** - Flexible server configuration supporting HTTP/HTTPS, IP addresses, domains, and custom ports
- ?? **SSL Support** - Works with HTTP, valid SSL certificates, and self-signed certificates
- ?? **Match Scouting** - Comprehensive scouting forms for autonomous, teleop, and endgame performance
- ?? **Team Management** - View and browse team information
- ?? **Event Tracking** - Access event schedules and match data
- ?? **Offline Support** - Built-in support for offline data collection and syncing
- ?? **Cross-Platform** - Runs on iOS, Android, Windows, and macOS

## SSL & Certificate Support

The app is configured to work with:
- ? **HTTP connections** (e.g., `http://192.168.1.100:8080`)
- ? **HTTPS with valid certificates** (e.g., `https://scout.team.com:443`)
- ? **HTTPS with self-signed certificates** (for development/testing)

**No certificate warnings or errors!**

See [SSL_CONFIGURATION.md](SSL_CONFIGURATION.md) for detailed information about SSL setup and security considerations.

## Architecture

### Project Structure

```
ObsidianScout/
??? Models/              # Data models for API responses
?   ??? ApiResponse.cs
?   ??? User.cs
?   ??? Team.cs
?   ??? Event.cs
?   ??? Match.cs
?   ??? ScoutingData.cs
??? Services/            # Business logic and API communication
?   ??? ISettingsService.cs
?   ??? SettingsService.cs
?   ??? IApiService.cs
?   ??? ApiService.cs
??? ViewModels/          # MVVM ViewModels
?   ??? LoginViewModel.cs
?   ??? MainViewModel.cs
?   ??? TeamsViewModel.cs
?   ??? EventsViewModel.cs
?   ??? ScoutingViewModel.cs
??? Views/               # UI Pages
?   ??? LoginPage.xaml
?   ??? TeamsPage.xaml
?   ??? EventsPage.xaml
?   ??? ScoutingPage.xaml
??? Converters/          # Value converters for data binding
?   ??? ValueConverters.cs
??? Platforms/           # Platform-specific configuration
?   ??? Android/         # Android SSL/HTTP configuration
?   ??? iOS/             # iOS SSL/HTTP configuration
?   ??? MacCatalyst/     # macOS SSL/HTTP configuration
?   ??? Windows/         # Windows configuration
??? Resources/           # App resources (styles, images, fonts)
```

### Design Patterns

- **MVVM (Model-View-ViewModel)** - Clean separation of concerns
- **Dependency Injection** - Using .NET MAUI's built-in DI container
- **Repository Pattern** - Services abstract API communication
- **Secure Storage** - Using MAUI SecureStorage for sensitive data

## Configuration

### Server Configuration

The app allows flexible server configuration through the login screen:

1. **Protocol**: Choose between HTTP or HTTPS
2. **Server Address**: Enter an IP address (e.g., `192.168.1.100`) or domain name (e.g., `scout.example.com`)
3. **Port**: Specify the port number (e.g., `8080` for HTTP or `443` for HTTPS)

The app automatically constructs the full URL: `{protocol}://{address}:{port}`

#### Example Configurations

**Local Development (HTTP):**
- Protocol: `http`
- Address: `192.168.1.100`
- Port: `8080`
- Result: `http://192.168.1.100:8080`

**Local Development (HTTPS with self-signed cert):**
- Protocol: `https`
- Address: `192.168.1.100`
- Port: `8443`
- Result: `https://192.168.1.100:8443`
- Status: ? Works without certificate errors

**Production Server:**
- Protocol: `https`
- Address: `scout.team5454.com`
- Port: `443`
- Result: `https://scout.team5454.com:443`

### Authentication

**Login Requirements:**
- Username (provided by team admin)
- Password (secure password)
- Team Number (your FRC team number, e.g., `5454`)

### Settings Storage

All settings are securely stored using MAUI's `SecureStorage` API:
- Server configuration (protocol, address, port)
- Authentication token (JWT)
- Token expiration timestamp

## API Integration

The app integrates with the OBSIDIAN Scout Mobile API v1.0:

### Endpoints Used

- `POST /api/mobile/auth/login` - User authentication (requires team number)
- `POST /api/mobile/auth/refresh` - Token refresh
- `GET /api/mobile/auth/verify` - Token verification
- `GET /api/mobile/teams` - Retrieve team list
- `GET /api/mobile/events` - Retrieve event list
- `GET /api/mobile/matches` - Retrieve match schedule
- `POST /api/mobile/scouting/submit` - Submit scouting data
- `POST /api/mobile/scouting/bulk-submit` - Bulk offline sync
- `GET /api/mobile/health` - Server health check

### Authentication Flow

1. User enters credentials (username, password, **team number**) on login screen
2. App sends login request to server
3. Server returns JWT token with 7-day expiration
4. Token is stored securely using SecureStorage
5. Token is included in all subsequent API requests
6. App automatically navigates to login if token expires

## Features Detail

### Login Page
- Username/password authentication
- **Team number** entry (required)
- Server configuration panel (HTTP/HTTPS, IP/domain, port)
- Connection testing
- SSL certificate support (including self-signed)
- Error handling and user feedback

### Main Page (Home)
- Welcome message with user info
- Quick navigation to key features
- Logout functionality

### Scouting Page
- Match and team ID selection
- Autonomous scoring (Speaker & Amp notes)
- Teleop scoring (Speaker & Amp notes)
- Endgame climb status
- Additional notes field
- Form validation
- Submit with confirmation

### Teams Page
- Scrollable list of teams
- Pull-to-refresh functionality
- Team details: number, name, location
- Scouting data count per team

### Events Page
- List of events
- Event details: name, code, location, dates
- Team count per event
- Pull-to-refresh

## Development

### Prerequisites

- .NET 10 SDK
- Visual Studio 2022 or later (or VS Code with MAUI extension)
- Platform-specific requirements:
  - **Android**: Android SDK
  - **iOS**: macOS with Xcode
  - **Windows**: Windows 10/11 SDK
  - **macOS**: Xcode command line tools

### Building the Project

```bash
# Restore dependencies
dotnet restore

# Build for specific platform
dotnet build -f net10.0-android
dotnet build -f net10.0-ios
dotnet build -f net10.0-windows10.0.19041.0
dotnet build -f net10.0-maccatalyst

# Run on Android
dotnet run -f net10.0-android

# Run on Windows
dotnet run -f net10.0-windows10.0.19041.0
```

### NuGet Packages

- `Microsoft.Maui.Controls` - Core MAUI framework
- `Microsoft.Extensions.Logging.Debug` - Debug logging
- `CommunityToolkit.Mvvm` - MVVM helpers and source generators

## Security

### Secure Data Storage

The app uses platform-specific secure storage:
- **Android**: Android KeyStore
- **iOS**: iOS Keychain
- **Windows**: Data Protection API (DPAPI)
- **macOS**: macOS Keychain

### SSL/TLS Support

- ? Supports HTTP for local development
- ? Supports HTTPS with valid certificates
- ? Supports HTTPS with self-signed certificates
- ?? Current configuration accepts all certificates (suitable for development/internal use)
- ?? Can be customized for stricter validation in production

See [SSL_CONFIGURATION.md](SSL_CONFIGURATION.md) for details.

### Best Practices

- JWT tokens are never logged or displayed
- Passwords are not stored locally
- All API communication should use HTTPS in production
- Token expiration is checked before each request
- Automatic logout on token expiration
- Self-signed certificates accepted for development convenience

## Troubleshooting

### Connection Issues

1. Verify server is running and accessible
2. Check firewall settings
3. Use "Test Connection" in server configuration
4. For local development, ensure device is on same network
5. **For HTTPS with self-signed certs**: Should work without errors
6. **For HTTP**: Ensure protocol is set to `http`, not `https`

### Authentication Issues

1. Verify credentials are correct (username, password, **team number**)
2. Check if token has expired
3. Try logging out and logging back in
4. Verify server URL is correct
5. **Team number must be numeric** (e.g., `5454`, not `Team 5454`)

### SSL Certificate Issues

1. **Should not occur** - app accepts all certificates
2. If you see certificate errors, check [SSL_CONFIGURATION.md](SSL_CONFIGURATION.md)
3. Verify platform-specific files are properly configured

### Build Issues

1. Clean solution: `dotnet clean`
2. Restore packages: `dotnet restore`
3. Rebuild: `dotnet build`
4. Check .NET 10 SDK is installed

## Documentation

- [README.md](README.md) - This file (overview and technical details)
- [QUICKSTART.md](QUICKSTART.md) - User quick start guide
- [USAGE_EXAMPLES.md](USAGE_EXAMPLES.md) - Real-world usage scenarios
- [SSL_CONFIGURATION.md](SSL_CONFIGURATION.md) - SSL/certificate configuration details
- [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) - Technical implementation details
- [DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md) - Deployment and release guide

## API Documentation

For complete API documentation, refer to the OBSIDIAN Scout Mobile API Documentation provided with your server installation. Key points:

- Base URL: `{server_url}/api/mobile`
- Authentication: Bearer token in Authorization header (requires team number at login)
- Content-Type: `application/json`
- Token expiration: 7 days

## Future Enhancements

- [ ] Offline data caching and sync
- [ ] Pit scouting functionality
- [ ] Match schedule integration
- [ ] Team statistics and analytics
- [ ] Image upload for pit scouting
- [ ] Push notifications for match reminders
- [ ] QR code scanning for quick data entry
- [ ] Export data to CSV/Excel
- [ ] Certificate pinning for enhanced security (optional)

## Support

For issues or questions:
1. Check the troubleshooting section
2. Review API documentation
3. Check [SSL_CONFIGURATION.md](SSL_CONFIGURATION.md) for certificate issues
4. Check server logs for detailed error messages
5. Contact your team's system administrator

## License

Copyright © 2024-2025 OBSIDIAN Scout Team

## Version History

### v1.0.1 (2025)
- ? Added support for self-signed SSL certificates
- ? Added HTTP support for local development
- ? Added team number to login (API requirement)
- ?? Comprehensive SSL configuration documentation
- ?? Platform-specific SSL/HTTP configurations

### v1.0.0 (2024)
- Initial release
- Login and authentication
- Team and event browsing
- Match scouting
- Configurable server settings (HTTP/HTTPS, IP/domain, custom port)
- Cross-platform support (iOS, Android, Windows, macOS)
