using System.Collections.ObjectModel;
using System.Text.Json;
using System.IO;

using System.Diagnostics;
using System.Windows;
using System.Net.Http;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using SharpCompress.Archives.Rar;
using SharpCompress.Archives;
using SharpCompress.Common;

using Microsoft.VisualBasic;

namespace IgnaviorLauncher.ViewModels;
using Services;
using SharpCompress.Readers;

public partial class MainViewModel : ObservableObject
{
    private readonly ManifestService manifestService;
    private readonly SettingsService settingsService;
    private readonly LocalGameService gameService;

    [ObservableProperty]
    private ObservableCollection<GameViewModel> games;

    [ObservableProperty]
    private double changelogScale = 1.0;

    private GameViewModel? selectedGame;
    public GameViewModel SelectedGame
    {
        get
        {
            return selectedGame!;
        }
        set
        {
            if (SetProperty(ref selectedGame, value) && value != null)
            {
                //SelectGameCommand.Execute(value);
                LoadChangelogForGame(value);
            }
        }
    }

    private readonly Dictionary<(string id, string version), string> changelogCache = [];

    private static string GetDefaultGameLibraryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            ".Ignavior");
    }

    public MainViewModel()
    {
        manifestService = new();
        settingsService = new();

        var settings = settingsService.Load();
        if (string.IsNullOrEmpty(settings.LibraryPath))
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select game install folder",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };
            if (dialog.ShowDialog() == true)
            {
                settings.LibraryPath = dialog.FolderName;
                settingsService.Save(settings);
            }
            else
            {
                string defpath = GetDefaultGameLibraryPath();
                settings.LibraryPath = defpath;
                MessageBox.Show($"Operation canceled, defaulting to path {defpath}");
                settingsService.Save(settings);
            }
        }

        if (settings.Secret == null || settings.Secret.Length == 0)
        {
            string secret = Interaction.InputBox(
                "TC catchphrase (club) (lowercase, spaces) + bday DDMMYY: ",
                "First-Time Setup", "", -1, -1);
            
            if (!string.IsNullOrEmpty(secret))
            {
                settings.Secret = PasswordService.Encrypt(secret)!;
                settingsService.Save(settings);
            }
            // user-canceled, or wrong password? NYI
        }

        gameService = new(settings.LibraryPath);
        Games = [];
        _ = Async();
    }

    private async Task Async()
    {
        var manifest = await manifestService.FetchManifestAsync();
        if (manifest == null)
        {
            MessageBox.Show("Failed to fetch manifest.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw new Exception("Failed to fetch manifest.");
        }

        var games = gameService.GetInstalledGameIds().ToDictionary(id => id);
        var models = new ObservableCollection<GameViewModel>();

        foreach (var pair in manifest.Games)
        {
            string id = pair.Key;
            var game = pair.Value;

            string? version = null;
            string? displayVersion = null;

            if (games.ContainsKey(id))
            {
                var info = gameService.GetGameInfo(id);
                if (info != null)
                {
                    string dir = Path.Combine(gameService.LibraryPath, id, id);

                    if (Directory.Exists(dir))
                    {
                        version = info.InstalledVersion;
                        displayVersion = info.DisplayVersion 
                            ?? GetDisplayVersion(id, version);
                    }
                    else
                    {
                        version = null;
                    }
                }
            }

            string buttonText = "Install";
            if (version != null)
            {
                if (string.IsNullOrEmpty(displayVersion))
                {
                    displayVersion = GetDisplayVersion(id, version);
                }

                bool latest = version == game.LatestVersion;
                buttonText = latest ? "Play" : "Update";
            }

            var model = new GameViewModel
            {
                Name = game.Name,
                InstalledVersion = version ?? "",
                DisplayVersion = displayVersion ?? version ?? "",
                TextState = buttonText,

                LastPlayed = games.ContainsKey(id) ? 
                    gameService.GetGameInfo(id)?.LastPlayed ?? 
                    DateTime.MinValue : DateTime.MinValue
            };

            manifestMap[id] = game;
            models.Add(model);
        }

        Games = [.. models.OrderByDescending(g => g.LastPlayed)];
        SelectedGame = Games.FirstOrDefault()!;
    }

    private readonly Dictionary<string, GameManifest> manifestMap = [];

    private async void LoadChangelogForGame(GameViewModel game)
    {
        var entry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
        if (entry.Key == null)
            return;

        string id = entry.Key;
        var manifest = entry.Value;

        game.PatchNotes.Clear();

        var versions = new List<string>();
        if (manifest.Base != null)
        {
            versions.Add(manifest.Base.Version);
        }

        foreach (var patch in manifest.Patches)
        {
            if (!versions.Contains(patch.OldVersion))
            {
                versions.Add(patch.OldVersion);
            }
            if (!versions.Contains(patch.NewVersion))
            {
                versions.Add(patch.NewVersion);
            }
        }
        versions = [.. versions.Distinct().OrderByDescending(ver => ver)];
        
        // Holdover from old write-to-file changelog caching (currently memory-only)
        // string cache = Path.Combine(ResourceService.LocalAppDirectory, "changelogs", id);
        // Directory.CreateDirectory(cache);

        using var client = new HttpClient();

        foreach (var version in versions)
        {
            // string md = Path.Combine(cache, $"{version}.md");
            string displayName = GetDisplayVersion(id, version);
            string markdown;

            if (changelogCache.TryGetValue((id, version), out var cached))
            {
                markdown = cached;
            }
            else
            {
                string url = PathManagerService.GetChangelogUrl() + $"{id}/{version}.md";

                try
                {
                    markdown = await client.GetStringAsync(url);
                    // await File.WriteAllTextAsync(cache, markdown);
                    changelogCache[(id, version)] = markdown;
                }
                catch
                {
                    markdown = $"*No changelog available.*";
                }
            }

            game.PatchNotes.Add(new PatchNoteViewModel
            {
                Version = version,
                DisplayVersion = displayName,
                MarkdownContent = markdown,
                ReleaseDate = DateTime.MinValue
            });
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

    private static FileStream TryOpenFile(string path, FileMode mode, 
        FileAccess access, FileShare share, int maxRetries = 3)
    {
        for (int i = 1; i <= maxRetries; i++)
        {
            try
            {
                return new FileStream(path, mode, 
                    access, share, bufferSize: 4096, 
                    options: FileOptions.SequentialScan);
            }
            catch (IOException)
            when (i < maxRetries)
            {
                Thread.Sleep(300 * i);
            }
        }
        MessageBox.Show($"Failed to open {path}", "Error");
        throw new IOException($"Failed to open {path}");
    }

    private static void TryDeleteFile(string path, int maxRetries = 3)
    {
        for (int i = 1; i <= maxRetries; i++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException)
            when (i < maxRetries)
            {
                Debug.WriteLine($"Attempt {i} failed");
                Thread.Sleep(300 * i);
            }
        }
        MessageBox.Show($"Failed to delete {path}", "Error");
        throw new Exception($"Failed to delete {path}");
    }

    private static void TryDeleteDirectory(string path, int maxRetries = 3)
    {
        for (int i = 1; i <= maxRetries; i++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (Exception)
            when (i < maxRetries)
            {
                Debug.WriteLine($"Failed to delete directory {path} (attempt {i})");
                Thread.Sleep(300 * i);
            }
        }
        MessageBox.Show($"Failed to delete directory {path}", "Error");
        throw new Exception($"Failed to delete directory {path}");
    }

    private async Task<List<string>> DownloadArchivePartsAsync(
        DownloadService downloader,
        string tempDir,
        BaseInfo baseInfo)
    {
        string archiveDir = Path.Combine(tempDir, Guid.NewGuid().ToString());
        Directory.CreateDirectory(archiveDir);

        var paths = new List<string>();
        if (baseInfo.Parts != null && baseInfo.Parts.Count != 0)
        {
            foreach (var part in baseInfo.Parts)
            {
                string originalName = Path.GetFileName(new Uri(part.Url).LocalPath);
                string path = await downloader.DownloadFileAsync(part.Url, archiveDir, originalName);
                paths.Add(path);
            }
        }
        else if (!string.IsNullOrEmpty(baseInfo.Url))
        {
            string originalName = Path.GetFileName(new Uri(baseInfo.Url).LocalPath);
            string path = await downloader.DownloadFileAsync(baseInfo.Url, archiveDir, originalName);
            paths.Add(path);
        }
        else
        {
            throw new Exception("No archive URL or parts specified.");
        }
        return paths;
    }

    private async Task<List<string>> DownloadArchivePartsAsync(
        DownloadService downloader,
        string tempDir,
        PatchInfo patchInfo)
    {
        string archiveDir = Path.Combine(tempDir, Guid.NewGuid().ToString());
        Directory.CreateDirectory(archiveDir);

        var paths = new List<string>();
        if (patchInfo.Parts != null && patchInfo.Parts.Count != 0)
        {
            foreach (var part in patchInfo.Parts)
            {
                string originalName = Path.GetFileName(new Uri(part.Url).LocalPath);
                string path = await downloader.DownloadFileAsync(part.Url, archiveDir, originalName);
                paths.Add(path);
            }
        }
        else if (!string.IsNullOrEmpty(patchInfo.Url))
        {
            string originalName = Path.GetFileName(new Uri(patchInfo.Url).LocalPath);
            string path = await downloader.DownloadFileAsync(patchInfo.Url, archiveDir, originalName);
            paths.Add(path);
        }
        else
        {
            throw new Exception("No patch URL or parts specified.");
        }
        return paths;
    }

    private async void InstallGame(GameViewModel game)
    {
        var gameEntry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
        if (gameEntry.Key == null) 
            return;

        string id = gameEntry.Key;
        var manifest = gameEntry.Value;

        string library = gameService.LibraryPath;
        string outerDir = Path.Combine(library, id);
        string innerDir = Path.Combine(outerDir, id);
        Directory.CreateDirectory(outerDir);

        DownloadService downloader = new();
        string temp = PathManagerService.GetDownloadsPath();
        string? rarPath = null;

        try
        {
            List<string> downloaded = await DownloadArchivePartsAsync(downloader, temp, manifest.Base);
            rarPath = downloaded.First();
            Debug.WriteLine($"Downloaded archive to {rarPath}");

            if (!File.Exists(rarPath))
            {
                throw new Exception("Downloaded file missing!");
            }

            game.TextState = "Installing...";

            string extractTemp = Path.Combine(Path.GetTempPath(), "IgnaviorInstall", Guid.NewGuid().ToString());
            Directory.CreateDirectory(extractTemp);

            string password = GetSecret();
            var archiveOptions = new ReaderOptions { Password = password };
            
            //using (var stream = TryOpenFile(rarPath, FileMode.Open, FileAccess.Read, FileShare.Read)) // replace stream with rarpath
            using (var archive = RarArchive.OpenArchive(rarPath, archiveOptions))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    entry.WriteToDirectory(extractTemp, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }

            NormalizeExtraction(extractTemp, innerDir);

            Directory.Delete(extractTemp, true);
            foreach (var part in downloaded)
            {
                TryDeleteFile(part);
            }

            string archiveDir = Path.GetDirectoryName(downloaded.First());
            TryDeleteDirectory(archiveDir);

            string displayVer = GetDisplayVersion(id, manifest.Base.Version);
            gameService.SaveGameInfo(new LocalGameInfo
            {
                Id = id,
                InstalledVersion = manifest.Base.Version,
                DisplayVersion = displayVer,
                LastPlayed = DateTime.Now
            });

            game.InstalledVersion = manifest.Base.Version;
            game.DisplayVersion = displayVer;
            game.TextState = manifest.Base.Version == manifest.LatestVersion ? "Play" : "Update";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            MessageBox.Show($"Installation failed:\n{ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            if (rarPath != null && File.Exists(rarPath))
                TryDeleteFile(rarPath);
        }
    }

    private void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
        {
            string destination = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, destination, overwrite: true);
        }

        foreach (string subdirectory in Directory.GetDirectories(source))
        {
            string destDir = Path.Combine(target, Path.GetFileName(subdirectory));
            CopyDirectory(subdirectory, destDir);
        }
    }

    private void NormalizeExtraction(string source, string target)
    {
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        var entries = Directory.GetFileSystemEntries(source);
        if (entries.Length == 1 && Directory.Exists(entries[0]))
        {
            string subdir = entries[0];
            if (IsGameDirectory(subdir))
            {
                CopyDirectory(subdir, target);
                return;
            }
        }

        CopyDirectory(source, target);
    }

    private static bool IsGameDirectory(string path)
    {
        return Directory.GetFiles(path, "*.exe").Length != 0
            || Directory.GetDirectories(path, "*_Data").Length != 0;
    }

    private string GetDisplayVersion(string id, string version)
    {
        if (manifestMap.TryGetValue(id, out var manifest)
            && manifest.VersionNames != null
            && manifest.VersionNames.TryGetValue(version, out var displayName))
        {
            return displayName;
        }
        return version;
    }

    private async void UpdateGame(GameViewModel game)
    {
        var gameEntry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
        if (gameEntry.Key == null)
            return;

        string id = gameEntry.Key;
        var manifest = gameEntry.Value;

        string library = gameService.LibraryPath;
        string gameDirectory = Path.Combine(library, id);

        var info = gameService.GetGameInfo(id) 
            ?? throw new Exception("Preserved gameinfo.json not found!");

        string dir = Path.Combine(gameDirectory, id);
        
        var patches = new List<PatchInfo>();
        string version = game.InstalledVersion;

        // Note: patches must be listed in sequential order
        while (version != manifest.LatestVersion)
        {
            var next = manifest.Patches.FirstOrDefault(p => p.OldVersion == version) 
                ?? throw new Exception($"No patch found from {version} to next version!");
            patches.Add(next);
            version = next.NewVersion;
        }

        var downloader = new DownloadService();
        string tempDirectory = PathManagerService.GetDownloadsPath();

        foreach (var patch in patches)
        {
            List<string> downloads = await DownloadArchivePartsAsync(
                downloader, tempDirectory, patch
                );
            ApplyPatchPackage(downloads.First(), dir);
            string archiveDir = Path.GetDirectoryName(downloads.First());
            TryDeleteDirectory(archiveDir);
        }

        info.InstalledVersion = manifest.LatestVersion;
        info.DisplayVersion = GetDisplayVersion(id, manifest.LatestVersion);
        gameService.SaveGameInfo(info);

        game.InstalledVersion = manifest.LatestVersion;
        game.DisplayVersion = GetDisplayVersion(id, manifest.LatestVersion);
        game.TextState = "Play";

        // Optional:
        // ReorderGameBar(game);
    }

    private void PlayGame(GameViewModel game)
    {
        var entry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
        if (entry.Key == null)
            return;

        string id = entry.Key;
        string dir = Path.Combine(gameService.LibraryPath, id, id);
        string? exe = Directory.GetFiles(dir, "*.exe").FirstOrDefault();

        if (exe != null)
        {
            Process.Start(exe);
        }
        else
        {
            MessageBox.Show("Could not find game executable.", "Error");
        }

        gameService.UpdateLastPlayed(id);
        
        ReorderGameBar(game);
    }

    private void ReorderGameBar(GameViewModel game)
    {
        game.LastPlayed = DateTime.Now;
        var reordered = Games.OrderByDescending(g => g.LastPlayed).ToList();
        
        Games.Clear();

        foreach (var g in reordered)
        {
            Games.Add(g);
        }
        SelectedGame = game;
    }

    private string GetSecret()
    {
        var settings = settingsService.Load();
        if (settings.Secret == null || settings.Secret.Length == 0)
        {
            return null;
        }

        string secret = PasswordService.Decrypt(settings.Secret);
        return PasswordService.HashKey(secret);
    }

    private void ApplyPatchPackage(string rar, string dir)
    {
        string temp = Path.Combine(Path.GetTempPath(), "IgnaviorLauncher-patcher", Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);

        string pw = GetSecret();

        //using var fileStream = TryOpenFile(rar, FileMode.Open, FileAccess.Read, FileShare.Read); // replace rar with fileStream
        using var archive = RarArchive.OpenArchive(rar, new ReaderOptions { Password = pw });

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            entry.WriteToDirectory(temp, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
        string? manifest = Directory.GetFiles(temp, "manifest.json", SearchOption.AllDirectories).FirstOrDefault() 
            ?? throw new Exception("Patch manifest not found in package.");
        string? root = Path.GetDirectoryName(manifest);

        var patch = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(manifest));
        foreach (var deletedFile in patch!.deleted_files)
        {
            string fullPath = Path.Combine(dir, deletedFile);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        foreach (var newFile in patch.new_files)
        {
            string source = Path.Combine(root!, newFile);
            string dest = Path.Combine(dir, newFile);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, overwrite: true);
        }

        string? xdelta = AppDomain.CurrentDomain.GetData("PatcherPath") as string;

        foreach (var patchEntry in patch.patches)
        {
            string target = Path.Combine(dir, patchEntry.file);
            string patcher = Path.Combine(root!, patchEntry.patch);
            string output = target + ".patcher";

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
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
            File.Move(output, target);
        }
        Directory.Delete(temp, true);
    }

    private static string FindGameRoot(string directory)
    {
        var subdirectories = Directory.GetDirectories(directory);
        if (subdirectories.Length == 1)
        {
            string dir = subdirectories[0];
            if (Directory.GetFiles(dir, "*.exe").Length != 0 ||
                Directory.GetDirectories(dir, "*_Data").Length != 0)
            {
                return Path.GetFileName(dir);
            }
        }
        return ".";
    }

#pragma warning disable IDE1006
    private class PatchManifest
    {
        public List<PatchEntry> patches { get; set; } = [];
        public List<string> new_files { get; set; } = [];
        public List<string> deleted_files { get; set; } = [];
    }

    private class PatchEntry
    {
        public required string file { get; set; }
        public required string patch { get; set; }
    }
#pragma warning restore

    [RelayCommand]
    private void SelectGame(GameViewModel game)
    {
        if (game == null)
            return;

        SelectedGame = Games.FirstOrDefault(g => g == game) 
            ?? throw new Exception("Null value, somehow, in SelectGame");
    }

    [RelayCommand]
    private void VerifyIntegrity(GameViewModel game)
    {
        // TEMPORARY NYI
        Debug.WriteLine("Verify integrity of game files failed: Not yet implemented");
        if (game != null)
            return;
        // TEMPORARY NYI

        if (game == null)
            return;

        var entry = manifestMap;
        Debug.WriteLine(entry);
    }

    private readonly bool resetGameInfoOnUninstall = false;

    [RelayCommand]
    private void UninstallGame(GameViewModel game)
    {
        if (game == null)
        {
            return;
        }

        var result = MessageBox.Show($"Are you sure you want to uninstall {game.Name}?",
                                     "Confirm Uninstallation",
                                     MessageBoxButton.YesNo,
                                     MessageBoxImage.Question);
        
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var entry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
            if (entry.Key == null) 
                return;

            string id = entry.Key;
            string gameFilesDir = Path.Combine(gameService.LibraryPath, id, id);

            if (Directory.Exists(gameFilesDir))
            {
                Directory.Delete(gameFilesDir, true);
            }

            if (resetGameInfoOnUninstall)
            {
                gameService.RemoveGameInfo(id);
            }

            game.InstalledVersion = "";
            game.DisplayVersion = "";
            game.TextState = "Install";
            SelectedGame = game;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Uninstallation failed:\n{ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenExplorer(GameViewModel game)
    {
        if (game == null) 
            return;

        try
        {
            var entry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
            if (entry.Key == null) 
                return;

            string id = entry.Key;
            string dir = Path.Combine(gameService.LibraryPath, id, id);

            if (Directory.Exists(dir))
            {
                Process.Start("explorer.exe", dir);
            }
            else
            {
                MessageBox.Show("Game folder not found.", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open folder:\n{ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        try
        {
            var updatedManifest = await manifestService.FetchManifestAsync();
            if (updatedManifest == null)
            {
                MessageBox.Show("Failed to fetch new manifest. Please check your Internet connection.",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            manifestMap.Clear();
            foreach (var pair in updatedManifest.Games)
            {
                manifestMap[pair.Key] = pair.Value;
            }

            foreach (var game in Games)
            {
                var entry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
                if (entry.Key == null)
                    continue;

                string id = entry.Key;
                var mgame = entry.Value;

                if (!string.IsNullOrEmpty(game.InstalledVersion))
                {
                    game.DisplayVersion = GetDisplayVersion(id, game.InstalledVersion);
                }

                if (string.IsNullOrEmpty(game.InstalledVersion))
                {
                    game.TextState = "Install";
                }
                else
                {
                    bool isLatest = game.InstalledVersion == mgame.LatestVersion;
                    game.TextState = isLatest ? "Play" : "Update";
                }
            }

            if (SelectedGame != null)
            {
                LoadChangelogForGame(SelectedGame);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error when checking for updates:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}