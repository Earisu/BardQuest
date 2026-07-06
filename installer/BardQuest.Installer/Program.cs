using System;
using System.IO;

namespace BardQuest.Installer;

internal static class Program
{
    // Usage:
    //   BardQuest.Installer install <managedDir> <dllSourceDir>
    //   BardQuest.Installer patch   <managedDir>
    //   BardQuest.Installer restore <managedDir>
    private static int Main(string[] args)
    {
        try
        {
            switch (args.Length >= 1 ? args[0] : "")
            {
                case "install":
                    CopyDlls(args[2], args[1]);
                    SeamPatcher.Patch(args[1]);
                    Console.WriteLine("BardQuest installed.");
                    return 0;
                case "patch":
                    SeamPatcher.Patch(args[1]);
                    Console.WriteLine("BardQuest seam patched.");
                    return 0;
                case "restore":
                    SeamPatcher.Restore(args[1]);
                    Console.WriteLine("BardQuest restored.");
                    return 0;
                default:
                    Console.Error.WriteLine("Usage: install <managedDir> <dllSourceDir> | patch <managedDir> | restore <managedDir>");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 2;
        }
    }

    private static void CopyDlls(string dllSourceDir, string managedDir)
    {
        foreach (var name in new[] { "BardQuest.Mod.dll", "BardQuest.Domain.dll", "YARG.Core.dll" })
        {
            var src = Path.Combine(dllSourceDir, name);
            if (!File.Exists(src)) throw new FileNotFoundException("Missing DLL to deploy: " + src);
            File.Copy(src, Path.Combine(managedDir, name), overwrite: true);
        }
    }
}
