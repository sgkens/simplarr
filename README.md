# Simplerr 🎬

![simplerr](https://img.shields.io/badge/simplerr-tui-39FF14?style=flat-square&logoColor=black&labelColor=0D0D0D) ![rr](https://img.shields.io/badge/rr--native-stack-FFB300?style=flat-square&labelColor=0D0D0D) ![Spectre.Console](https://img.shields.io/badge/Spectre.Console-powered-00CFFF?style=flat-square&labelColor=0D0D0D)

> A lightweight Terminal UI for managing your self-hosted media suite. Control WSL2, Docker containers, and your entire *arr stack from a single, high-performance dashboard.

---

## Key Features

- 🟦 **WSL2 Management** — Start, stop and restart your WSL2 distro directly from Windows.
- 🔸 **Global Resources** — Real-time aggregate CPU and Memory tracking for your entire stack.
- 🟩 **Container Dashboard** — Live status, image details, and instant management (Start/Stop/Restart).
- 🔄 **Dynamic Scrolling** — Smoothly navigate large container lists with automatic pagination and scroll indicators.
- 🔹 **Log Viewer** — Real-time log tailing with keyword filtering and high-speed sanitization.
- 🔄 **Event-Driven TUI** — Zero-lag input response and optimized background polling for a snappy experience.

---

## Quick Start

### Installation

**Option A — Scoop (Windows)**
```powershell
scoop bucket add phellams https://gitlab.com/phellams/scoop-bucket.git
scoop install simplerr
```

**Option B — Homebrew (macOS / Linux)**
```bash
brew tap phellams/tap
brew install simplerr
```

**Option C — One-Liner Installers**
- **Windows:** `irm https://raw.githubusercontent.com/phellams/simplerr/main/install.ps1 | iex`
- **Linux:** `curl -fsSL https://raw.githubusercontent.com/phellams/simplerr/main/install.sh | bash`

---

## Dashboard Preview

```text
╔══════════════════════════════════════════════════════════════════╗
║                    Simplerr  Media Suite Manager                 ║
╠══════════════════════════════════════════════════════════════════╣
║  ● WSL: Running — Ubuntu-Media                                   ║
║  [ F1 ] Start   [ F2 ] Stop   [ F3 ] Restart                     ║
╠══════════════════════════════════════════════════════════════════╣
║  Global Stack Resources: CPU: 22.4%   Memory: 2.1GB / 16.0GB     ║
╠══════════════════════════════════════════════════════════════════╣
║  #   Service              Status     CPU              Memory     ║
║  ├─────────────────────────────────────────────────────────────   ║
║  1   📺 Plex Media Server  ● Running  ██░░░░░░░░░░ 12%  █░░ 1.2GB║
║  2   📡 Sonarr (TV)        ● Running  █░░░░░░░░░░░  4%  █░░ 300MB║
║> 3   🎬 Radarr (Movies)    ● Running  █░░░░░░░░░░░  3%  █░░ 280MB║
╚══════════════════════════════════════════════════════════════════╝
   Containers (1-10 of 25)  [↑/↓] to Scroll
```

---

## Configuration

Simplerr automatically creates a config at `~/.phellams/simplerr/config.json`.

```json
{
  "wsl": { "enabled": true, "distroName": "Ubuntu-Media" },
  "docker": { "pollIntervalSeconds": 5 },
  "services": [
    { "name": "plex", "icon": "📺", "friendlyName": "Plex Media Server" },
    { "name": "sonarr", "icon": "📡" }
  ]
}
```

---

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `↑` / `↓` | Navigate container list (Auto-Scroll) |
| `S` / `T` / `R` | Start / Stop / Restart selected service |
| `L` | View real-time logs |
| `F` | Set log filter / `C` Clear filter |
| `?` / `H` | Show Help Overlay |
| `Q` / `Esc` | Back / Quit |

---

## License

Distributed under the [MIT License](LICENSE). Built with ❤️ for the self-hosted community.
