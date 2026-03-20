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
        return "https://raw.githubusercontent.com/puuhapake/IgnaviorLauncher_files/main/changelogs/";
    }

    public static string GetManifestUrl()
    {
        return "https://raw.githubusercontent.com/puuhapake/IgnaviorLauncher_files/main/manifest.json";
    }

    public static string GetIconUrl()
    {
        return "https://raw.githubusercontent.com/puuhapake/IgnaviorLauncher_files/main/icons/";
    }
}
