using System.Net.Http;

using System.Text.Json.Serialization;
using System.Text.Json;

namespace IgnaviorLauncher.Services;

internal class ManifestService
{
    private readonly HttpClient client;

    private readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    public ManifestService()
    {
        client = new();
    }

    public async Task<RootManifest> FetchManifestAsync()
    {
        try
        {
            var json = await client.GetStringAsync(PathManagerService.GetManifestUrl());
            var result = JsonSerializer.Deserialize<RootManifest>(json, options);
            return result!;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Manifest fetch failed: {ex}");
            return null!;
        }
    }
}
