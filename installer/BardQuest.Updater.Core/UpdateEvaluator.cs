namespace BardQuest.Updater;

public readonly record struct UpdateStatus(
    bool Installed, bool SeamMissing, bool ModUpdateAvailable, string? AvailableVersion);

// Pure decision: given persisted state, the latest release, and whether the seam
// is still present in the target install, what should the updater surface/do?
public static class UpdateEvaluator
{
    public static UpdateStatus Evaluate(UpdaterConfig config, ReleaseInfo? latest, bool seamPresentInManagedDir)
    {
        bool installed = config.InstalledVersion is not null;
        bool seamMissing = installed && !seamPresentInManagedDir;

        bool updateAvailable = false;
        if (installed && latest is { } rel
            && SemVer.TryParse(rel.Tag, out _)
            && SemVer.TryParse(config.InstalledVersion!, out _))
        {
            updateAvailable = SemVer.IsNewer(rel.Tag, config.InstalledVersion!);
        }

        return new UpdateStatus(installed, seamMissing, updateAvailable, latest?.Tag);
    }
}
