using System.IO;

namespace IgnaviorLauncher.Services;

public class PathManagerService
{
    public static string GetDownloadsPath()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IgnaviorLauncher", "Downloads");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetChangelogUrl()
    {
        return $"https://raw.githubusercontent.com/puuhapake/IgnaviorLauncher_files/main/changelogs/";
    }

    private const string manifest = "https://raw.githubusercontent.com/puuhapake/IgnaviorLauncher_files/main/manifest.json";
    public static string ManifestUrl = manifest;
}
