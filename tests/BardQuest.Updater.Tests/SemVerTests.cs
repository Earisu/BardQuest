using BardQuest.Updater;

using Xunit;

namespace BardQuest.Updater.Tests;

public class SemVerTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("V0.15.0", 0, 15, 0)]
    public void TryParse_AcceptsSemverWithOptionalVPrefix(string tag, int maj, int min, int patch)
    {
        Assert.True(SemVer.TryParse(tag, out (int Major, int Minor, int Patch) v));
        Assert.Equal((maj, min, patch), v);
    }

    [Theory]
    [InlineData("b3642")]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("nightly")]
    public void TryParse_RejectsNonSemver(string tag) => Assert.False(SemVer.TryParse(tag, out _));

    [Fact]
    public void IsNewer_ComparesNumericallyPerComponent()
    {
        Assert.True(SemVer.IsNewer("v1.2.4", "v1.2.3"));
        Assert.True(SemVer.IsNewer("v1.3.0", "v1.2.9"));
        Assert.True(SemVer.IsNewer("v2.0.0", "v1.9.9"));
        Assert.False(SemVer.IsNewer("v1.2.3", "v1.2.3"));
        Assert.False(SemVer.IsNewer("v1.2.3", "v1.2.4"));
    }

    [Fact]
    public void Compare_ThrowsOnMalformed() =>
        _ = Assert.Throws<FormatException>(() => SemVer.Compare("v1.0.0", "b3642"));
}
