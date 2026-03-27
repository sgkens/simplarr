# WSL Media Suite TUI - Design Thoughts

Great idea! Let me break down the technical approach and scope this properly.

## Technology Choice

**Razor Console won't work here** — Razor is ASP.NET's HTML templating engine. For a TUI you want:

| Option | Language | Verdict |
|---|---|---|
| **Spectre.Console** | C# | ✅ Best choice — rich TUI, gauges, tables, panels |
| Terminal.Gui | C# | ✅ Good — more widget-focused |
| Textual | Python | ✅ Great TUI framework |
| `blessed` / `ink` | Node.js | Okay |

**Recommendation: C# + Spectre.Console** — it has everything you described out of the box and runs natively on Windows/PowerShell.

---

## Proposed Architecture

```text
PhellamsManager.exe  (single .exe, self-contained)
├── Config: $HOME\.phellams\simplerr\config.json
├── WSL Controls     → wsl.exe process calls
├── Docker Client    → Docker Engine API (via named pipe) or docker.exe
└── Spectre.Console  → Live dashboard rendering
```

---

## Suggested Config File

```json
{
  "wslDistroName": "Ubuntu-Media",
  "composePath": "\\\\wsl$\\Ubuntu-Media\\home\\user\\media-stack",
  "refreshIntervalSeconds": 5,
  "services": [
    { "name": "plex",        "icon": "📺" },
    { "name": "sonarr",      "icon": "📡" },
    { "name": "radarr",      "icon": "🎬" },
    { "name": "qbittorrent", "icon": "🔽" },
    { "name": "prowlarr",    "icon": "🔍" }
  ]
}
```

---

## UI Layout Concept

```text
╔══════════════════════════════════════════════════════╗
║           🎬  Phellams Media Manager                 ║
╠══════════════════════════════════════════════════════╣
║  WSL:  ● Running (Ubuntu-Media)                      ║
║  [ Start WSL ]  [ Stop WSL ]  [ Restart WSL ]        ║
╠══════════════════════════════════════════════════════╣
║  CONTAINERS                          CPU    MEM      ║
║  ● plex         Running  📺    [▓▓▓░░] 12%  [▓▓░░] 1.2GB ║
║  ● sonarr       Running  📡    [▓░░░░]  4%  [▓░░░] 300MB ║
║  ● radarr       Running  🎬    [▓░░░░]  3%  [▓░░░] 280MB ║
║  ✕ qbittorrent  Stopped  🔽                           ║
║  ● prowlarr     Running  🔍    [░░░░░]  1%  [░░░░] 120MB ║
╠══════════════════════════════════════════════════════╣
║  Selected: plex                                      ║
║  [ Start ]  [ Stop ]  [ Restart ]  [ View Logs ]     ║
╚══════════════════════════════════════════════════════╝
```

---

## Phased Build Plan

### Phase 1 — MVP (WSL controls + container status)
- Read config file
- WSL start/stop/restart buttons
- Container list with status (polling docker ps)
- Basic keyboard navigation

### Phase 2 — Stats
- Staggered `docker stats` polling (one container at a time, rotating)
- CPU/Memory gauges via Spectre.Console `BreakdownChart` or progress bars
- Configurable refresh interval

### Phase 3 — Container Actions
- Per-container start/stop/restart
- Log viewer (scrollable panel, tail -n 100 docker logs)
- Log filtering by keyword

---

## Key Technical Notes

- **Staggered stats** — smart call, `docker stats --no-stream` on all containers simultaneously is heavy. Rotating through them one-by-one every second is the right approach
- **WSL detection** — can poll `wsl --list --running` to show live WSL state
- **Docker comms** — can either shell out to `docker.exe` (simpler) or use the Docker Engine API via named pipe `//./pipe/docker_engine` (faster, no process spawning)
- **Self-contained exe** — publish as `dotnet publish -r win-x64 --self-contained` so the user just double-clicks it

## Technology Decisions

### .NET Version — Go .NET 8

Stick with **.NET 8** for now. Here's why:

- .NET 10 isn't released yet (Nov 2025... but you're in March 2026 so it *is* out)
- However **Spectre.Console AOT support is still maturing** — trimming + AOT with reflection-heavy TUI libs causes headaches
- .NET 8 is LTS, AOT works well for simple console apps
- You can migrate to .NET 10 AOT later once Spectre.Console stabilises

> **Honest take:** AOT saves ~50-100ms startup. For a TUI dashboard that runs for hours, it's not worth the compatibility pain right now. Ship it working first.

---

### Input — Keyboard Only

**Pick keyboard only.** Here's why:

```text
Mouse in TUI = pain
  ├── Windows Terminal mouse support is inconsistent
  ├── Spectre.Console has no mouse API
  ├── Terminal.Gui has mouse but it's clunky
  └── Users accidentally click through windows beneath
```

**Keyboard nav is faster anyway** once the user learns 3-4 keys:

```text
  Tab / Arrow keys  → navigate
  Enter             → confirm action
  S / T / R         → Start / sTop / Restart (context aware)
  L                 → Logs
  Q                 → Quit
  ?                 → Help overlay
```

Since your target user isn't IT-savvy, show a **persistent key hint bar** at the bottom — they never need to remember anything.

```text
╔═══════════════════════════════════════════════╗
║  [↑↓] Navigate  [S]tart  [T] Stop  [R]estart  ║
║  [L]ogs         [Q]uit   [?] Help             ║
╚═══════════════════════════════════════════════╝
```

---

### Docker Comms — Shell out to `docker.exe`

Since Docker is **native inside WSL2**, the Windows `docker.exe` actually proxies through to the WSL2 instance anyway. Simplest path:

```text
Option A: shell out to docker.exe (Windows side)
  ✅ Simple, no dependencies
  ✅ Works if Docker Desktop is installed OR if wsl.exe exposes docker
  ❌ Process spawn overhead (~30ms per call, fine for staggered polling)

Option B: Docker Engine API via WSL2 socket
  ✅ Fast, no process spawning
  ✅ Proper typed responses
  ❌ Need to proxy the Unix socket from WSL2 → Windows (npiperelay or socat)
  ❌ More setup for the user
```

**Recommendation: Start with `docker.exe` shell calls, abstract it behind an interface so you can swap to the API later.**

```csharp
public interface IDockerProvider
{
    Task<List<ContainerInfo>> GetContainersAsync();
    Task<ContainerStats> GetStatsAsync(string containerId);
    Task StartContainerAsync(string containerId);
    Task StopContainerAsync(string containerId);
    Task<IAsyncEnumerable<string>> GetLogsAsync(string containerId, int tail = 100);
}
```

---

### Confirmed Stack

```text
Language:     C#
Runtime:      .NET 8 (LTS)
AOT:          No for now, revisit on .NET 10
TUI Library:  Spectre.Console (Live display + Layout)
Input:        Keyboard only
Docker:       docker.exe shell calls behind abstraction layer
Config:       $HOME\.phellams\simplerr\config.json
Distribution: Single self-contained .exe (win-x64)
```