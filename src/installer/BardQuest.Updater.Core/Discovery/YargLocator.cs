namespace BardQuest.Updater.Core.Discovery;

// Finds YARG installs the YARC Launcher created under <LocalAppData>/YARC/YARG Installs.
public static class YargLocator
{
    public static bool IsValidManagedDir(string managedDir) =>
        File.Exists(Path.Combine(managedDir, "Assembly-CSharp.dll"));

    // Walks up from a Managed folder looking for an install's tag.txt (YARC layout:
    // <install>/tag.txt with the Managed folder nested under <install>/installation/...).
    // Returns the trimmed tag, or null if none is found within 8 levels.
    public static string? TagFromManagedDir(string managedDir)
    {
        DirectoryInfo? dir = new(managedDir);
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            string tagFile = Path.Combine(dir.FullName, "tag.txt");
            if (File.Exists(tagFile))
            {
                string tag = File.ReadAllText(tagFile).Trim();
                return tag.Length == 0 ? null : tag;
            }

            dir = dir.Parent;
        }

        return null;
    }

    // The Managed folder's path relative to an install's "installation/" directory.
    public static string ManagedSubpath() =>
        OperatingSystem.IsMacOS()
            ? Path.Combine("YARG.app", "Contents", "Resources", "Data", "Managed")
            : Path.Combine("YARG_Data", "Managed");

    public static string YargInstallsRoot() =>
        Path.Combine(PlatformPaths.LocalAppDataRoot(), "YARC", "YARG Installs");

    public static IReadOnlyList<YargInstall> DiscoverLauncherInstalls(string yargInstallsRoot)
    {
        var result = new List<YargInstall>();
        if (!Directory.Exists(yargInstallsRoot))
        {
            return result;
        }

        foreach (string installDir in Directory.GetDirectories(yargInstallsRoot))
        {
            string managed = Path.Combine(installDir, "installation", ManagedSubpath());
            if (!IsValidManagedDir(managed))
            {
                continue;
            }

            string tagFile = Path.Combine(installDir, "tag.txt");
            string label = File.Exists(tagFile)
                ? File.ReadAllText(tagFile).Trim()
                : Path.GetFileName(installDir);

            result.Add(new YargInstall(label, managed));
        }

        return result;
    }

    public static IReadOnlyList<YargInstall> Discover() => DiscoverLauncherInstalls(YargInstallsRoot());
}
