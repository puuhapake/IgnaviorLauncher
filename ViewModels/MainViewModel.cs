using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IgnaviorLauncher.Services;

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System;
using SharpCompress.Archives;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace IgnaviorLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ManifestService manifest;
    private readonly LocalGameService gameService;

    [ObservableProperty]
    private ObservableCollection<GameViewModel> games;

    private GameViewModel selectedGame;
    public GameViewModel SelectedGame
    {
        get => selectedGame;
        set
        {
            if (SetProperty(ref selectedGame, value) && value != null)
            {
                SelectGameCommand.Execute(value);
            }
        }
    }

    private string GetGameLibraryPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Ignavior");
    }

    public MainViewModel()
    {
        manifest = new();
        // WARN: Hard-coded to %UserPath%/.Ignavior/
        string path = GetGameLibraryPath();
        gameService = new(path);

        Games = new ObservableCollection<GameViewModel>();
        _ = Async();
    }

    private async Task Async()
    {
        var manifest = await this.manifest.FetchManifestAsync();
        if (manifest == null)
        {
            // throw?
            return;
        }

        var games = gameService.GetInstalledGameIds().ToDictionary(id => id);
        var models = new ObservableCollection<GameViewModel>();

        foreach (var pair in manifest.Games)
        {
            string id = pair.Key;
            var game = pair.Value;

            string version = null;
            if (games.ContainsKey(id))
            {
                var info = gameService.GetGameInfo(id);
                version = info?.InstalledVersion;
            }

            string buttonText = "Install";
            if (version != null)
            {
                bool latest = version == game.LatestVersion;
                buttonText = latest ? "Play" : "Update";
            }

            var model = new GameViewModel
            {
                Name = game.Name,
                InstalledVersion = version ?? "",
                TextState = buttonText,

                LastPlayed = games.ContainsKey(id) ? 
                gameService.GetGameInfo(id)?.LastPlayed ?? 
                DateTime.MinValue : DateTime.MinValue
            };

            manifestMap[id] = game;
            models.Add(model);
        }

        Games = [.. models.OrderByDescending(g => g.LastPlayed)];
        SelectedGame = Games.FirstOrDefault();
    }

    private readonly Dictionary<string, GameManifest> manifestMap = new();

    private async void LoadChangelogForGame(GameViewModel game)
    {
        var entry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
        if (entry.Key == null)
            return;

        string id = entry.Key;
        var manifest = entry.Value;

        game.PatchNotes.Clear();
        string changelogUrl = "https://raw.githubusercontent.com/artmedland/IgnaviorLauncher_files/main/changelogs";
        var versions = new List<string>();

        if (manifest.Base != null)
            versions.Add(manifest.Base.Version);
        foreach (var patch in manifest.Patches)
        {
            if (!versions.Contains(patch.OldVersion))
                versions.Add(patch.OldVersion);
            if (!versions.Contains(patch.NewVersion))
                versions.Add(patch.NewVersion);
        }
        versions = versions.Distinct().OrderByDescending(v => v).ToList();

        foreach (var version in versions)
        {
            string url = $"{changelogUrl}{id}/{version}.md";
            try
            {
                using var client = new System.Net.Http.HttpClient();
                string markdown = await client.GetStringAsync(url);
                game.PatchNotes.Add(new PatchNoteViewModel
                {
                    Version = version,
                    MarkdownContent = markdown,
                    ReleaseDate = DateTime.MinValue
                });
            } 
            catch
            {
                game.PatchNotes.Add(new PatchNoteViewModel
                {
                    Version = version,
                    MarkdownContent = $"#{version}\n",
                    ReleaseDate = DateTime.MinValue
                });
            }
        }
    }

    [RelayCommand]
    private void PrimaryAction(GameViewModel game)
    {
        if (game == null)
            return;

        switch (game.TextState)
        {
            case "Install":
                InstallGame(game);
                break;
            case "Update":
                UpdateGame(game);
                break;
            case "Play":
                PlayGame(game);
                break;
        }
    }

    private async void InstallGame(GameViewModel game)
    {
        var gameEntry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
        if (gameEntry.Key == null)
            return;

        string id = gameEntry.Key;
        var manifest = gameEntry.Value;

        string library = GetGameLibraryPath();
        string gamedir = Path.Combine(library, id);
        Directory.CreateDirectory(gamedir);

        DownloadService downloader = new();
        string temp = Path.Combine(Path.GetTempPath(), "IGNAV_LauncherDL");
        string rarPath = await downloader.DownloadFileAsync(manifest.Base.Url, temp);

        using var archive = SharpCompress.Archives.Rar.RarArchive.OpenArchive(rarPath);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            entry.WriteToDirectory(gamedir, new SharpCompress.Common.ExtractionOptions()
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }

        File.Delete(rarPath);
        gameService.SaveGameInfo(new LocalGameInfo()
        {
            Id = id,
            InstalledVersion = manifest.Base.Version,
            LastPlayed = DateTime.Now
        });

        game.InstalledVersion = manifest.Base.Version;
        game.TextState = manifest.Base.Version == manifest.LatestVersion ? "Play" : "Update";
    }

    private async void UpdateGame(GameViewModel game)
    {
        var gameEntry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
        if (gameEntry.Key == null)
            return;

        string id = gameEntry.Key;
        var manifest = gameEntry.Value;

        string library = GetGameLibraryPath();
        string gamedir = Path.Combine(library, id);

        var patches = new List<PatchInfo>();
        string version = game.InstalledVersion;

        // assumes patches in order!
        while (version != manifest.LatestVersion)
        {
            var next = manifest.Patches.FirstOrDefault(p => p.OldVersion == version);
            if (next == null)
                throw new Exception($"No patch found from {version} to next version!");
            patches.Add(next);
            version = next.NewVersion;
        }

        var downloader = new DownloadService();
        string temp = Path.Combine(Path.GetTempPath(), "IGNAV_LauncherDL");

        foreach (var patch in patches)
        {
            string rar = await downloader.DownloadFileAsync(patch.Url, temp);
            ApplyPatchPackage(rar, gamedir);
            File.Delete(rar);
        }

        var info = gameService.GetGameInfo(id);
        info.InstalledVersion = manifest.LatestVersion;
        gameService.SaveGameInfo(info);

        game.InstalledVersion = manifest.LatestVersion;
        game.TextState = "Play";
    }

    private void PlayGame(GameViewModel game)
    {
        var entry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
        
        if (entry.Key != null)
        {
            gameService.UpdateLastPlayed(entry.Key);
        }
        game.LastPlayed = DateTime.Now;

        var reordered = Games.OrderByDescending(g => g.LastPlayed).ToList();
        Games.Clear();

        foreach (var g in reordered)
        {
            Games.Add(g);
        }
        SelectedGame = game;
    }

    private void ApplyPatchPackage(string rar, string dir)
    {
        string temp = Path.Combine(Path.GetTempPath(), "IGNAVPatcherTool", Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);

        using var archive = SharpCompress.Archives.Rar.RarArchive.OpenArchive(rar);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            entry.WriteToDirectory(temp, new SharpCompress.Common.ExtractionOptions()
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
        string manifest = Path.Combine(temp, "manifest.json");
        if (!File.Exists(manifest))
        {
            throw new Exception("Patch manifest not found in package.");
        }

        var patch = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(manifest));
        foreach (var deletedFile in patch.deleted_files)
        {
            string fullPath = Path.Combine(dir, deletedFile);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        foreach (var newFile in patch.new_files)
        {
            string source = Path.Combine(temp, newFile);
            string dest = Path.Combine(dir, newFile);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(source, dest, overwrite: true);
        }

        string xdelta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xdelta3.exe");
        foreach (var patchEntry in patch.patches)
        {
            string target = Path.Combine(dir, patchEntry.file);
            string patcher = Path.Combine(temp, patchEntry.patch);
            string output = target + " (patch)";

            var process = new System.Diagnostics.Process()
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = xdelta,
                    Arguments = $"-d -s \"{target}\" \"{patcher}\" \"{output}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new Exception($"Patch {patchEntry.file} (xdelta3) failed: {error}");
            }
            File.Delete(target);
            File.Move(temp, target);
        }
        Directory.Delete(temp, true);
    }

    private class PatchManifest
    {
        public List<PatchEntry> patches { get; set; } = new();
        public List<string> new_files { get; set; } = new();
        public List<string> deleted_files { get; set; } = new();
    }

    private class PatchEntry
    {
        public string file { get; set; }
        public string patch { get; set; }
    }

    [Obsolete]
    private void LoadDummyData()
    {
        var games = new ObservableCollection<GameViewModel>();

        var game1 = new GameViewModel
        {
            Name = "Alpha Game",
            InstalledVersion = "v1.2.3",
            TextState = "Play"
        };
        game1.PatchNotes.Add(new PatchNoteViewModel
        {
            Version = "1.2.3",
            ReleaseDate = new DateTime(2026, 3, 14),
            MarkdownContent = @"## **New Features**
- Added support for new control system
- Improved rendering performance

### **Bugs**
- Fixed bugs"
        });
        game1.PatchNotes.Add(new PatchNoteViewModel
        {
            Version = "1.2.2",
            ReleaseDate = new DateTime(2026, 3, 13),
            MarkdownContent = @"- Nothing interesting"
        });
        game1.PatchNotes.Add(new PatchNoteViewModel()
        {
            Version = "1.2",
            ReleaseDate = new DateTime(2026, 1, 23),
            MarkdownContent = @"## Bugs
- Added new bugs
- Fixed none

### New Features
- Major new content
- Patched game-breaking glitch
- New backend for input management
- New version control system
- Removed all old saves"
        });

        var game2 = new GameViewModel
        {
            Name = "Beta Game",
            InstalledVersion = "",
            TextState = "Install"
        };
        game2.PatchNotes.Add(new PatchNoteViewModel
        {
            Version = "v0.9",
            ReleaseDate = new DateTime(2023, 9, 2),
            MarkdownContent = @"## New Features
- Added new level, _The Backrooms_
- Beta release"
        });

        games.Add(game1);
        games.Add(game2);

        Games = [.. games.OrderByDescending(g => g.LastPlayed)];
        SelectedGame = Games.FirstOrDefault();
    }

    [RelayCommand]
    private void SelectGame(GameViewModel game)
    {
        if (game == null)
            return;

        SelectedGame = Games.FirstOrDefault(g => g == game);
    }
}