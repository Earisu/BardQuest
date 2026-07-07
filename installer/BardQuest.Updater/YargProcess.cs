namespace BardQuest.Updater;

// Whether a YARG process is currently running (patching while it runs would fail/corrupt).
public static class YargProcess
{
    public static bool IsRunning() =>
        System.Diagnostics.Process.GetProcessesByName("YARG").Length > 0;
}
