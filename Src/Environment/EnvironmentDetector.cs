// src/Environment/EnvironmentDetector.cs
using Simplerr.Infrastructure;

namespace Simplerr.Environment;

public enum RuntimeEnvironment
{
    WindowsWithWsl,
    WindowsOnly,
    Linux,
    MacOs,
}

public static class EnvironmentDetector
{
    public static RuntimeEnvironment Detect()
    {
        if (OperatingSystem.IsLinux()) return RuntimeEnvironment.Linux;
        if (OperatingSystem.IsMacOS()) return RuntimeEnvironment.MacOs;

        if (OperatingSystem.IsWindows())
        {
            return IsWslAvailable()
                ? RuntimeEnvironment.WindowsWithWsl
                : RuntimeEnvironment.WindowsOnly;
        }

        return RuntimeEnvironment.Linux;
    }

    private static bool IsWslAvailable()
    {
        try
        {
            var result = ProcessRunner.Run("wsl", "--list --quiet", timeoutMs: 3000);
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output);
        }
        catch
        {
            return false;
        }
    }
}