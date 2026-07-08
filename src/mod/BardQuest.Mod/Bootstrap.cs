using YARG.Menu.Main;

// Deliberately in the top-level BardQuest namespace: the installer injects an IL call to
// BardQuest.Bootstrap.OnMainMenuEnabled (see SeamPatcher.BootstrapType), so this type name is a
// contract with the patcher. Do not fold it into BardQuest.Mod to "match the folder".
namespace BardQuest;

// The single static entry point the installer injects a call to, at the start of MainMenu.OnEnable().
public static class Bootstrap
{
    public static void OnMainMenuEnabled(MainMenu mainMenu)
    {
        try
        {
            Mod.BardQuestManager.EnsureCreated().OnMainMenuEnabled(mainMenu);
        }
        catch (Exception ex)
        {
            Mod.ModLog.Error("Bootstrap failed: " + ex);
        }
    }

    // Injected at the end of YARG's SongContainer.FillContainers(): kicks the (fire-and-forget)
    // rating build after every library refresh. Must never throw into YARG's scan flow.
    public static void OnLibraryRefreshed()
    {
        try
        {
            Mod.Scan.ScanService.OnLibraryRefreshed();
        }
        catch (Exception ex)
        {
            Mod.ModLog.Error("OnLibraryRefreshed failed: " + ex);
        }
    }
}
