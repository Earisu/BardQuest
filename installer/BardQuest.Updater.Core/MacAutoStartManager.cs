namespace BardQuest.Updater;

public sealed class MacAutoStartManager(string execPath, string launchAgentsDir) : IAutoStartManager
{
    private string PlistPath => Path.Combine(launchAgentsDir, AutoStartCommand.LaunchAgentLabel + ".plist");

    public bool IsEnabled() => File.Exists(PlistPath);

    public void Enable()
    {
        _ = Directory.CreateDirectory(launchAgentsDir);
        File.WriteAllText(PlistPath,
            AutoStartCommand.BuildLaunchAgentPlist(execPath, AutoStartCommand.LaunchAgentLabel));
    }

    public void Disable()
    {
        if (File.Exists(PlistPath))
        {
            File.Delete(PlistPath);
        }
    }
}
