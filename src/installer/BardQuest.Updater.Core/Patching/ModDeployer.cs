namespace BardQuest.Updater.Core.Patching;

// Copies/removes the three BardQuest DLLs into/out of a YARG Managed folder.
public static class ModDeployer
{
    public static IReadOnlyList<string> ModDllNames { get; } =
        ["BardQuest.Mod.dll", "BardQuest.Domain.dll", "YARG.Core.dll"];

    public static void Copy(string dllSourceDir, string managedDir)
    {
        foreach (string name in ModDllNames)
        {
            string src = Path.Combine(dllSourceDir, name);
            if (!File.Exists(src))
            {
                throw new FileNotFoundException("Missing DLL to deploy: " + src);
            }

            File.Copy(src, Path.Combine(managedDir, name), overwrite: true);
        }
    }

    public static void Delete(string managedDir)
    {
        foreach (string name in ModDllNames)
        {
            string path = Path.Combine(managedDir, name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
