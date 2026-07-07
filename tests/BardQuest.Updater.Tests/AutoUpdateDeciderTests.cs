using Xunit;

namespace BardQuest.Updater.Tests;

public class AutoUpdateDeciderTests
{
    private static UpdateStatus Status(bool installed, bool seamMissing, bool updateAvailable) =>
        new(installed, seamMissing, updateAvailable, updateAvailable ? "v1.1.0" : null);

    [Fact]
    public void Disabled_AlwaysNone()
    {
        UpdateStatus s = Status(installed: true, seamMissing: true, updateAvailable: true);
        Assert.Equal(AutoUpdateAction.None,
            AutoUpdateDecider.Decide(s, yargRunning: false, compatible: true, autoUpdateEnabled: false));
    }

    [Fact]
    public void SeamMissing_NeedsAttention()
    {
        UpdateStatus s = Status(installed: true, seamMissing: true, updateAvailable: false);
        Assert.Equal(AutoUpdateAction.NeedsAttention,
            AutoUpdateDecider.Decide(s, yargRunning: false, compatible: true, autoUpdateEnabled: true));
    }

    [Fact]
    public void NoUpdate_None()
    {
        UpdateStatus s = Status(installed: true, seamMissing: false, updateAvailable: false);
        Assert.Equal(AutoUpdateAction.None,
            AutoUpdateDecider.Decide(s, yargRunning: false, compatible: true, autoUpdateEnabled: true));
    }

    [Fact]
    public void UpdateAvailable_Incompatible_NeedsAttention()
    {
        UpdateStatus s = Status(installed: true, seamMissing: false, updateAvailable: true);
        Assert.Equal(AutoUpdateAction.NeedsAttention,
            AutoUpdateDecider.Decide(s, yargRunning: false, compatible: false, autoUpdateEnabled: true));
    }

    [Fact]
    public void UpdateAvailable_Compatible_YargRunning_WaitForExit()
    {
        UpdateStatus s = Status(installed: true, seamMissing: false, updateAvailable: true);
        Assert.Equal(AutoUpdateAction.WaitForYargExit,
            AutoUpdateDecider.Decide(s, yargRunning: true, compatible: true, autoUpdateEnabled: true));
    }

    [Fact]
    public void UpdateAvailable_Compatible_YargClosed_ApplyNow()
    {
        UpdateStatus s = Status(installed: true, seamMissing: false, updateAvailable: true);
        Assert.Equal(AutoUpdateAction.ApplyNow,
            AutoUpdateDecider.Decide(s, yargRunning: false, compatible: true, autoUpdateEnabled: true));
    }
}
