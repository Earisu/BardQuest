using Xunit;

namespace BardQuest.Updater.Tests;

public class YargLocatorTests
{
    private static string MakeInstall(string installsRoot, string guid, string tag)
    {
        string installation = Path.Combine(installsRoot, guid, "installation");
        string managed = Path.Combine(installation, YargLocator.ManagedSubpath());
        _ = Directory.CreateDirectory(managed);
        File.WriteAllText(Path.Combine(managed, "Assembly-CSharp.dll"), "stub");
        File.WriteAllText(Path.Combine(installsRoot, guid, "tag.txt"), tag);
        return managed;
    }

    [Fact]
    public void IsValidManagedDir_TrueOnlyWhenAssemblyCSharpPresent()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bq-mgd-" + Guid.NewGuid());
        _ = Directory.CreateDirectory(dir);
        try
        {
            Assert.False(YargLocator.IsValidManagedDir(dir));
            File.WriteAllText(Path.Combine(dir, "Assembly-CSharp.dll"), "stub");
            Assert.True(YargLocator.IsValidManagedDir(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void DiscoverLauncherInstalls_ReturnsValidInstallsWithTagLabels()
    {
        string root = Path.Combine(Path.GetTempPath(), "bq-installs-" + Guid.NewGuid());
        try
        {
            string stableManaged = MakeInstall(root, "guid-stable", "v0.15.0");
            string nightlyManaged = MakeInstall(root, "guid-nightly", "b3642");
            // A junk dir with no valid managed folder must be ignored.
            _ = Directory.CreateDirectory(Path.Combine(root, "empty", "installation"));

            IReadOnlyList<YargInstall> installs = YargLocator.DiscoverLauncherInstalls(root);

            Assert.Equal(2, installs.Count);
            Assert.Contains(installs, i => i.Label == "v0.15.0" && i.ManagedDir == stableManaged);
            Assert.Contains(installs, i => i.Label == "b3642" && i.ManagedDir == nightlyManaged);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DiscoverLauncherInstalls_ReturnsEmpty_WhenRootMissing()
    {
        string root = Path.Combine(Path.GetTempPath(), "bq-none-" + Guid.NewGuid());
        Assert.Empty(YargLocator.DiscoverLauncherInstalls(root));
    }
}
