feat: initial scaffolding of Simplerr

Add cross-platform TUI for managing a self-hosted Docker media suite
running under WSL2 or natively on Linux.

- Project scaffold targeting .NET 8 (win-x64, linux-x64)
- Cross-platform environment detection (Windows/WSL2/Linux/macOS)
- Config system with auto-generated default at ~/.phellams/simplerr/config.json
- WSL2 service (start/stop/restart distro) with no-op stub for Linux hosts
- Docker shell service (container list, staggered stats, start/stop/restart, log tail)
- Thread-safe AppState with staggered per-container stats polling
- Spectre.Console live dashboard (container table, CPU/mem gauges, WSL panel)
- Keyboard-driven input (navigate, start/stop/restart container, log viewer)
- Log viewer with live tail and keyword filter
- Windows PowerShell installer (install.ps1) with preflight checks,
  PATH management, Start Menu shortcut, and uninstall support
- Linux bash installer (install.sh) with arch detection (x64/arm64/arm),
  PATH management, desktop entry, and uninstall support
- README with full config reference, keyboard map, and troubleshooting guide