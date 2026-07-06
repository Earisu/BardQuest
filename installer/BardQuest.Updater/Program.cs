using Avalonia;

namespace BardQuest.Updater;

internal static class Program
{
    // Headless CLI (used by deploy-sandbox.sh / CI):
    //   BardQuest.Updater install <managedDir> <dllSourceDir>
    //   BardQuest.Updater patch   <managedDir>
    //   BardQuest.Updater restore <managedDir>
    // Any other invocation launches the Avalonia GUI.
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] is "install" or "patch" or "restore")
        {
            return RunCli(args);
        }

        _ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static int RunCli(string[] args)
    {
        try
        {
            switch (args[0])
            {
                case "install":
                    ModDeployer.Copy(args[2], args[1]);
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
}
