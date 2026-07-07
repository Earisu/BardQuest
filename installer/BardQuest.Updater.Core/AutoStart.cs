using System.Runtime.Versioning;
using System.Security;

namespace BardQuest.Updater;

// Manages the OS "run at login" item for the background tray updater.
public interface IAutoStartManager
{
    bool IsEnabled();
    void Enable();   // register a login item that launches "<exe> --tray"
    void Disable();  // remove it
}

// Pure builders for the platform-specific login-item payloads (unit-tested).
public static class AutoStartCommand
{
    public const string LaunchAgentLabel = "com.bardquest.updater";
    public const string RunKeyValueName = "BardQuest Updater";

    public static string BuildRunKeyValue(string execPath) => $"\"{execPath}\" --tray";

    public static string BuildLaunchAgentPlist(string execPath, string label)
    {
        string safeLabel = SecurityElement.Escape(label);
        string safePath = SecurityElement.Escape(execPath);
        return $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key><string>{safeLabel}</string>
          <key>ProgramArguments</key>
          <array>
            <string>{safePath}</string>
            <string>--tray</string>
          </array>
          <key>RunAtLoad</key><true/>
        </dict>
        </plist>
        """;
    }
}

public static class AutoStartManager
{
    // Returns the right manager for the running OS; a no-op on unsupported platforms.
    public static IAutoStartManager ForCurrentOs(string execPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsAutoStartManager(execPath);
        }

        if (OperatingSystem.IsMacOS())
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
            return new MacAutoStartManager(execPath, dir);
        }

        return new NoOpAutoStartManager();
    }
}

public sealed class NoOpAutoStartManager : IAutoStartManager
{
    public bool IsEnabled() => false;
    public void Enable() { }
    public void Disable() { }
}

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

[SupportedOSPlatform("windows")]
public sealed class WindowsAutoStartManager(string execPath) : IAutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(AutoStartCommand.RunKeyValueName) is not null;
    }

    public void Enable()
    {
        using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(AutoStartCommand.RunKeyValueName, AutoStartCommand.BuildRunKeyValue(execPath));
    }

    public void Disable()
    {
        using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(AutoStartCommand.RunKeyValueName, throwOnMissingValue: false);
    }
}
