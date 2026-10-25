# JaxClient

JaxClient is a Windows desktop launcher for managing Minecraft profiles and starting the game with the selected version and mod loader.

## Features

- Create and manage Minecraft profiles.
- Launch profiles with Vanilla, Fabric, Forge, and NeoForge.
- Browse mods and resource packs from Modrinth and CurseForge.
- Import and export profiles, including `.jxcpack`, `.noriskpack`, and Modrinth `.mrpack` packs.
- Import Minecraft options and run supported profiles offline.
- Download and prepare Minecraft files and Java runtimes required by a profile.

## Requirements

- Windows
- .NET 8 SDK
- An internet connection for restoring dependencies, downloading game files, and online account features

The launcher manages Java runtimes for Minecraft profiles. You do not need to install a separate Java runtime just to build the launcher.

## Build and run

Open PowerShell in this directory and run:

```powershell
dotnet restore .\JaxClient-v3.csproj
dotnet build .\JaxClient-v3.csproj --configuration Release
dotnet run --project .\JaxClient-v3.csproj
```

To publish a single-file Windows build:

```powershell
dotnet publish .\JaxClient-v3.csproj --configuration Release --runtime win-x64 --self-contained
```

## Project layout

- `MainWindow.xaml` and `MainWindow.xaml.cs` — launcher UI and application flows
- `Languages\` — English and German localization resources
- `Assets\` — application artwork and other bundled resources
- `BodyRendering\` and `HeadRendering\` — Minecraft skin rendering

## Contributing

Bug reports and contributions are welcome. Include the launcher version, Windows version, steps to reproduce, and relevant log output when reporting a problem.
