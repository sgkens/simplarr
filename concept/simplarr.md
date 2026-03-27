# Simplerr 🎬

## Cross-Platform Support Design

```text
Simplerr
├── Windows + WSL2 mode
│   ├── WSL controls visible (start/stop/restart)
│   └── docker.exe → proxies to WSL2 Docker
│
└── Linux native mode  
    ├── WSL controls hidden (not applicable)
    └── docker binary → direct Unix socket
```

### Runtime Detection

```csharp
public enum RuntimeEnvironment
{
    WindowsWithWsl,
    WindowsWithoutWsl,  // Docker Desktop native Windows
    Linux,
    MacOs               // future, free with abstraction
}
```

Auto-detected at startup — user doesn't configure this, it just works.

---

## Revised Architecture

```text
simplerr/
├── src/
│   ├── Simplerr.csproj
│   ├── Program.cs
│   ├── Config/
│   │   ├── Config.cs              # Model
│   │   └── ConfigLoader.cs        # Loads from $HOME/.phellams/simplerr/
│   ├── Environment/
│   │   └── EnvironmentDetector.cs # Detects WSL/Linux/Windows
│   ├── Services/
│   │   ├── Wsl/
│   │   │   ├── IWslService.cs
│   │   │   ├── WslService.cs      # Real impl
│   │   │   └── NullWslService.cs  # No-op for Linux
│   │   └── Docker/
│   │       ├── IDockerService.cs
│   │       ├── DockerShellService.cs   # Shells out to docker
│   │       └── Models/
│   │           ├── ContainerInfo.cs
│   │           └── ContainerStats.cs
│   └── Ui/
│       ├── Dashboard.cs           # Main live display
│       ├── LogViewer.cs           # Log panel
│       └── Components/
│           ├── WslPanel.cs
│           ├── ContainerTable.cs
│           ├── StatsGauges.cs
│           └── KeyHintBar.cs
└── README.md
```

---

## Config Path — Cross Platform

```csharp
// Resolves correctly on both platforms
private static string GetConfigDir() =>
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".phellams",
        "simplerr"
    );
// Windows → C:\Users\user\.phellams\simplerr\config.json
// Linux   → /home/user/.phellams/simplerr/config.json
```

---

## Updated Config

```json
{
  "wsl": {
    "enabled": true,
    "distroName": "Ubuntu-Media"
  },
  "docker": {
    "composePath": "/home/user/media-stack",
    "pollIntervalSeconds": 5,
    "statsPollIntervalSeconds": 2
  },
  "services": [
    { "name": "plex",        "icon": "📺", "friendlyName": "Plex Media Server" },
    { "name": "sonarr",      "icon": "📡", "friendlyName": "Sonarr (TV)"       },
    { "name": "radarr",      "icon": "🎬", "friendlyName": "Radarr (Movies)"   },
    { "name": "qbittorrent", "icon": "🔽", "friendlyName": "qBittorrent"       },
    { "name": "prowlarr",    "icon": "🔍", "friendlyName": "Prowlarr"          }
  ]
}
```

---

## Distribution Targets

```bash
# Windows (primary)
dotnet publish -r win-x64 --self-contained -o ./dist/windows

# Linux (bonus)
dotnet publish -r linux-x64 --self-contained -o ./dist/linux
```

Both as single `.exe` / binary — user just runs it.

---

## Potential Future

Since you're already cross-platform and have the *arr naming...

```text
simplerr on GitHub → community adds their own service presets
  ├── media-suite  (plex, sonarr, radarr, prowlarr, qbit)
  ├── dev-stack    (postgres, redis, nginx)
  └── home-lab     (pihole, homeassistant, etc.)
```