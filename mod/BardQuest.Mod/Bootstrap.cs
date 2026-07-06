using YARG.Menu.Main;

namespace BardQuest
{
    // The single static entry point the installer injects a call to, at the start of MainMenu.OnEnable().
    public static class Bootstrap
    {
        public static void OnMainMenuEnabled(MainMenu mainMenu)
        {
            try
            {
                Mod.BardQuestManager.EnsureCreated().OnMainMenuEnabled(mainMenu);
            }
            catch (System.Exception ex)
            {
                Mod.ModLog.Error("Bootstrap failed: " + ex);
            }
        }
    }
}
