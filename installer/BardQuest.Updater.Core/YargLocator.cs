namespace BardQuest.Updater;

public readonly record struct YargInstall(string Label, string ManagedDir);

// Finds YARG installs the YARC Launcher created under <LocalAppData>/YARC/YARG Installs.
public static class YargLocator
{
    public static bool IsValidManagedDir(string managedDir) =>
        File.Exists(Path.Combine(managedDir, "Assembly-CSharp.dll"));

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
