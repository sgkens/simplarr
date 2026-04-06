// src/Ui/Dashboard.cs
using Simplerr.Config;
using Simplerr.Environment;
using Simplerr.Services.Docker;
using Simplerr.Services.Docker.Models;
using Simplerr.Services.Wsl;
using Simplerr.State;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Simplerr.Ui;

public class Dashboard
{
    private readonly AppState _state;
    private readonly SimplerrConfig _config;
    private readonly IWslService _wsl;
    private readonly IDockerService _docker;
    private readonly RuntimeEnvironment _env;
    private CancellationTokenSource? _dashboardCts;
    private readonly AutoResetEvent _redrawEvent = new(false);

    public Dashboard(
        AppState state,
        SimplerrConfig config,
        IWslService wsl,
        IDockerService docker,
        RuntimeEnvironment env
    )
    {
        _state = state;
        _config = config;
        _wsl = wsl;
        _docker = docker;
        _env = env;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        AnsiConsole.Clear();

        // Start background pollers
        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var containerPoller = PollContainersAsync(pollCts.Token);
        var statsPoller = PollStatsAsync(pollCts.Token);
        var wslPoller = PollWslAsync(pollCts.Token);

        // View loop
        while (!pollCts.IsCancellationRequested)
        {
            // Input loop for Dashboard (blocks until exited or view changed)
            await InputLoopAsync(pollCts, ct);

            if (_state.RequestedLogContainer is not null)
            {
                var container = _state.RequestedLogContainer;
                _state.RequestedLogContainer = null;
                
                var viewer = new LogViewer(_docker, container, GetFriendlyName(container));
                await viewer.ShowAsync(pollCts.Token);
                
                // When viewer returns, clear console to resume dashboard
                AnsiConsole.Clear();
            }
            else if (!pollCts.IsCancellationRequested)
            {
                // If it exited without logs requested, it's either Q or some other reason
                break;
            }
        }

        await Task.WhenAll(containerPoller, statsPoller, wslPoller);
    }

    // ── Render ─────────────────────────────────────────────────────────────

    private Layout BuildLayout()
    {
        return new Layout("root")
            .SplitRows(
                new Layout("header").Size(3),
                new Layout("wsl").Size(_wsl.IsSupported ? 5 : 0),
                new Layout("global").Size(3),
                new Layout("containers"),
                new Layout("actions").Size(5),
                new Layout("hints").Size(3)
            );
    }

    private IRenderable RenderHeader() =>
        new Panel(
            new Markup(
                "[bold yellow] Simplerr[/]  [grey]Media Suite Manager[/]"
            )
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey"),
            Expand = true,
        };

