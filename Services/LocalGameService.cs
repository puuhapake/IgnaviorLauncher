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
    public string DisplayVersion { get; set; }
    public DateTime LastPlayed { get; set; }
    public string GameRoot { get; set; }
}

public class LocalGameService
{
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    public string LibraryPath { get; }

    public LocalGameService(string path)
    {
        LibraryPath = path;
        Directory.CreateDirectory(path);
    }

    public LocalGameInfo GetGameInfo(string id)
    {
        string dir = Path.Combine(LibraryPath, id);
        string info = Path.Combine(dir, "gameinfo.json");

        if (!File.Exists(info))
            return null;

        var json = File.ReadAllText(info);
        return JsonSerializer.Deserialize<LocalGameInfo>(json);
    }

    public void SaveGameInfo(LocalGameInfo info)
    {
        string folder = Path.Combine(LibraryPath, info.Id);
        Directory.CreateDirectory(folder);
        
        string file = Path.Combine(folder, "gameinfo.json");
        var json = JsonSerializer.Serialize(info, jsonOptions);
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

    public void RemoveGameInfo(string id)
    {
        string dir = Path.Combine(LibraryPath, id);
        string info = Path.Combine(dir, "gameinfo.json");

        if (!File.Exists(info))
        {
            System.Diagnostics.Debug.WriteLine($"Tried to delete gameinfo for {id}, but could not find json");
            return;
        }
        File.Delete(info);
    }

    public IEnumerable<string> GetInstalledGameIds()
    {
        foreach (var dir in Directory.GetDirectories(LibraryPath))
        {
            string id = Path.GetFileName(dir);

            if (File.Exists(Path.Combine(dir, "gameinfo.json")))
            {
                yield return id;
            }
        }
    }
}
