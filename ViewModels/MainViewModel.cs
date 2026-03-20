using System.Collections.ObjectModel;
using System.Text.Json;
using System.IO;

using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Windows;
using System.Net.Http;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using SharpCompress.Archives.Rar;
using SharpCompress.Archives;
using SharpCompress.Readers;
using SharpCompress.Common;

using Microsoft.VisualBasic;

namespace IgnaviorLauncher.ViewModels;
using Services;

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
                LoadChangelogForGame(value);
            }
        }
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

    private readonly Dictionary<(string id, string version), string> changelogCache = [];
    private readonly Dictionary<string, GameManifest> manifestMap = [];
    private readonly Dictionary<string, BitmapImage> gameIcons = new();

    #region Constructor
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
                "Vad är TC:s catchphrase (\"klubb\"), allt med litet, plus min födelsedag (DDMMYY)\nExempel: \"hasse har en hammare + 290204\"",
                "First-Time Setup", "", -1, -1);

            if (!string.IsNullOrEmpty(secret))
            {
                settings.Secret = PasswordService.Encrypt(secret)!;
                settingsService.Save(settings);
            }
            // user-canceled, or wrong password? NYI
        }

        gameService = new(settings.LibraryPath);
        CleanupTempDirectory();
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

        Debug.WriteLine($"Games found: {string.Join(", ", manifest.Games!.Keys)}");

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
                            ?? GetDisplayVersion(id, version!);
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
            _ = LoadGameIcon(id, model);
            models.Add(model);
        }

        Games = [.. models.OrderByDescending(g => g.LastPlayed)];
        SelectedGame = Games.FirstOrDefault()!;
    }
    #endregion

    #region Deletion
    private static void CleanupTempDirectory()
    {
        try
        {
            string downloads = PathManagerService.GetDownloadsPath();
            foreach (var dir in Directory.GetDirectories(downloads))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete {dir}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cleanup error {ex}");
        }
    }
    #endregion

    private async Task LoadGameIcon(string id, GameViewModel game)
    {
        try
        {
            string url = PathManagerService.GetIconUrl() + $"{id}.png";
            var bmp = new BitmapImage();

            bmp.BeginInit();
            bmp.UriSource = new Uri(url);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            gameIcons[id] = bmp;
            game.Icon = bmp;
        }
        catch
        {
            // first letter will be shown instead as text
        }
    }

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
            versions.Add(manifest.Base.Version!);
        }

        if (manifest.Patches != null)
        {
            foreach (var patch in manifest.Patches!)
            {
                if (!versions.Contains(patch.OldVersion!))
                {
                    versions.Add(patch.OldVersion!);
                }
                if (!versions.Contains(patch.NewVersion!))
                {
                    versions.Add(patch.NewVersion!);
                }
            }
        }

        versions = [.. versions.Distinct().OrderByDescending(ver => ver)];

        using var client = new HttpClient();
        foreach (var version in versions)
        {
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

    private static async Task<List<string>> DownloadArchivePartsAsync(
        DownloadService downloader,
        string tempDir,
        BaseInfo baseInfo,
        IProgress<double>? progress = null)
    {
        string archiveDir = Path.Combine(tempDir, Guid.NewGuid().ToString());
        Directory.CreateDirectory(archiveDir);

        var paths = new List<string>();
        var parts = baseInfo.Parts ?? new List<PartInfo>();
        int totalParts = parts.Count;

        if (totalParts == 0 && !string.IsNullOrEmpty(baseInfo.Url))
        {
            totalParts = 1;
        }

        int completed = 0;
        if (baseInfo.Parts != null && baseInfo.Parts.Count != 0)
        {
            foreach (var part in baseInfo.Parts)
            {
                string originalName = Path.GetFileName(new Uri(part.Url!).LocalPath);

                string path = await downloader.DownloadFileAsync(part.Url!, archiveDir, originalName, 
                    progress: progress == null ? null : new Progress<double>(
                        p => progress.Report((completed + p) / totalParts))
                    );
                paths.Add(path);
                completed++;
                progress?.Report((double)completed / totalParts);
            }
        }
        else if (!string.IsNullOrEmpty(baseInfo.Url))
        {
            string originalName = Path.GetFileName(new Uri(baseInfo.Url).LocalPath);
            string path = await downloader.DownloadFileAsync(baseInfo.Url, archiveDir, originalName, progress);
            paths.Add(path);
            progress?.Report(1.0);
        }
        else
        {
            throw new Exception("No archive URL or parts specified.");
        }
        return paths;
    }

    // move to download service?
    private static async Task<List<string>> DownloadArchivePartsAsync(
        DownloadService downloader,
        string tempDir,
        PatchInfo patchInfo,
        IProgress<double>? progress = null)
    {
        string archiveDir = Path.Combine(tempDir, Guid.NewGuid().ToString());
        Directory.CreateDirectory(archiveDir);

        var paths = new List<string>();
        var parts = patchInfo.Parts ?? [];
        int totalParts = parts.Count;

        if (totalParts == 0 && !string.IsNullOrEmpty(patchInfo.Url))
        {
            totalParts = 1;
        }

        int completed = 0;
        if (patchInfo.Parts != null && patchInfo.Parts.Count != 0)
        {
            foreach (var part in patchInfo.Parts)
            {
                string originalName = Path.GetFileName(new Uri(part.Url!).LocalPath);

                string path = await downloader.DownloadFileAsync(part.Url!, archiveDir, originalName,
                    progress: progress == null ? null : new Progress<double>(
                        p => progress.Report((completed + p) / totalParts)));
                paths.Add(path);
                completed++;
                progress?.Report((double)completed / totalParts);
            }
        }
        else if (!string.IsNullOrEmpty(patchInfo.Url))
        {
            string originalName = Path.GetFileName(new Uri(patchInfo.Url).LocalPath);
            string path = await downloader.DownloadFileAsync(patchInfo.Url, archiveDir, originalName, progress);
            paths.Add(path);
            progress?.Report(1.0);
        }
        else
        {
            throw new Exception("No patch URL or parts specified.");
        }
        return paths;
    }

    private static void ExtractArchive(string path, string target, string password)
    {
        bool extracted = false;

        try
        {
            using var archive = RarArchive.OpenArchive(path, new ReaderOptions { Password = password });
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                entry.WriteToDirectory(target, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
            }
            extracted = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SharpCompress extraction failed: {ex}");
        }

        if (!extracted)
        {
            Debug.WriteLine("Fallback to 7zip");
            string szip = AppDomain.CurrentDomain.GetData("7zPath") as string
                ?? throw new Exception("SharpCompress failed and 7zip unavailable");

            string temp7z = Path.Combine(Path.GetTempPath(), "7z_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp7z);

            ProcessStartInfo processInfo = new()
            {
                FileName = szip,
                Arguments = $"x -p\"{password}\" -o\"{temp7z}\" -y \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(processInfo);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"7z extraction failed: {process.StandardError.ReadToEnd()}");
            }

            CopyDirectory(temp7z, target);
            Directory.Delete(temp7z, true);
        }

        string[] files = Directory.GetFiles(target, "*", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            throw new Exception("Extraction produced no files");
        }

        string manifest = Directory.GetFiles(target, "manifest.json", SearchOption.AllDirectories).FirstOrDefault();
        if (manifest != null && new FileInfo(manifest).Length == 0)
        {
            throw new Exception("Extracted manifest empty");
        }
    }

    #region Installation
    private async void InstallGame(GameViewModel game)
    {
        var gameEntry = manifestMap.FirstOrDefault(pair => pair.Value.Name == game.Name);
        if (gameEntry.Key == null || game.IsDownloading)
            return;

        game.IsDownloading = true;
        game.DownloadProgress = 0;
        game.TextState = "Downloading...";

        string id = gameEntry.Key;
        var manifest = gameEntry.Value;

        string library = gameService.LibraryPath;
        string outerDir = Path.Combine(library, id);
        string innerDir = Path.Combine(outerDir, id);
        Directory.CreateDirectory(outerDir);

        DownloadService downloader = new();
        string temp = PathManagerService.GetDownloadsPath();
        string? rar = null;

        try
        {
            Progress<double> progress = new(p => game.DownloadProgress = p * 100);
            List<string> downloaded = await DownloadArchivePartsAsync(downloader, temp, manifest.Base, progress);
            
            VerifyPartFiles(downloaded);
            rar = downloaded.First();
            Debug.WriteLine($"Downloaded archive to {rar}");

            if (!File.Exists(rar))
            {
                throw new Exception("Downloaded file missing!");
            }


            await Task.Delay(50);
            game.TextState = "Extracting...";
            await Task.Delay(50);
            game.IsExtracting = true;

            string pw = GetSecret();

            string extractTemp = Path.Combine(Path.GetTempPath(), "IgnaviorLauncher-install", Guid.NewGuid().ToString());
            Directory.CreateDirectory(extractTemp);

            ExtractArchive(rar, extractTemp, GetSecret());
            NormalizeExtraction(extractTemp, innerDir);

            if (Directory.GetFiles(innerDir, "*.exe").Length == 0)
            {
                Debug.WriteLine("No executable found after installation. Installation likely failed.");
                return;
            }

            Directory.Delete(extractTemp, true);

            string displayVer = GetDisplayVersion(id, manifest.Base.Version!);
            gameService.SaveGameInfo(new LocalGameInfo
            {
                Id = id,
                InstalledVersion = manifest.Base.Version,
                DisplayVersion = displayVer,
                LastPlayed = DateTime.Now
            });

            game.IsExtracting = false;
            game.IsDownloading = false;
            game.InstalledVersion = manifest.Base.Version;
            game.DisplayVersion = displayVer;
            game.TextState = manifest.Base.Version == manifest.LatestVersion ? "Play" : "Update";
        }
        catch (Exception ex)
        {
            game.TextState = "Install";
            game.IsDownloading = false;

            Debug.WriteLine(ex);
            MessageBox.Show($"Installation failed:\n{ex.Message}\nStack trace: {ex.StackTrace}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            if (rar != null && File.Exists(rar))
            {
                try
                {
                    File.Delete(rar);
                }
                catch
                {
                    Debug.WriteLine($"Failed to delete {rar}");
                }
            }
        }
    }

    private static void NormalizeExtraction(string source, string target)
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

    private static void CopyDirectory(string source, string target)
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
    #endregion

    #region Update and patching
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
        string version = game.InstalledVersion!;

        game.TextState = "Patching...";

        // Note: patches must be listed in sequential order
        while (version != manifest.LatestVersion)
        {
            var next = manifest.Patches!.FirstOrDefault(p => p.OldVersion == version)
                ?? throw new Exception($"No patch found from {version} to next version!");
            patches.Add(next);
            version = next.NewVersion!;
        }

        var downloader = new DownloadService();
        string tempDirectory = PathManagerService.GetDownloadsPath();

        foreach (var patch in patches)
        {
            List<string> downloads = await DownloadArchivePartsAsync(
                downloader, tempDirectory, patch
                );
            VerifyPartFiles(downloads);
            ApplyPatchPackage(downloads.First(), dir);
        }

        game.IsDownloading = false;
        info.InstalledVersion = manifest.LatestVersion;
        info.DisplayVersion = GetDisplayVersion(id, manifest.LatestVersion);
        gameService.SaveGameInfo(info);

        game.InstalledVersion = manifest.LatestVersion;
        game.DisplayVersion = GetDisplayVersion(id, manifest.LatestVersion);
        game.TextState = "Play";

        // Optional:
        // ReorderGameBar(game);
    }

    private void ApplyPatchPackage(string rar, string dir)
    {
        string temp = Path.Combine(Path.GetTempPath(), "IgnaviorLauncher-patcher", Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);

        ExtractArchive(rar, temp, GetSecret());

        string? manifest = Directory.GetFiles(temp, "manifest.json", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new Exception("Patch manifest not found in package.");

        string root = Path.GetDirectoryName(manifest)!;
        
        var patch = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(manifest));
        
        foreach (var patchEntry in patch!.patches)
        {
            string path = Path.Combine(root, patchEntry.patch);
            var patcherInfo = new FileInfo(path);
            Debug.WriteLine($"Patch file size {path}: {new FileInfo(path).Length} with manifest of length {new FileInfo(manifest).Length}");

            if (!patcherInfo.Exists)
            {
                Debug.WriteLine($"Missing patch file {patchEntry.patch}");
                return;
            }
            if (patcherInfo.Length == 0)
            {
                Debug.WriteLine($"Patch file {patchEntry.patch} is empty!");
                return;
            }
        }

        foreach (var deletedFile in patch!.deleted_files)
        {
            string fullPath = Path.Combine(dir, deletedFile);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        foreach (var newFile in patch!.new_files)
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
            if (!File.Exists(patcher))
            {
                throw new Exception("Patcher not found");
            }

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

            if (!File.Exists(output))
            {
                throw new Exception($"xdelta3 did not create output file {output}");
            }

            File.Delete(target);
            File.Move(output, target);
        }

        Directory.Delete(temp, true);
    }
    #endregion

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

    #region Helpers
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
            return null!;
        }

        string secret = PasswordService.Decrypt(settings.Secret)!;
        return PasswordService.HashKey(secret)!;
    }

    private static string GetDefaultGameLibraryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".Ignavior");
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

    private static void VerifyPartFiles(List<string> parts)
    {
        foreach (var path in parts)
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                throw new Exception($"Part file missing: {path}");
            }
            if (info.Length == 0)
            {
                throw new Exception($"Part file empty: {path}");
            }
        }
    }
    #endregion

    #region Commands
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
            default:
                break;
        }
    }

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
            foreach (var pair in updatedManifest.Games!)
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
    #endregion
}