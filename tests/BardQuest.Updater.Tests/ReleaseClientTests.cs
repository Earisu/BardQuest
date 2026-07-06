using Xunit;

namespace BardQuest.Updater.Tests;

public class ReleaseClientTests
{
    [Fact]
    public void ParseLatestRelease_SkipsPrerelease_PicksZipAsset()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "github-releases.json"));

        ReleaseInfo? info = ReleaseClient.ParseLatestRelease(json);

        _ = Assert.NotNull(info);
        Assert.Equal("v1.0.0", info!.Value.Tag);
        Assert.Equal("https://example.test/BardQuest-v1.0.0.zip", info.Value.AssetUrl);
    }

    [Fact]
    public void ParseLatestRelease_ReturnsNull_OnEmptyArray() =>
        Assert.Null(ReleaseClient.ParseLatestRelease("[]"));

    [Fact]
    public void ParseLatestRelease_ReturnsNull_WhenNoZipAsset()
    {
        const string json = /*lang=json,strict*/ """
        [ { "tag_name": "v2.0.0", "draft": false, "prerelease": false,
            "assets": [ { "name": "readme.txt", "browser_download_url": "https://example.test/r.txt" } ] } ]
        """;
        Assert.Null(ReleaseClient.ParseLatestRelease(json));
    }
}
