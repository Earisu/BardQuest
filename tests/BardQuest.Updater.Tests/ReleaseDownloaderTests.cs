using BardQuest.Updater.Core.Patching;
using BardQuest.Updater.Core.Releases;

using Xunit;

namespace BardQuest.Updater.Tests;

public class ReleaseDownloaderTests
{
    private static void WriteModDlls(string dir)
    {
        _ = Directory.CreateDirectory(dir);
        foreach (string name in ModDeployer.ModDllNames)
        {
            File.WriteAllText(Path.Combine(dir, name), "stub");
        }
    }

    [Fact]
    public void ValidateExtracted_ReturnsDir_WhenDllsAtRoot()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bq-ex-" + Guid.NewGuid());
        try
        {
            WriteModDlls(dir);
            Assert.Equal(dir, ReleaseDownloader.ValidateExtracted(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void ValidateExtracted_ReturnsSubdir_WhenDllsNested()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bq-ex-" + Guid.NewGuid());
        string sub = Path.Combine(dir, "BardQuest");
        try
        {
            _ = Directory.CreateDirectory(dir);
            WriteModDlls(sub);
            Assert.Equal(sub, ReleaseDownloader.ValidateExtracted(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void ValidateExtracted_ReturnsNull_WhenDllsAbsent()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bq-ex-" + Guid.NewGuid());
        try
        {
            _ = Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "readme.txt"), "x");
            Assert.Null(ReleaseDownloader.ValidateExtracted(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
