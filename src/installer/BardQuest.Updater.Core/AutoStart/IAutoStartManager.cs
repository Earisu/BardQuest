namespace BardQuest.Updater.Core.AutoStart;

// Manages the OS "run at login" item for the background tray updater.
public interface IAutoStartManager
{
    bool IsEnabled();
    void Enable();   // register a login item that launches "<exe> --tray"
    void Disable();  // remove it
}
