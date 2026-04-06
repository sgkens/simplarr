// src/config/SimplerrConfig.cs
namespace Simplerr.Config;

public class SimplerrConfig
{
    public WslConfig Wsl { get; set; } = new();
    public DockerConfig Docker { get; set; } = new();
    public List<ServiceConfig> Services { get; set; } = [];
}

public class WslConfig
{
    public bool Enabled { get; set; } = false;
    public string DistroName { get; set; } = string.Empty;
}

public class DockerConfig
{
    public string ComposePath { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 5;
    public int StatsPollIntervalSeconds { get; set; } = 3;
    public bool ShowAllContainers { get; set; } = false;
}

public class ServiceConfig
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "■";
    public string FriendlyName { get; set; } = string.Empty;
}