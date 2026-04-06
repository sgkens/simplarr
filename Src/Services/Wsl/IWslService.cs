// src/Services/Wsl/IWslService.cs
namespace Simplerr.Services.Wsl;

public interface IWslService
{
    bool IsSupported { get; }
    WslStatus GetStatus(string distroName);
    Task StartAsync(string distroName);
    Task StopAsync();
    Task RestartAsync(string distroName);
}

public enum WslStatus
{
    Running,
    Stopped,
    Unknown,
}