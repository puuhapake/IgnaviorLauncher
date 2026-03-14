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

    public async Task<string> DownloadFileAsync(string url, string dest, string fileName = null)
    {
        Directory.CreateDirectory(dest);
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = Path.GetFileName(new Uri(url).LocalPath) ?? Guid.NewGuid().ToString();
        }

        string destPath = Path.Combine(dest, fileName);

        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using FileStream fileStream = new(dest, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fileStream);

        return destPath;
    }
}