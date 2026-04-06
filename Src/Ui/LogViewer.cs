// src/Ui/LogViewer.cs
using Simplerr.Services.Docker;
using Simplerr.Services.Docker.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Simplerr.Ui;

public class LogViewer
{
    private readonly IDockerService _docker;
    private readonly ContainerInfo _container;
    private readonly string _friendlyName;
    private readonly List<string> _lines = [];
    private string _filter = string.Empty;
    private bool _isInputting = false;

    public LogViewer(
        IDockerService docker,
        ContainerInfo container,
        string friendlyName
    )
    {
        _docker = docker;
        _container = container;
        _friendlyName = friendlyName;
    }

    public async Task ShowAsync(CancellationToken ct)
    {
        AnsiConsole.Clear();

        using var logCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var logTask = Task.Run(() =>
        {
            foreach (var line in _docker.TailLogs(_container.Id, 100, logCts.Token))
            {
                lock (_lines)
                {
                    _lines.Add(line);
                    if (_lines.Count > 500)
                        _lines.RemoveAt(0);
                }
            }
        }, logCts.Token);

        await AnsiConsole
            .Live(RenderLogPanel())
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var renderTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

                var renderTask = Task.Run(async () =>
                {
                    while (!logCts.IsCancellationRequested)
                    {
                        await renderTimer.WaitForNextTickAsync(logCts.Token);
                        if (!_isInputting)
                            ctx.UpdateTarget(RenderLogPanel());
                    }
                }, logCts.Token);

                await Task.Run(() =>
                {
                    while (!logCts.IsCancellationRequested)
                    {
                        if (!Console.KeyAvailable)
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        var key = Console.ReadKey(intercept: true);

                        if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Q)
                        {
                            logCts.Cancel();
                        }
                        else if (key.Key == ConsoleKey.F && key.Modifiers == 0)
                        {
                            _isInputting = true;
                            // Move cursor to bottom and prompt
                            AnsiConsole.Cursor.MoveDown(35);
                            _filter = AnsiConsole.Ask<string>("[bold blue]Filter:[/] ");
                            _isInputting = false;
                            AnsiConsole.Clear(); // Refresh after input
                        }
                        else if (key.Key == ConsoleKey.C)
                        {
                            lock (_lines) _filter = string.Empty;
                        }
                    }
                }, logCts.Token);

                await renderTask;
            });

        AnsiConsole.Clear();
    }

    private IRenderable RenderLogPanel()
    {
        List<string> filtered;

        lock (_lines)
        {
            filtered = string.IsNullOrEmpty(_filter)
                ? [.. _lines]
                : _lines
                    .Where(l => l.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        var visibleLines = filtered.TakeLast(30).ToList();

        var content = visibleLines.Count > 0
            ? string.Join("\n", visibleLines.Select(Markup.Escape))
            : "[grey]Waiting for logs...[/]";

        var filterLabel = string.IsNullOrEmpty(_filter)
            ? "[grey]none[/]"
            : $"[yellow]{Markup.Escape(_filter)}[/]";

        return new Rows(
            new Panel(new Markup(content))
            {
                Header = new PanelHeader(
                    $" Logs — {Markup.Escape(_friendlyName)} "
                ),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("blue"),
            },
            new Markup(
                $"Filter: {filterLabel}   " +
                "[grey][F] Set filter   [C] Clear filter   [Q/Esc] Back[/]"
            )
        );
    }
}