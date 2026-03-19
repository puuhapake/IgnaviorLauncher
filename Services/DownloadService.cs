using System.Net.Http;
using System.IO;

namespace IgnaviorLauncher.Services;

public class DownloadService
{
    private readonly HttpClient client;

    public DownloadService()
    {
        client = new();
    }

    public async Task<string> DownloadFileAsync(string url, string dest, string? overrideFileName = null)
    {
        Directory.CreateDirectory(dest);
        string? fileName = overrideFileName ?? Guid.NewGuid().ToString() + Path.GetExtension(new Uri(url).LocalPath);
        string tempPath = Path.Combine(dest, fileName + ".tmp");
        string finalPath = Path.Combine(dest, fileName);

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? contentLength = response.Content.Headers.ContentLength;

                using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 8192, true))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                if (contentLength.HasValue)
                {
                    var fileInfo = new FileInfo(tempPath);
                    if (fileInfo.Length != contentLength.Value)
                    {
                        File.Delete(tempPath);
                        throw new Exception($"Download incomplete: expected {contentLength.Value} bytes but got {fileInfo.Length}");
                    }
                }
                File.Move(tempPath, finalPath, overwrite: true);
                return finalPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Download attempt {attempt} failed: {ex.Message}");
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                if (attempt == maxRetries)
                {
                    throw new Exception($"Download failed after {maxRetries} attempts: {ex.Message}", ex);
                }
                await Task.Delay(1000);
            }
        }
        throw new Exception("Download failure.");
    }
}