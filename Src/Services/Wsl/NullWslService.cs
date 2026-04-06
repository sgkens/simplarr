// src/Services/Wsl/NullWslService.cs
namespace Simplerr.Services.Wsl;

/// <summary>No-op implementation for Linux/macOS hosts.</summary>
public class NullWslService : IWslService
{
    public bool IsSupported => false;

    public WslStatus GetStatus(string distroName) => WslStatus.Unknown;

    public Task StartAsync(string distroName) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public Task RestartAsync(string distroName) => Task.CompletedTask;
}