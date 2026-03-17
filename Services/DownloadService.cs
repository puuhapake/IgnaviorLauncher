using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace IgnaviorLauncher.Services;

public class DownloadService
{
    private readonly HttpClient client;

    public DownloadService()
    {
        client = new();
    }

    public async Task<string> DownloadFileAsync(string url, string dest)
    {
        Directory.CreateDirectory(dest);

        string baseFileName = Path.GetFileName(new Uri(url).LocalPath);
        string extension = Path.GetExtension(baseFileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".tmp";
        }

        string fileName = Guid.NewGuid().ToString() + extension;
        string destPath = Path.Combine(dest, fileName);

        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using FileStream fileStream = new(
                destPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 8192,
                FileOptions.Asynchronous
                );

            await response.Content.CopyToAsync(fileStream);
            return destPath;
        }
        catch
        {
            // foo
        }

        throw new Exception("Download failure");
    }
}