using Xunit;

namespace BardQuest.Updater.Tests;

public class UpdaterConfigTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bq-cfg-" + Guid.NewGuid());
        string path = Path.Combine(dir, "updater-config.json");
        try
        {
            var written = new UpdaterConfig
            {
                ManagedDir = "/some/Managed",
                InstalledVersion = "v1.2.3",
                AutoStartEnabled = true,
                LastCheckUtc = new DateTime(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc),
            };
            written.Save(path);

            var read = UpdaterConfig.Load(path);
            Assert.Equal("/some/Managed", read.ManagedDir);
            Assert.Equal("v1.2.3", read.InstalledVersion);
            Assert.True(read.AutoStartEnabled);
            Assert.Equal(written.LastCheckUtc, read.LastCheckUtc);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_ReturnsEmptyConfig_WhenFileMissing()
    {
        string path = Path.Combine(Path.GetTempPath(), "bq-missing-" + Guid.NewGuid(), "x.json");
        var cfg = UpdaterConfig.Load(path);
        Assert.Null(cfg.ManagedDir);
        Assert.Null(cfg.InstalledVersion);
        Assert.False(cfg.AutoStartEnabled);
        Assert.Null(cfg.LastCheckUtc);
    }

    [Fact]
    public void Load_ReturnsEmptyConfig_WhenFileCorrupt()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bq-corrupt-" + Guid.NewGuid());
        string path = Path.Combine(dir, "x.json");
        _ = Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(path, "{ not valid json");
            var cfg = UpdaterConfig.Load(path);
            Assert.Null(cfg.ManagedDir);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
