using System.Text.Json;

using BardQuest.Updater.Core.Discovery;

namespace BardQuest.Updater.Core.Config;

// Small persisted state for the updater: which YARG install is targeted, what
// mod version is installed, whether auto-start is on, and when we last checked.
public sealed class UpdaterConfig
{
    public string? ManagedDir { get; set; }
    public string? InstalledVersion { get; set; }
    public bool AutoStartEnabled { get; set; }
    public DateTime? LastCheckUtc { get; set; }

    // Newest release already downloaded + evaluated by the background updater, so a
    // held (waiting-for-YARG) or incompatible release is not re-downloaded every poll.
    public string? HeldVersion { get; set; }

    private static readonly JsonSerializerOptions s_options = new() { WriteIndented = true };

    public static string DefaultPath() =>
        Path.Combine(PlatformPaths.LocalAppDataRoot(), "BardQuest", "updater-config.json");

    public static UpdaterConfig Load(string path)
    {
        try
        {
            return !File.Exists(path)
                ? new UpdaterConfig()
                : JsonSerializer.Deserialize<UpdaterConfig>(File.ReadAllText(path)) ?? new UpdaterConfig();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new UpdaterConfig();
        }
    }

    public void Save(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, s_options));
    }
}
