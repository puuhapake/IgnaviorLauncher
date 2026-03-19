using System.Text.Json;
using System.IO;

namespace IgnaviorLauncher.Services;

public class Settings
{
    public string? LibraryPath { get; set; }
    public byte[]? Secret { get; set; }
}

public class SettingsService
{
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    private readonly string settingsPath;

    public SettingsService()
    {
        string appData = ResourceService.LocalAppDirectory;
        Directory.CreateDirectory(Path.Combine(appData));
        settingsPath = Path.Combine(appData, "settings.json");
    }

    public Settings Load()
    {
        if (!File.Exists(settingsPath))
        {
            return new Settings();
        }

        var json = File.ReadAllText(settingsPath);
        return JsonSerializer.Deserialize<Settings>(json) 
            ?? new Settings();
    }

    public void Save(Settings settings)
    {
        string json = JsonSerializer.Serialize(settings, jsonOptions);
        File.WriteAllText(settingsPath, json);
    }
}