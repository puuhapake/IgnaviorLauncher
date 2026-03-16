using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace IgnaviorLauncher.Services;

internal class ManifestService
{
    private readonly HttpClient client;
    private const string ManifestUrl = "https://raw.githubusercontent.com/puuhapake/IgnaviorLauncher_files/main/manifest.json";

    private readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
    };

    public ManifestService()
    {
        client = new();
    }

    public async Task<RootManifest> FetchManifestAsync()
    {
        try
        {
            var json = await client.GetStringAsync(ManifestUrl);
            System.Diagnostics.Debug.WriteLine($"Manifest: {json}");
            var result = JsonSerializer.Deserialize<RootManifest>(json, options);
            System.Diagnostics.Debug.WriteLine($"Games: {result?.Games?.Count ?? 0}");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Manifest fetch failed: {ex}");
            return null;
        }
    }
}
