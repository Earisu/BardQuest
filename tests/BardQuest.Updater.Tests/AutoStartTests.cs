using Xunit;

namespace BardQuest.Updater.Tests;

public class AutoStartTests
{
    [Fact]
    public void BuildRunKeyValue_QuotesPathAndAddsTrayArg()
    {
        Assert.Equal("\"C:\\Program Files\\BardQuest\\BardQuest.Updater.exe\" --tray",
            AutoStartCommand.BuildRunKeyValue(@"C:\Program Files\BardQuest\BardQuest.Updater.exe"));
    }

    [Fact]
    public void BuildLaunchAgentPlist_ContainsLabelPathAndTrayArg()
    {
        string plist = AutoStartCommand.BuildLaunchAgentPlist(
            "/Applications/BardQuest Updater.app/Contents/MacOS/BardQuest.Updater",
            AutoStartCommand.LaunchAgentLabel);

        Assert.Contains("<key>Label</key><string>com.bardquest.updater</string>", plist);
        Assert.Contains("/Applications/BardQuest Updater.app/Contents/MacOS/BardQuest.Updater", plist);
        Assert.Contains("<string>--tray</string>", plist);
        Assert.Contains("<key>RunAtLoad</key><true/>", plist);
    }

    [Fact]
    public void MacManager_EnableWritesPlist_DisableRemovesIt()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bq-la-" + Guid.NewGuid());
        try
        {
            var mgr = new MacAutoStartManager("/Applications/BardQuest Updater.app/Contents/MacOS/BardQuest.Updater", dir);
            Assert.False(mgr.IsEnabled());

            mgr.Enable();
            Assert.True(mgr.IsEnabled());
            string plistPath = Path.Combine(dir, "com.bardquest.updater.plist");
            Assert.True(File.Exists(plistPath));
            Assert.Contains("--tray", File.ReadAllText(plistPath));

            mgr.Disable();
            Assert.False(mgr.IsEnabled());
            Assert.False(File.Exists(plistPath));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
