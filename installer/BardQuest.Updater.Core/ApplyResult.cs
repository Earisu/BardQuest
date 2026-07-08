namespace BardQuest.Updater;

public readonly record struct ApplyResult(
    ApplyOutcome Outcome, string? Version, string? ModTarget, string? InstallTag);
