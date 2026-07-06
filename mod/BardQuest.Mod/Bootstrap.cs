using YARG.Menu.Main;

namespace BardQuest.Mod;

// The single static entry point the installer injects a call to, at the start of MainMenu.OnEnable().
public static class Bootstrap
{
    public static void OnMainMenuEnabled(MainMenu mainMenu)
    {
        try
        {
            BardQuestManager.EnsureCreated().OnMainMenuEnabled(mainMenu);
        }
        catch (Exception ex)
        {
            ModLog.Error("Bootstrap failed: " + ex);
        }
    }
}
