using System.Security;

namespace BardQuest.Updater;

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
