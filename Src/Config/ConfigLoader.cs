// src/Config/ConfigLoader.cs
using System.Text.Json;

// Alias to avoid clash with our Simplerr.Environment namespace
using SysEnv = System.Environment;

namespace Simplerr.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string ConfigDir =>
        Path.Combine(
            SysEnv.GetFolderPath(SysEnv.SpecialFolder.UserProfile),
            ".phellams",
            "simplerr"
        );

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static SimplerrConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return CreateDefault();
        }

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<SimplerrConfig>(json, JsonOptions)
            ?? CreateDefault();
    }

    private static SimplerrConfig CreateDefault()
    {
        var config = new SimplerrConfig
        {
            Wsl = new WslConfig { Enabled = false, DistroName = "Ubuntu" },
            Docker = new DockerConfig
            {
                ComposePath = string.Empty,
                PollIntervalSeconds = 5,
                StatsPollIntervalSeconds = 3,
                ShowAllContainers = false,
            },
            Services =
            [
                new() { Name = "plex",        Icon = "📺", FriendlyName = "Plex Media Server" },
                new() { Name = "sonarr",      Icon = "📡", FriendlyName = "Sonarr (TV)"       },
                new() { Name = "radarr",      Icon = "🎬", FriendlyName = "Radarr (Movies)"   },
                new() { Name = "qbittorrent", Icon = "🔽", FriendlyName = "qBittorrent"       },
                new() { Name = "prowlarr",    Icon = "🔍", FriendlyName = "Prowlarr"          },
            ],
        };

        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(
            ConfigPath,
            JsonSerializer.Serialize(config, JsonOptions)
        );

        return config;
    }
}