namespace BardQuest.Updater;

public sealed class NoOpAutoStartManager : IAutoStartManager
{
    public bool IsEnabled() => false;
    public void Enable() { }
    public void Disable() { }
}
