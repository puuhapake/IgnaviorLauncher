using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace IgnaviorLauncher.Services;

internal class ManifestService
{
    private readonly HttpClient client;
    private const string ManifestUrl = "https://raw.githubusercontent.com/puuhapake/IgnaviorLauncher_files/main/manifest.json";

    public ManifestService()
    {
        client = new();
    }

    public async Task<RootManifest> FetchManifestAsync()
    {
        try
        {
            var json = await client.GetStringAsync(ManifestUrl);
            return JsonSerializer.Deserialize<RootManifest>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Manifest fetch failed: {ex}");
            return null;
        }
    }
}
