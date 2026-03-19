using System.Reflection;
using System.IO;
using SharpCompress.Compressors.Xz;

namespace IgnaviorLauncher.Services;

public static class ResourceService
{
    private static readonly string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string BaseDirectory = Path.Combine(local, "IgnaviorLauncher", "bin");
    private const string REL_PATH = "IgnaviorLauncher.Resources.";

    // TODO: Move to separate service
    public static readonly string LocalAppDirectory = Path.Combine(local, "IgnaviorLauncher");

    /// <summary>
    /// Finds the xdelta3 patcher executable from embedded resources.
    /// </summary>
    public static string GetPatcher()
    {
        string exe = Path.Combine(BaseDirectory, "xdelta3.exe");
        if (File.Exists(exe))
        {
            return exe;
        }

        Directory.CreateDirectory(BaseDirectory);
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(REL_PATH + "xdelta3.exe"))
        {
            if (stream == null)
            {
                throw new Exception("Patcher executable (xdelta3) not found in embedded resources.");
            }

            using FileStream fileStream = new(
                path: exe,
                mode: FileMode.Create,
                access: FileAccess.Write);
            stream.CopyTo(fileStream);
        }
        return exe;
    }

    public static string GetZipper()
    {
        string exe = Path.Combine(BaseDirectory, "7z.exe");
        string dll = Path.Combine(BaseDirectory, "7z.dll");
        if (File.Exists(exe) && File.Exists(dll))
        {
            return exe;
        }

        Directory.CreateDirectory(BaseDirectory);
        ExtractResource(REL_PATH + "7z.exe", exe);
        ExtractResource(REL_PATH + "7z.dll", dll);
        return exe;
    }

    private static void ExtractResource(string resource, string output)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resource);

        if (stream == null)
        {
            System.Diagnostics.Debug.WriteLine($"Resource {resource} not found");
            return;
        }
        using var fileStream = new FileStream(
            output,
            FileMode.Create,
            FileAccess.Write
        );
        stream.CopyTo(fileStream);
    }
}