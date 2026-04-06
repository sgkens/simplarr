// src/Services/Docker/Models/ContainerInfo.cs
namespace Simplerr.Services.Docker.Models;

public record ContainerInfo(
    string Id,
    string Name,
    string Image,
    string Status,
    bool IsRunning,
    Dictionary<string, string> Labels
);