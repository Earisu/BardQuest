using Xunit;

namespace BardQuest.Updater.Tests;

public class UpdateEvaluatorTests
{
    private static UpdaterConfig Installed(string version) => new() { InstalledVersion = version };

    [Fact]
    public void NotInstalled_NothingFlagged()
    {
        UpdateStatus s = UpdateEvaluator.Evaluate(new UpdaterConfig(), new ReleaseInfo("v9.9.9", "u"), seamPresentInManagedDir: false);
        Assert.False(s.Installed);
        Assert.False(s.SeamMissing);
        Assert.False(s.ModUpdateAvailable);
        Assert.Equal("v9.9.9", s.AvailableVersion);
    }

    [Fact]
    public void Installed_NewerReleaseAvailable_FlagsUpdate()
    {
        UpdateStatus s = UpdateEvaluator.Evaluate(Installed("v1.0.0"), new ReleaseInfo("v1.1.0", "u"), seamPresentInManagedDir: true);
        Assert.True(s.Installed);
        Assert.False(s.SeamMissing);
        Assert.True(s.ModUpdateAvailable);
        Assert.Equal("v1.1.0", s.AvailableVersion);
    }

    [Fact]
    public void Installed_SameVersion_NoUpdate()
    {
        UpdateStatus s = UpdateEvaluator.Evaluate(Installed("v1.1.0"), new ReleaseInfo("v1.1.0", "u"), seamPresentInManagedDir: true);
        Assert.False(s.ModUpdateAvailable);
    }

    [Fact]
    public void Installed_ButSeamGone_FlagsSeamMissing()
    {
        UpdateStatus s = UpdateEvaluator.Evaluate(Installed("v1.0.0"), latest: null, seamPresentInManagedDir: false);
        Assert.True(s.SeamMissing);
        Assert.False(s.ModUpdateAvailable);
    }

    [Fact]
    public void MalformedVersions_DoNotThrow_NoUpdate()
    {
        UpdateStatus s = UpdateEvaluator.Evaluate(Installed("nightly"), new ReleaseInfo("also-bad", "u"), seamPresentInManagedDir: true);
        Assert.False(s.ModUpdateAvailable);
    }
}
