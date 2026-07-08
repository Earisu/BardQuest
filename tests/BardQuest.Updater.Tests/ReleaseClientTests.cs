using BardQuest.Updater.Core.Releases;

using Xunit;

namespace BardQuest.Updater.Tests;

public class ReleaseClientTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "github-releases.json"));

    [Fact]
    public void ParseLatestRelease_WithModPrefix_SkipsUpdaterAndPrerelease_StripsPrefix()
    {
        ReleaseInfo? info = ReleaseClient.ParseLatestRelease(Fixture(), ReleaseClient.ModTagPrefix);

        _ = Assert.NotNull(info);
        Assert.Equal("1.0.0", info.Value.Tag); // "mod-v" stripped
        Assert.Equal("https://example.test/bardquest-mod-1.0.0.zip", info.Value.AssetUrl);
    }

    [Fact]
    public void ParseLatestRelease_NoPrefix_ReturnsRawTagOfFirstStableWithZip()
    {
        // Default (empty) prefix = legacy behavior: no filter, raw tag_name.
        ReleaseInfo? info = ReleaseClient.ParseLatestRelease(Fixture());

        _ = Assert.NotNull(info);
        Assert.Equal("updater-v0.3.0", info.Value.Tag);
        Assert.Equal("https://example.test/updater.zip", info.Value.AssetUrl);
    }

    [Fact]
    public void ParseLatestRelease_WithModPrefix_ReturnsNull_WhenOnlyUpdaterReleases()
    {
        const string json = /*lang=json,strict*/ """
        [ { "tag_name": "updater-v0.3.0", "draft": false, "prerelease": false,
            "assets": [ { "name": "u.zip", "browser_download_url": "https://example.test/u.zip" } ] } ]
        """;
        Assert.Null(ReleaseClient.ParseLatestRelease(json, ReleaseClient.ModTagPrefix));
    }

    [Fact]
    public void ParseLatestRelease_ReturnsNull_OnEmptyArray() =>
        Assert.Null(ReleaseClient.ParseLatestRelease("[]", ReleaseClient.ModTagPrefix));

    [Fact]
    public void ParseLatestRelease_WithModPrefix_ReturnsNull_WhenNoZipAsset()
    {
        const string json = /*lang=json,strict*/ """
        [ { "tag_name": "mod-v2.0.0", "draft": false, "prerelease": false,
            "assets": [ { "name": "readme.txt", "browser_download_url": "https://example.test/r.txt" } ] } ]
        """;
        Assert.Null(ReleaseClient.ParseLatestRelease(json, ReleaseClient.ModTagPrefix));
    }
}