    private IRenderable RenderGlobalStats()
    {
        var stats = _state.Stats.Values.ToList();
        var totalCpu = stats.Sum(s => s.CpuPercent);
        var totalMem = stats.Sum(s => s.MemoryUsedBytes);
        var totalLimit = stats.Sum(s => s.MemoryLimitBytes);

        var memLabel = totalLimit > 0
            ? $"[white]Memory:[/] {FormatBytes(totalMem)} / {FormatBytes(totalLimit)} ({(double)totalMem / totalLimit * 100:F1}%)"
            : "[white]Memory:[/] --";

        var cpuLabel = $"[white]CPU:[/] {totalCpu:F1}%";

        return new Panel(
            new Markup($"{cpuLabel}   {memLabel}")
        )
        {
            Header = new PanelHeader(" Global Stack Resources "),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("cyan"),
            Expand = true,
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double val = bytes;
        int unitIndex = 0;
        while (val >= 1024 && unitIndex < units.Length - 1)
        {
            val /= 1024;
            unitIndex++;
        }
        return $"{val:F1}{units[unitIndex]}";
    }

    private IRenderable RenderWslPanel()
    {
        if (!_wsl.IsSupported)
            return new Text(string.Empty);

        var (icon, color) = _state.WslStatus switch
        {
            WslStatus.Running => ("● ", "green"),
            WslStatus.Stopped => ("○ ", "red"),
            _ => ("? ", "yellow"),
        };

        var statusText = _state.WslStatus switch
        {
            WslStatus.Running =>
                $"[green]Running[/] — [grey]{Markup.Escape(_config.Wsl.DistroName)}[/]",
            WslStatus.Stopped => "[red]Stopped[/]",
            _ => "[yellow]Unknown[/]",
        };

        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(new Markup($"[{color}]{icon}[/]WSL: {statusText}"));
        grid.AddRow(
            new Markup(
                "[grey][[ [/][bold]F1[/][grey] ]] Start   " +
                "[[ [/][bold]F2[/][grey] ]] Stop   " +
                "[[ [/][bold]F3[/][grey] ]] Restart[/]"
            )
        );

        return new Panel(grid)
        {
            Header = new PanelHeader(" WSL "),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("blue"),
            Expand = true,
        };
    }

    private IRenderable RenderContainerTable()
    {
        var containers = _state.Containers;
        
        // Calculate available space
        var reservedHeight = 3 + (_wsl.IsSupported ? 5 : 0) + 3 + 5 + 3 + 4; // Panels + Table Header/Borders
        var terminalHeight = AnsiConsole.Console.Profile.Height;
        var pageSize = Math.Max(5, terminalHeight - reservedHeight);
        
        _state.UpdateScroll(pageSize);

        var table = new Table()
            .BorderColor(Color.Grey)
            .Border(TableBorder.Rounded)
            .Expand()
            .AddColumn(new TableColumn("[grey]#[/]").Width(4).RightAligned())
            .AddColumn(new TableColumn("[white]Service[/]").NoWrap())
            .AddColumn(new TableColumn("[white]Status[/]").Width(12))
            .AddColumn(new TableColumn("[white]CPU[/]").Width(22))
            .AddColumn(new TableColumn("[white]Memory[/]").Width(30));

        if (containers.Count == 0)
        {
            table.AddRow(
                new Text(string.Empty),
                new Markup("[grey]No containers found[/]"),
                new Text(string.Empty),
                new Text(string.Empty),
                new Text(string.Empty)
            );
            return new Panel(table)
            {
                Header = new PanelHeader(" Containers "),
                Border = BoxBorder.Rounded,
                Expand = true,
            };
        }

        var visibleCount = Math.Min(pageSize, containers.Count);
        for (var i = 0; i < visibleCount; i++)
        {
            var actualIndex = _state.ScrollOffset + i;
            if (actualIndex >= containers.Count) break;

            var c = containers[actualIndex];
            var isSelected = actualIndex == _state.SelectedIndex;
            var friendly = GetFriendlyName(c);

            var (statusIcon, statusColor) = c.IsRunning
                ? ("●", "green")
                : ("○", "red");

            var prefix = isSelected ? "[bold yellow]>[/] " : "  ";
            var nameDisplay = isSelected
                ? $"[bold yellow]{Markup.Escape(friendly.Trim())}[/]"
                : $"[white]{Markup.Escape(friendly.Trim())}[/]";

            var statusDisplay =
                $"[{statusColor}]{statusIcon} {(c.IsRunning ? "Running" : "Stopped")}[/]";

            _state.Stats.TryGetValue(c.Id, out var stats);

            var cpuBar = RenderBar(stats?.CpuPercent ?? 0, 100, 12);
            var memBar = RenderBar(stats?.MemoryPercent ?? 0, 100, 12);

            var cpuLabel = stats is not null
                ? $"{stats.CpuPercent:F1}%"
                : "[grey]--[/]";
            var memLabel = stats is not null
                ? $"{stats.MemoryUsedFormatted}/{stats.MemoryLimitFormatted}"
                : "[grey]--[/]";

            table.AddRow(
                new Markup($"{prefix}[grey]{actualIndex + 1}[/]".Trim()),
                new Markup(nameDisplay.Trim()),
                new Markup(statusDisplay.Trim()),
                new Markup($"{cpuBar} {cpuLabel}".Trim()),
                new Markup($"{memBar} {memLabel}".Trim())
            );
        }

        // Add scroll indicator if needed
        var headerText = " Containers ";
        if (containers.Count > pageSize)
        {
            headerText = $" Containers ({_state.ScrollOffset + 1}-{Math.Min(_state.ScrollOffset + pageSize, containers.Count)} of {containers.Count}) ";
        }

        return new Panel(table)
        {
            Header = new PanelHeader(headerText),
            Border = BoxBorder.Rounded,
            Expand = true,
        };
    }

    private IRenderable RenderActionsPanel()
    {
        var selected = _state.SelectedContainer;
        var status = _state.StatusMessage;

        var selectedLabel = selected is not null
            ? $"[bold white]{Markup.Escape(GetFriendlyName(selected))}[/]"
            : "[grey]None selected[/]";

        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(new Markup($"Selected: {selectedLabel}"));

        if (status is not null)
            grid.AddRow(new Markup($"[yellow]{Markup.Escape(status)}[/]"));

        grid.AddRow(
            new Markup(
                "[grey][[ [/][bold]S[/][grey] ]] Start   " +
                "[[ [/][bold]T[/][grey] ]] Stop   " +
                "[[ [/][bold]R[/][grey] ]] Restart   " +
                "[[ [/][bold]L[/][grey] ]] Logs[/]"
            )
        );

        return new Panel(grid)
        {
            Header = new PanelHeader(" Actions "),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("yellow"),
            Expand = true,
        };
    }

    private static IRenderable RenderHintsBar() =>
        new Markup(
            "[grey][[ [/][bold]↑[/][grey] ]][[↓]] Navigate   " +
            "[[F1]] WSL Start   [[F2]] WSL Stop   [[F3]] WSL Restart   " +
            "[[S]] Start   [[T]] Stop   [[R]] Restart   " +
            "[[L]] Logs   [[Q]] Quit[/]"
        );

    // ── Input Loop ─────────────────────────────────────────────────────────

    private async Task InputLoopAsync(
        CancellationTokenSource pollCts,
        CancellationToken ct
    )
    {
        _dashboardCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var dashCt = _dashboardCts.Token;

        await AnsiConsole
            .Live(BuildLiveDisplay())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                var renderTask = Task.Run(async () =>
                {
                    while (!dashCt.IsCancellationRequested)
                    {
                        // Wait for redraw event or periodic timeout (500ms)
                        WaitHandle.WaitAny([_redrawEvent, dashCt.WaitHandle], 500);
                        
                        ctx.UpdateTarget(BuildLiveDisplay());
                    }
                }, dashCt);

                // Input (blocking — run on thread pool)
                await Task.Run(() =>
                {
                    while (!dashCt.IsCancellationRequested)
                    {
                        if (!Console.KeyAvailable)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        var key = Console.ReadKey(intercept: true);
                        HandleKey(key, pollCts);
                        _redrawEvent.Set(); // Trigger immediate redraw
                    }
                }, dashCt);

                try { await renderTask; } catch (OperationCanceledException) {}
            });
    }

    private void HandleKey(ConsoleKeyInfo key, CancellationTokenSource pollCts)
    {
        // Global keys
        if (key.Key == ConsoleKey.Q)
        {
            pollCts.Cancel();
            _dashboardCts?.Cancel();
            return;
        }

        // Toggle help (handles '?' key or 'H')
        if (key.KeyChar == '?' || (key.Key == ConsoleKey.H && key.Modifiers == 0))
        {
            _state.ToggleHelp();
            return;
        }

        // If help is open, allow closing it with Escape
        if (_state.IsHelpOpen)
        {
            if (key.Key == ConsoleKey.Escape)
                _state.ToggleHelp();
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                _state.MoveSelectionUp();
                break;

            case ConsoleKey.DownArrow:
                _state.MoveSelectionDown();
                break;

            case ConsoleKey.F1 when _wsl.IsSupported:
                FireAndForget(() => WslActionAsync(WslAction.Start));
                break;

            case ConsoleKey.F2 when _wsl.IsSupported:
                FireAndForget(() => WslActionAsync(WslAction.Stop));
                break;

            case ConsoleKey.F3 when _wsl.IsSupported:
                FireAndForget(() => WslActionAsync(WslAction.Restart));
                break;

            case ConsoleKey.S when key.Modifiers == 0:
                FireAndForget(() => ContainerActionAsync(DockerAction.Start));
                break;

            case ConsoleKey.T when key.Modifiers == 0:
                FireAndForget(() => ContainerActionAsync(DockerAction.Stop));
                break;

            case ConsoleKey.R when key.Modifiers == 0:
                FireAndForget(() => ContainerActionAsync(DockerAction.Restart));
                break;

            case ConsoleKey.L when key.Modifiers == 0:
                var container = _state.SelectedContainer;
                if (container is not null)
                {
                    _state.RequestedLogContainer = container;
                    _dashboardCts?.Cancel();
                }
                break;
        }
    }

    // ── Actions ────────────────────────────────────────────────────────────

    private enum WslAction { Start, Stop, Restart }
    private enum DockerAction { Start, Stop, Restart }

    private async Task WslActionAsync(WslAction action)
    {
        if (_state.IsBusy || _state.IsHelpOpen) return;
        _state.SetBusy(true);

        var label = action.ToString();
        _state.SetStatus($"WSL: {label}ing...");

        try
        {
            await (action switch
            {
                WslAction.Start => _wsl.StartAsync(_config.Wsl.DistroName),
                WslAction.Stop => _wsl.StopAsync(),
                WslAction.Restart => _wsl.RestartAsync(_config.Wsl.DistroName),
                _ => Task.CompletedTask,
            });

            _state.SetStatus($"WSL {label} complete.");
        }
        catch (Exception ex)
        {
            _state.SetStatus($"[red]WSL error: {ex.Message}[/]");
        }
        finally
        {
            _state.SetBusy(false);
            await Task.Delay(3000);
            _state.SetStatus(null);
            _redrawEvent.Set();
        }
    }

    private async Task ContainerActionAsync(DockerAction action)
    {
        if (_state.IsBusy || _state.IsHelpOpen) return;

        var container = _state.SelectedContainer;
        if (container is null) return;

        _state.SetBusy(true);
        _state.SetStatus(
            $"{GetFriendlyName(container)}: {action}ing..."
        );

        try
        {
            await (action switch
            {
                DockerAction.Start =>
                    _docker.StartContainerAsync(container.Id),
                DockerAction.Stop =>
                    _docker.StopContainerAsync(container.Id),
                DockerAction.Restart =>
                    _docker.RestartContainerAsync(container.Id),
                _ => Task.CompletedTask,
            });

            _state.SetStatus(
                $"{GetFriendlyName(container)}: {action} complete."
            );
        }
        catch (Exception ex)
        {
            _state.SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            _state.SetBusy(false);
            await Task.Delay(3000);
            _state.SetStatus(null);
            _redrawEvent.Set();
        }
    }

    // ── Pollers ────────────────────────────────────────────────────────────

    private async Task PollContainersAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var allContainers = await _docker.GetContainersAsync();
                var filtered = allContainers;

                if (!_config.Docker.ShowAllContainers && _config.Services.Count > 0)
                {
                    filtered = allContainers
                        .Where(c => _config.Services.Any(s => IsMatch(c, s.Name)))
                        .OrderBy(c => GetFriendlyName(c))
                        .ToList();
                }
                else
                {
                    filtered = allContainers
                        .OrderBy(c => c.Name)
                        .ToList();
                }

                _state.UpdateContainers(filtered);
                _redrawEvent.Set();
            }
            catch (Exception ex)
            {
                _state.SetStatus($"[red]Docker poll error: {ex.Message}[/]");
                _redrawEvent.Set();
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_config.Docker.PollIntervalSeconds),
                ct
            ).ConfigureAwait(false);
        }
    }

    private bool IsMatch(ContainerInfo container, string serviceName)
    {
        // 1. Direct name match
        if (container.Name.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. Image name match
        if (container.Image.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
            return true;

        // 3. Docker Compose service label
        if (container.Labels.TryGetValue("com.docker.compose.service", out var svcLabel) &&
            svcLabel.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private async Task PollStatsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var containers = _state.Containers.Where(c => c.IsRunning).ToList();
            if (containers.Count == 0)
            {
                await Task.Delay(2000, ct).ConfigureAwait(false);
                continue;
            }

            // Poll all running containers in parallel
            var tasks = containers.Select(async container =>
            {
                try
                {
                    var stats = await _docker.GetStatsAsync(container.Id);
                    if (stats is not null)
                    {
                        _state.UpdateStats(container.Id, stats);
                        _redrawEvent.Set();
                    }
                }
                catch
                {
                    // swallow
                }
            });

            await Task.WhenAll(tasks);

            await Task.Delay(
                TimeSpan.FromSeconds(_config.Docker.StatsPollIntervalSeconds),
                ct
            ).ConfigureAwait(false);
        }
    }

    private async Task PollWslAsync(CancellationToken ct)
    {
        if (!_wsl.IsSupported) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var status = _wsl.GetStatus(_config.Wsl.DistroName);
                _state.UpdateWslStatus(status);
                _redrawEvent.Set();
            }
            catch
            {
                _state.UpdateWslStatus(WslStatus.Unknown);
                _redrawEvent.Set();
            }

            await Task.Delay(5000, ct).ConfigureAwait(false);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private IRenderable BuildLiveDisplay()
    {
        if (_state.IsHelpOpen)
            return RenderHelpOverlay();

        var layout = BuildLayout();

        layout["header"].Update(RenderHeader());

        if (_wsl.IsSupported)
            layout["wsl"].Update(RenderWslPanel());

        layout["global"].Update(RenderGlobalStats());
        layout["containers"].Update(RenderContainerTable());
        layout["actions"].Update(RenderActionsPanel());
        layout["hints"].Update(RenderHintsBar());

        return layout;
    }

    private IRenderable RenderHelpOverlay()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .AddColumn("Key")
            .AddColumn("Action");

        table.AddRow("[bold]Global[/]", "");
        table.AddRow("  ↑/↓", "Navigate container list");
        table.AddRow("  Q", "Quit Simplerr");
        table.AddRow("  ?", "Show/Hide this help overlay");
        table.AddEmptyRow();

        if (_wsl.IsSupported)
        {
            table.AddRow("[bold]WSL Controls[/]", "");
            table.AddRow("  F1", "Start WSL distro");
            table.AddRow("  F2", "Stop WSL (shutdown)");
            table.AddRow("  F3", "Restart WSL distro");
            table.AddEmptyRow();
        }

        table.AddRow("[bold]Container Actions[/]", "");
        table.AddRow("  S", "Start selected container");
        table.AddRow("  T", "Stop selected container");
        table.AddRow("  R", "Restart selected container");
        table.AddRow("  L", "Open log viewer");
        table.AddEmptyRow();

        table.AddRow("[bold]Log Viewer[/]", "");
        table.AddRow("  F", "Set filter string");
        table.AddRow("  C", "Clear filter");
        table.AddRow("  Q/Esc", "Return to dashboard");

        return new Align(
            new Panel(table)
            {
                Header = new PanelHeader(" [bold yellow]Simplerr Help[/] "),
                Padding = new Padding(2, 1, 2, 1),
            },
            HorizontalAlignment.Center,
            VerticalAlignment.Middle
        );
    }

    private string GetFriendlyName(ContainerInfo container)
    {
        var svc = _config.Services.FirstOrDefault(s => IsMatch(container, s.Name));

        if (svc is null) return container.Name;

        return string.IsNullOrWhiteSpace(svc.FriendlyName)
            ? $"{svc.Icon} {svc.Name}"
            : $"{svc.Icon} {svc.FriendlyName}";
    }

    private static string RenderBar(double value, double max, int width)
    {
        var filled = (int)Math.Round(value / max * width);
        filled = Math.Clamp(filled, 0, width);

        var color = value switch
        {
            > 80 => "red",
            > 50 => "yellow",
            _ => "green",
        };

        var bar = new string('█', filled) + new string('░', width - filled);
        return $"[{color}]{bar}[/]";
    }

    private static void FireAndForget(Func<Task> action)
    {
        Task.Run(async () =>
        {
            try { await action(); }
            catch { /* swallow */ }
        });
    }
}
