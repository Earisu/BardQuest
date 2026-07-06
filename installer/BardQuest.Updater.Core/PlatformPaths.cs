namespace BardQuest.Updater;

// Resolves the per-user local app-data root the same way YARG's PathHelper does,
// so we find the YARC launcher's installs in the same place the game does.
public static class PlatformPaths
{
    public static string LocalAppDataRoot()
    {
        return OperatingSystem.IsMacOS()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }
}
