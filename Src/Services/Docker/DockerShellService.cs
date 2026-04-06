// src/Services/Docker/DockerShellService.cs
using System.Globalization;
using System.Text.RegularExpressions;
using Simplerr.Infrastructure;
using Simplerr.Services.Docker.Models;

namespace Simplerr.Services.Docker;

public partial class DockerShellService : IDockerService
{
    private readonly string _dockerCmd;
    private readonly string[] _dockerArgs;

    public DockerShellService(string command = "docker")
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        _dockerCmd = parts[0];
        _dockerArgs = parts.Skip(1).ToArray();
    }

    private string[] BuildArgs(params string[] args) =>
        [.. _dockerArgs, .. args];

    private const string PsFormat =
        "\"{{.ID}}|{{.Names}}|{{.Image}}|{{.Status}}|{{.State}}|{{.Labels}}\"";

    private const string StatsFormat =
        "\"{{.CPUPerc}}|{{.MemUsage}}\"";

    public async Task<List<ContainerInfo>> GetContainersAsync()
    {
        var result = await Task.Run(() =>
            ProcessRunner.Run(
                _dockerCmd,
                BuildArgs("ps", "-a", "--format", PsFormat)
            )
        );

        if (result.ExitCode != 0)
        {
            throw new Exception($"{_dockerCmd} ps failed (code {result.ExitCode}): {result.Error}");
        }

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseContainerLine)
            .Where(c => c is not null)
            .Cast<ContainerInfo>()
            .ToList();
    }

    public async Task<ContainerStats?> GetStatsAsync(string containerId)
    {
        var result = await Task.Run(() =>
            ProcessRunner.Run(
                _dockerCmd,
                BuildArgs("stats", containerId, "--no-stream", "--format", StatsFormat),
                timeoutMs: 5000
            )
        );

        if (result.ExitCode != 0) return null;

        var line = result.Output.Trim();
        return ParseStats(containerId, line);
    }

    public async Task StartContainerAsync(string containerId)
    {
        await Task.Run(() =>
            ProcessRunner.Run(_dockerCmd, BuildArgs("start", containerId))
        );
    }

    public async Task StopContainerAsync(string containerId)
    {
        await Task.Run(() =>
            ProcessRunner.Run(_dockerCmd, BuildArgs("stop", containerId))
        );
    }

    public async Task RestartContainerAsync(string containerId)
    {
        await Task.Run(() =>
            ProcessRunner.Run(_dockerCmd, BuildArgs("restart", containerId))
        );
    }

    public IEnumerable<string> TailLogs(
        string containerId,
        int tail,
        CancellationToken ct
    ) =>
        ProcessRunner.StreamLines(
            _dockerCmd,
            BuildArgs("logs", "--tail", tail.ToString(), "--follow", containerId),
            ct
        );

    // ── Parsers ───────────────────────────────────────────────────────────

    private static ContainerInfo? ParseContainerLine(string line)
    {
        var parts = line.Split('|', 6);
        if (parts.Length < 5) return null;

        var id = parts[0].Trim().Trim('\"');
        var name = parts[1].Trim().TrimStart('/');
        var image = parts[2].Trim();
        var status = parts[3].Trim();
        var isRunning = parts[4].Trim().Equals("running", StringComparison.OrdinalIgnoreCase);
        
        var labelsStr = parts.Length > 5 ? parts[5].Trim().Trim('\"') : "";
        var labels = ParseLabels(labelsStr);

        return new ContainerInfo(
            Id: id,
            Name: name,
            Image: image,
            Status: status,
            IsRunning: isRunning,
            Labels: labels
        );
    }

    private static Dictionary<string, string> ParseLabels(string labelsStr)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(labelsStr)) return dict;

        var pairs = labelsStr.Split(',');
        foreach (var pair in pairs)
        {
            var idx = pair.IndexOf('=');
            if (idx > 0)
            {
                var key = pair[..idx].Trim();
                var val = pair[(idx + 1)..].Trim();
                dict[key] = val;
            }
        }

        return dict;
    }

    private static ContainerStats? ParseStats(string id, string line)
    {
        var parts = line.Trim('\"').Split('|');
        if (parts.Length < 2) return null;

        var cpu = ParsePercent(parts[0]);
        var (used, limit) = ParseMemory(parts[1]);

        return new ContainerStats(id, cpu, used, limit);
    }

    private static double ParsePercent(string s)
    {
        var clean = s.Trim().TrimEnd('%');
        return double.TryParse(clean, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static (long Used, long Limit) ParseMemory(string s)
    {
        var parts = s.Split('/');
        if (parts.Length < 2) return (0, 0);
        return (
            ParseMemoryValue(parts[0].Trim()),
            ParseMemoryValue(parts[1].Trim())
        );
    }

    private static long ParseMemoryValue(string s)
    {
        var match = MemoryRegex().Match(s);
        if (!match.Success) return 0;

        var value = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return match.Groups[2].Value.ToUpperInvariant() switch
        {
            "GIB" or "GB" => (long)(value * 1_073_741_824),
            "MIB" or "MB" => (long)(value * 1_048_576),
            "KIB" or "KB" => (long)(value * 1_024),
            _ => (long)value,
        };
    }

    [GeneratedRegex(@"([\d.]+)\s*(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex MemoryRegex();
}