# ObsidianScout

ObsidianScout is a cross-platform .NET MAUI application for scouting robotics competitions. It provides offline caching, team and match management, and data synchronization with remote APIs.

This repository contains the .NET MAUI application targeting Android and Windows (net10.0).


## [Download](https://github.com/steveandjeff999/ObsidianScoutApp/tree/master/ObsidianScout/apks/)
[Select version and install here](https://github.com/steveandjeff999/ObsidianScoutApp/tree/master/ObsidianScout/apks/)

## What this project is

- A mobile/desktop app to collect scouting data at robotics events
- Local offline caching with optional sync to a remote OBSIDIAN Scout Mobile API
- MVVM architecture using .NET MAUI

## Notable features

- Cross-platform UI with .NET MAUI (Android, iOS, Mac Catalyst, Windows)
- Offline caching using `SecureStorage` with file fallback for larger payloads
- Team, event, match, and scouting data management
- Simple profile picture caching
- Extensible metric definitions and team metrics

## Getting started

Prerequisites
- .NET10 SDK
- Visual Studio with .NET MAUI workload (or VS Code with MAUI support)

Quick build

```bash
# Restore and build
dotnet restore
dotnet build -f net10.0-windows10.0.19041.0
```

Run
- Use Visual Studio to run on Android/Windows, or use `dotnet run` with the appropriate TFM for basic testing.

## Project structure

Key folders:
- `Models/` - Data models for API responses and local data
- `Services/` - API, caching, and settings services
- `ViewModels/` - MVVM viewmodels
- `Views/` - XAML pages and UI
- `Platforms/` - platform-specific configuration

## Documentation

See the `docs/` folder for developer guides and module documentation:
- `docs/getting-started.md` - setup and running the app
- `docs/development.md` - development workflow and contribution guidelines
- `docs/cache-service.md` - notes about the caching layer implementation

## Contributing

Contributions are welcome. Please open issues or pull requests on GitHub.

## License


