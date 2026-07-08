namespace BardQuest.Updater;

// Shared download → gate → apply pipeline used by both the GUI (Install/Update) and the
// background tray updater. Persisting config is the caller's responsibility.
public static class ModUpdateApplier
{
    // Gate an already-extracted release against the install and, if compatible, copy the
    // mod DLLs and ensure the seam. Never writes config.
    public static ApplyResult GateAndApply(string extractedRoot, string? installTag, string managedDir)
    {
        string? extracted = ReleaseDownloader.ValidateExtracted(extractedRoot);
        if (extracted is null)
        {
            return new ApplyResult(ApplyOutcome.MissingFiles, null, null, installTag);
        }

        ModAssemblyInfo info = ModAssemblyReader.Read(Path.Combine(extracted, "BardQuest.Mod.dll"));
        if (YargCompat.Evaluate(info.YargTarget, installTag) == Compatibility.Incompatible)
        {
            return new ApplyResult(ApplyOutcome.Incompatible, info.ModVersion, info.YargTarget, installTag);
        }

        ModDeployer.Copy(extracted, managedDir);
        SeamPatcher.EnsurePatched(managedDir);
        return new ApplyResult(ApplyOutcome.Applied, info.ModVersion, info.YargTarget, installTag);
    }

    // Download + extract the release into tempDir, then gate + apply.
    public static async Task<ApplyResult> DownloadGateApplyAsync(
        HttpClient http, ReleaseInfo rel, string tempDir, string? installTag, string managedDir,
        CancellationToken ct = default)
    {
        _ = await ReleaseDownloader.DownloadAndExtractAsync(http, rel.AssetUrl, tempDir, ct);
        return GateAndApply(tempDir, installTag, managedDir);
    }
}
