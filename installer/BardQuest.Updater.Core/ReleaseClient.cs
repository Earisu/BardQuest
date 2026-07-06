using System.Text.Json;

namespace BardQuest.Updater;

public readonly record struct ReleaseInfo(string Tag, string AssetUrl);

// Reads BardQuest's GitHub releases and extracts the latest stable release's zip asset.
public static class ReleaseClient
{
    public const string DefaultOwner = "Earisu";
    public const string DefaultRepo = "BardQuest";

    public static ReleaseInfo? ParseLatestRelease(string releasesJson)
    {
        using var doc = JsonDocument.Parse(releasesJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement release in doc.RootElement.EnumerateArray())
        {
            if (GetBool(release, "draft") || GetBool(release, "prerelease"))
            {
                continue;
            }

            if (!release.TryGetProperty("tag_name", out JsonElement tagEl)
                || tagEl.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string? assetUrl = FindZipAssetUrl(release);
            if (assetUrl is null)
            {
                continue;
            }

            return new ReleaseInfo(tagEl.GetString()!, assetUrl);
        }

        return null;
    }

    public static async Task<ReleaseInfo?> FetchLatestReleaseAsync(
        HttpClient http, string owner, string repo, CancellationToken ct = default)
    {
        string url = $"https://api.github.com/repos/{owner}/{repo}/releases";
        string body = await http.GetStringAsync(url, ct);
        return ParseLatestRelease(body);
    }

    private static bool GetBool(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.True;

    private static string? FindZipAssetUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out JsonElement assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out JsonElement nameEl)
                && nameEl.ValueKind == JsonValueKind.String
                && nameEl.GetString()!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                && asset.TryGetProperty("browser_download_url", out JsonElement urlEl)
                && urlEl.ValueKind == JsonValueKind.String)
            {
                return urlEl.GetString();
            }
        }

        return null;
    }
}
