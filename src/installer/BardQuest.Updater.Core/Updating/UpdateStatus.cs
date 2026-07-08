namespace BardQuest.Updater.Core.Updating;

public readonly record struct UpdateStatus(
    bool Installed, bool SeamMissing, bool ModUpdateAvailable, string? AvailableVersion);
