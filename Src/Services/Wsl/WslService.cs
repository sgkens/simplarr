// src/Services/Wsl/WslService.cs
using Simplerr.Infrastructure;

namespace Simplerr.Services.Wsl;

public class WslService : IWslService
{
    public bool IsSupported => true;

    public WslStatus GetStatus(string distroName)
    {
        var result = ProcessRunner.Run(
            "wsl",
            ["--list", "--running", "--quiet"],
            timeoutMs: 3000
        );

        if (result.ExitCode != 0) return WslStatus.Unknown;

        // WSL --list outputs UTF-16LE on Windows which produces null bytes
        // when read as UTF-8. Strip them out before comparing.
        var lines = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l
                .Replace("\r", string.Empty)
                .Replace("\0", string.Empty)
                .Trim()
            )
            .Where(l => !string.IsNullOrWhiteSpace(l));

        return lines.Any(l =>
            l.Equals(distroName, StringComparison.OrdinalIgnoreCase)
        )
            ? WslStatus.Running
            : WslStatus.Stopped;
    }

    public async Task StartAsync(string distroName)
    {
        await Task.Run(() =>
            ProcessRunner.Run(
                "wsl",
                ["--distribution", distroName, "--exec", "sleep", "0"]
            )
        );
    }

    public async Task StopAsync()
    {
        await Task.Run(() => ProcessRunner.Run("wsl", ["--shutdown"]));
    }

    public async Task RestartAsync(string distroName)
    {
        await StopAsync();
        await Task.Delay(2000);
        await StartAsync(distroName);
    }
}