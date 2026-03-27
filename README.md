# Simplerr 🎬

![simplerr](https://img.shields.io/badge/simplerr-tui-39FF14?style=flat-square&logoColor=black&labelColor=0D0D0D) ![rr](https://img.shields.io/badge/rr--native-stack-FFB300?style=flat-square&labelColor=0D0D0D) ![Spectre.Console](https://img.shields.io/badge/Spectre.Console-powered-00CFFF?style=flat-square&labelColor=0D0D0D)

> A terminal UI for managing your self-hosted media suite — WSL2, Docker,
> and all your *arr services from a single keyboard-driven dashboard.

Built for the *arr stack. Fits right in beside Sonarr, Radarr, Prowlarr.

---

## What It Does

- **WSL2 controls** — Start, stop and restart your WSL2 distro from Windows
- **Container dashboard** — Live status of all your Docker containers
- **Resource gauges** — Staggered CPU and memory stats per container
- **Container actions** — Start, stop, restart any container instantly
- **Log viewer** — Tail and filter logs per container without leaving the TUI
- **Cross-platform** — Runs on Windows (with or without WSL2) and Linux natively

---

## Screenshots

```text
╔══════════════════════════════════════════════════════════════════╗
║                    Simplerr  Media Suite Manager                 ║
╠══════════════════════════════════════════════════════════════════╣
║  ● WSL: Running — Ubuntu-Media                                   ║
║  [ F1 ] Start   [ F2 ] Stop   [ F3 ] Restart                     ║
╠══════════════════════════════════════════════════════════════════╣
║  #   Service              Status     CPU              Memory     ║
║  ├─────────────────────────────────────────────────────────────   ║
║  1   📺 Plex Media Server  ● Running  ██░░░░░░░░░░ 12%  █░░ 1.2GB║
║  2   📡 Sonarr (TV)        ● Running  █░░░░░░░░░░░  4%  █░░ 300MB║
║> 3   🎬 Radarr (Movies)    ● Running  █░░░░░░░░░░░  3%  █░░ 280MB║
║  4   🔽 qBittorrent        ○ Stopped                             ║
║  5   🔍 Prowlarr           ● Running  ░░░░░░░░░░░░  1%  ░░░ 120MB║
╠══════════════════════════════════════════════════════════════════╣
║  Selected: 🎬 Radarr (Movies)                                    ║
║  [ S ] Start   [ T ] Stop   [ R ] Restart   [ L ] Logs           ║
╠══════════════════════════════════════════════════════════════════╣
║  [↑↓] Navigate  [F1] WSL Start  [F2] WSL Stop  [F3] WSL Restart  ║
║  [S] Start  [T] Stop  [R] Restart  [L] Logs  [Q] Quit            ║
╚══════════════════════════════════════════════════════════════════╝
```

---

## Requirements

### Windows
- Windows 10 21H2 or later / Windows 11
- [WSL2](https://learn.microsoft.com/en-us/windows/wsl/install) (optional,
  but required for WSL controls)
- Docker running inside WSL2 **or** Docker Desktop
- .NET 8 runtime (not required if using the self-contained binary)

### Linux
- Docker installed and running
- Current user in the `docker` group  
  (`sudo usermod -aG docker $USER` then log out and back in)
- .NET 8 runtime (not required if using the self-contained binary)

---

## Installation

### Option A — Installer Script (Recommended)

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/phellams/simplerr/main/install.ps1 | iex
```

**Linux (bash):**
```bash
curl -fsSL https://raw.githubusercontent.com/phellams/simplerr/main/install.sh | bash
```

### Option B — Manual

1. Download the latest release for your platform from the
   [Releases](https://github.com/phellams/simplerr/releases) page
2. Extract and place `simplerr.exe` (Windows) or `simplerr` (Linux)
   somewhere on your PATH
3. Run it

### Option C — Build From Source

```bash
git clone https://github.com/phellams/simplerr.git
cd simplerr

# Windows
./build-windows.sh

# Linux
./build-linux.sh
```

Binaries output to `./dist/`.

---

## Configuration

On first run, Simplerr creates a default config file at:

| Platform | Path |
|---|---|
| Windows | `C:\Users\<you>\.phellams\simplerr\config.json` |
| Linux | `/home/<you>/.phellams/simplerr/config.json` |

### Full Config Reference

```json
{
  "wsl": {
    "enabled": true,
    "distroName": "Ubuntu-Media"
  },
  "docker": {
    "composePath": "/home/user/media-stack",
    "pollIntervalSeconds": 5,
    "statsPollIntervalSeconds": 3
  },
  "services": [
    {
      "name": "plex",
      "icon": "📺",
      "friendlyName": "Plex Media Server"
    },
    {
      "name": "sonarr",
      "icon": "📡",
      "friendlyName": "Sonarr (TV)"
    },
    {
      "name": "radarr",
      "icon": "🎬",
      "friendlyName": "Radarr (Movies)"
    },
    {
      "name": "qbittorrent",
      "icon": "🔽",
      "friendlyName": "qBittorrent"
    },
    {
      "name": "prowlarr",
      "icon": "🔍",
      "friendlyName": "Prowlarr"
    }
  ]
}
```

### Config Options

#### `wsl`
| Field | Type | Description |
|---|---|---|
| `enabled` | bool | Show WSL controls. Set `false` on Linux or if not using WSL2 |
| `distroName` | string | Exact name of your WSL2 distro (`wsl --list` to find it) |

#### `docker`
| Field | Type | Default | Description |
|---|---|---|---|
| `composePath` | string | — | Path to your `docker-compose.yml` directory (informational for now) |
| `pollIntervalSeconds` | int | `5` | How often to refresh container list |
| `statsPollIntervalSeconds` | int | `3` | Delay between staggered stats polls per container |

#### `services`
| Field | Type | Description |
|---|---|---|
| `name` | string | Container name (partial match supported) |
| `icon` | string | Emoji or character shown in the dashboard |
| `friendlyName` | string | Human-readable label shown in the UI |

> **Tip:** If `services` is empty, Simplerr shows **all** running containers
> rather than filtering. Useful for exploring a new setup.

---

## Keyboard Reference

### Global
| Key | Action |
|---|---|
| `↑` / `↓` | Navigate container list |
| `Q` | Quit Simplerr |
| `?` | Show help overlay *(coming soon)* |

### WSL Controls *(Windows only)*
| Key | Action |
|---|---|
| `F1` | Start WSL distro |
| `F2` | Stop WSL (shutdown) |
| `F3` | Restart WSL distro |

### Container Actions
| Key | Action |
|---|---|
| `S` | Start selected container |
| `T` | Stop selected container |
| `R` | Restart selected container |
| `L` | Open log viewer |

### Log Viewer
| Key | Action |
|---|---|
| `F` | Set filter string |
| `C` | Clear filter |
| `Q` / `Esc` | Return to dashboard |

---

## Typical Media Stack Setup

Simplerr is designed around a Docker Compose media suite running inside WSL2.
A typical stack it manages well:

| Service | Role |
|---|---|
| [Plex](https://www.plex.tv/) | Media server and streaming |
| [Sonarr](https://sonarr.tv/) | TV show management |
| [Radarr](https://radarr.video/) | Movie management |
| [Prowlarr](https://github.com/Prowlarr/Prowlarr) | Indexer manager |
| [qBittorrent](https://www.qbittorrent.org/) | Torrent client |
| [Overseerr](https://overseerr.dev/) | Request management |
| [Tdarr](https://home.tdarr.io/) | Media transcoding |

Add any of these to your `services` config array and Simplerr will pick
them up automatically by name.

---

## Troubleshooting

### "No containers found"
- Confirm Docker is running: `docker ps` in a terminal
- On Windows, confirm Docker is accessible from PowerShell (not just WSL)
- Check your `services[].name` values match your actual container names
  (`docker ps --format "{{.Names}}"` to list them)

### WSL controls not showing
- Confirm `wsl.enabled` is `true` in your config
- Confirm Simplerr is running on Windows, not inside WSL itself
- Run `wsl --list` in PowerShell to verify your distro name matches config

### Stats showing `--`
- Stats populate with a slight delay due to staggered polling
- If they never appear, confirm `docker stats --no-stream <id>` works
  in your terminal

### Permission denied (Linux)
- Add your user to the docker group:
  ```bash
  sudo usermod -aG docker $USER
  ```
  Then log out and back in.

---

## Roadmap

- [ ] Help overlay (`?`)
- [ ] Docker Compose `up` / `down` support
- [ ] Notification on container crash
- [ ] Config editor inside the TUI
- [ ] Community service presets (media, dev, home-lab)
- [ ] .NET 10 + AOT build target
- [ ] macOS support

---

## Contributing

PRs welcome. The codebase is intentionally small and layered — adding a new
platform or service backend means implementing one interface.

```text
IWslService   → new platform host support
IDockerService → new container runtime support
```

---

## Name

*Simplerr* is a nod to the \*arr stack (Sonarr, Radarr, Prowlarr...).
It does one thing — makes managing all of them simpler.

---

## License

MIT — do whatever you like with it.