// src/Infrastructure/ProcessRunner.cs
using System.Diagnostics;

namespace Simplerr.Infrastructure;

public record ProcessResult(int ExitCode, string Output, string Error);

public static class ProcessRunner
{
    public static ProcessResult Run(
        string fileName,
        string[] arguments,
        int timeoutMs = 10_000
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };

        process.Start();

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        var completed = process.WaitForExit(timeoutMs);

        if (!completed)
        {
            process.Kill();
            return new ProcessResult(-1, output, "Process timed out");
        }

        return new ProcessResult(process.ExitCode, output, error);
    }

    // Overload for simple space-separated args (no format strings)
    public static ProcessResult Run(
        string fileName,
        string arguments,
        int timeoutMs = 10_000
    ) => Run(fileName, arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries), timeoutMs);

    public static IEnumerable<string> StreamLines(
        string fileName,
        string[] arguments,
        CancellationToken ct
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        while (!process.StandardOutput.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = process.StandardOutput.ReadLine();
            if (line is not null)
                yield return line;
        }

        process.WaitForExit();
    }

    public static IEnumerable<string> StreamLines(
        string fileName,
        string arguments,
        CancellationToken ct
    ) => StreamLines(
        fileName,
        arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries),
        ct
    );
}