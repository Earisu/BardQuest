using System.Text.Json;

namespace BardQuest.Updater.Core.Releases;

// Reads BardQuest's GitHub releases and extracts the latest stable release's zip asset.
public static class ReleaseClient
{
    public const string DefaultOwner = "Earisu";
    public const string DefaultRepo = "BardQuest";
    public const string ModTagPrefix = "mod-v";

    // Returns the newest non-draft, non-prerelease release with a .zip asset. When
    // tagPrefix is non-empty, only releases whose tag_name starts with it are
    // considered, and the returned Tag has the prefix stripped (a bare semver).
    // Empty tagPrefix = no filter, raw tag_name (legacy behavior).
    public static ReleaseInfo? ParseLatestRelease(string releasesJson, string tagPrefix = "")
    {
        using var doc = JsonDocument.Parse(releasesJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement release in doc.RootElement.EnumerateArray().Where(release => !GetBool(release, "draft") && !GetBool(release, "prerelease")))
        {
            if (!release.TryGetProperty("tag_name", out JsonElement tagEl)
                || tagEl.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string tag = tagEl.GetString()!;
            if (tagPrefix.Length > 0 && !tag.StartsWith(tagPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string? assetUrl = FindZipAssetUrl(release);
            if (assetUrl is null)
            {
                continue;
            }

            string version = tagPrefix.Length > 0 ? tag[tagPrefix.Length..] : tag;
            return new ReleaseInfo(version, assetUrl);
        }

        return null;
    }

    public static async Task<ReleaseInfo?> FetchLatestReleaseAsync(
        HttpClient http, string owner, string repo, string tagPrefix = "", CancellationToken ct = default)
    {
        string url = $"https://api.github.com/repos/{owner}/{repo}/releases";
        string body = await http.GetStringAsync(url, ct);
        return ParseLatestRelease(body, tagPrefix);
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
