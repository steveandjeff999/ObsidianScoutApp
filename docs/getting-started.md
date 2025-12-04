# Getting Started

This document explains how to build and run ObsidianScout locally for development.

## Prerequisites

- .NET10 SDK
- Visual Studio2022 or later with .NET MAUI workload (or VS Code with MAUI support)
- Platform-specific tools (Android SDK, Xcode for iOS/macOS)

## Build

```bash
# Restore dependencies
dotnet restore

# Build for Windows
dotnet build -f net10.0-windows10.0.19041.0

# Build for Android
dotnet build -f net10.0-android
```

## Run

Open the solution in Visual Studio and select the target platform (Android, iOS, Windows, MacCatalyst), then run from the IDE. For quick testing on Windows you can use `dotnet run` with the Windows TFM.

## Configuration

- Server settings and tokens are stored in MAUI `SecureStorage`.
- To reset cached data, use the app settings page or delete files in the app data directory.

## Troubleshooting

- If you see build errors, ensure the .NET10 SDK and MAUI workloads are installed.
- For platform-specific runtime issues, consult MAUI platform docs.
