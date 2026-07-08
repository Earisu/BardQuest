namespace BardQuest.Updater;

public static class AutoStartManager
{
    // Returns the right manager for the running OS; a no-op on unsupported platforms.
    public static IAutoStartManager ForCurrentOs(string execPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsAutoStartManager(execPath);
        }

        if (OperatingSystem.IsMacOS())
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
            return new MacAutoStartManager(execPath, dir);
        }

        return new NoOpAutoStartManager();
    }
}
