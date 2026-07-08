namespace BardQuest.Updater.Core.Updating;

// Pure decision for the background updater: given the current update status, whether
// YARG is running, whether the downloaded build is compatible with the installed YARG,
// and whether the user has auto-update enabled, decide what the tray service should do.
public static class AutoUpdateDecider
{
    public static AutoUpdateAction Decide(
        UpdateStatus status, bool yargRunning, bool compatible, bool autoUpdateEnabled)
    {
        return !autoUpdateEnabled
            ? AutoUpdateAction.None
            : status.SeamMissing
            ? AutoUpdateAction.NeedsAttention
            : !status.ModUpdateAvailable
            ? AutoUpdateAction.None
            : !compatible ? AutoUpdateAction.NeedsAttention : yargRunning ? AutoUpdateAction.WaitForYargExit : AutoUpdateAction.ApplyNow;
    }
}
