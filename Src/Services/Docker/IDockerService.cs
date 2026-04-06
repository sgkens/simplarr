// src/Services/Docker/IDockerService.cs
using Simplerr.Services.Docker.Models;

namespace Simplerr.Services.Docker;

public interface IDockerService
{
    Task<List<ContainerInfo>> GetContainersAsync();
    Task<ContainerStats?> GetStatsAsync(string containerId);
    Task StartContainerAsync(string containerId);
    Task StopContainerAsync(string containerId);
    Task RestartContainerAsync(string containerId);
    IEnumerable<string> TailLogs(string containerId, int tail, CancellationToken ct);
}