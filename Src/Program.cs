// src/Program.cs
using Simplerr.Config;
using Simplerr.Environment;
using Simplerr.Services.Docker;
using Simplerr.Services.Wsl;
using Simplerr.State;
using Simplerr.Ui;
using Spectre.Console;

var config = ConfigLoader.Load();
var env = EnvironmentDetector.Detect();

IWslService wsl = env == RuntimeEnvironment.WindowsWithWsl && config.Wsl.Enabled
    ? new WslService()
    : new NullWslService();

var dockerCommand = "docker";
if (env == RuntimeEnvironment.WindowsWithWsl && config.Wsl.Enabled && !string.IsNullOrWhiteSpace(config.Wsl.DistroName))
{
    dockerCommand = $"wsl -d {config.Wsl.DistroName} docker";
}

IDockerService docker = new DockerShellService(dockerCommand);
var state = new AppState();

var dashboard = new Dashboard(state, config, wsl, docker, env);

Console.CursorVisible = false;
Console.Title = "Simplerr";

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await dashboard.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // clean exit
}
finally
{
    Console.CursorVisible = true;
    AnsiConsole.Clear();
    AnsiConsole.MarkupLine("[grey]Simplerr exited.[/]");
}