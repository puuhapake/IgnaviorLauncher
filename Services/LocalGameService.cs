using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace IgnaviorLauncher.Services;

public class LocalGameInfo
{
    public string Id { get; set; }
    public string InstalledVersion { get; set; }
    public DateTime LastPlayed { get; set; }
    public string GameRoot { get; set; }
}

public class LocalGameService
{
    private readonly string path;

    public LocalGameService(string path)
    {
        this.path = path;
        Directory.CreateDirectory(path);
    }

    public LocalGameInfo GetGameInfo(string id)
    {
        string dir = Path.Combine(path, id);
        string info = Path.Combine(dir, "gameinfo.json");

        if (!File.Exists(info))
            return null;

        var json = File.ReadAllText(info);
        return JsonSerializer.Deserialize<LocalGameInfo>(json);
    }

    public void SaveGameInfo(LocalGameInfo info)
    {
        string folder = Path.Combine(path, info.Id);
        Directory.CreateDirectory(folder);
        
        string file = Path.Combine(folder, "gameinfo.json");
        var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(file, json);
    }

    public void UpdateLastPlayed(string id)
    {
        var info = GetGameInfo(id);
        if (info == null)
            return;

        info.LastPlayed = DateTime.Now;
        SaveGameInfo(info);
    }

    public IEnumerable<string> GetInstalledGameIds()
    {
        foreach (var dir in Directory.GetDirectories(path))
        {
            string id = Path.GetFileName(dir);

            if (File.Exists(Path.Combine(dir, "gameinfo.json")))
            {
                yield return id;
            }
        }
    }
}
