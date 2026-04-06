// src/Services/Docker/Models/ContainerStats.cs
namespace Simplerr.Services.Docker.Models;

public record ContainerStats(
    string ContainerId,
    double CpuPercent,
    long MemoryUsedBytes,
    long MemoryLimitBytes
)
{
    public double MemoryPercent =>
        MemoryLimitBytes > 0
            ? (double)MemoryUsedBytes / MemoryLimitBytes * 100.0
            : 0;

    public string MemoryUsedFormatted => FormatBytes(MemoryUsedBytes);
    public string MemoryLimitFormatted => FormatBytes(MemoryLimitBytes);

    private static string FormatBytes(long bytes) =>
        bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1}GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F0}MB",
            >= 1_024 => $"{bytes / 1_024.0:F0}KB",
            _ => $"{bytes}B",
        };
}