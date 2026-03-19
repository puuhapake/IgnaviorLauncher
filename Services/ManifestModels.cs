using System.Text.Json.Serialization;

namespace IgnaviorLauncher.Services;

public class RootManifest
{
    [JsonPropertyName("games")]
    public Dictionary<string, GameManifest>? Games { get; set; }
}

public class GameManifest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("latest")]
    public string? LatestVersion { get; set; }

    [JsonPropertyName("versionNames")]
    public Dictionary<string, string>? VersionNames { get; set; } = [];

    [JsonPropertyName("base")]
    public BaseInfo? Base { get; set; }

    [JsonPropertyName("patches")]
    public List<PatchInfo>? Patches { get; set; }

    public Dictionary<string, string>? ChangelogUrls { get; set; }
}

public class BaseInfo
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("parts")]
    public List<PartInfo>? Parts { get; set; }
}

public class PartInfo
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    // add hash and size later
}

public class PatchInfo
{
    [JsonPropertyName("from")]
    public string? OldVersion { get; set; }

    [JsonPropertyName("to")]
    public string? NewVersion { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("parts")]
    public List<PartInfo>? Parts { get; set; }
}