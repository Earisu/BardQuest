namespace BardQuest.Updater.Core.Updating;

public readonly record struct ApplyResult(
    ApplyOutcome Outcome, string? Version, string? ModTarget, string? InstallTag);
