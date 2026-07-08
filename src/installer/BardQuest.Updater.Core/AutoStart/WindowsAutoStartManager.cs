using System.Runtime.Versioning;

namespace BardQuest.Updater.Core.AutoStart;

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
