using BardQuest.Updater.Core.Compatibility;
using BardQuest.Updater.Core.Config;
using BardQuest.Updater.Core.Discovery;
using BardQuest.Updater.Core.Patching;
using BardQuest.Updater.Core.Releases;
using BardQuest.Updater.Core.Updating;

namespace BardQuest.Updater;

// Background auto-update loop for --tray mode. Polls GitHub for a newer mod release and,
// when the user has auto-update enabled, applies it as soon as it is safe (YARG closed +
// version-compatible). Surfaces status through the supplied tooltip callback. All decision
// logic lives in Core (AutoUpdateDecider / ModUpdateApplier); this class is the plumbing.
public sealed class BackgroundUpdateService(string configPath, Action<string> setTooltip)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan YargExitWatchInterval = TimeSpan.FromSeconds(45);
    private const string NeedsAttentionTooltip = "BardQuest — update needs attention (open the updater)";

    private readonly string _configPath = configPath;
    private readonly Action<string> _setTooltip = setTooltip;
    private readonly CancellationTokenSource _cts = new();

    // In-memory pending download, kept while waiting for YARG to exit.
    private string? _pendingDir;
    private string? _pendingVersion;
    private bool _watching;

    // Serializes ApplyPending so the fast-watch loop and a manual "Check now" can never
    // run the file-copy/seam-patch against the same managed dir concurrently.
    private readonly Lock _applyLock = new();

    public void Start() => _ = RunLoopAsync(_cts.Token);

    public void Stop()
    {
        _cts.Cancel();
        _cts.Dispose();
        CleanupPending();
    }

    // Invoked by the tray "Check for updates now" menu item.
    public async Task CheckNowAsync()
    {
        try { await RunOnceAsync(_cts.Token); }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            await RunOnceAsync(ct);
            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                await RunOnceAsync(ct);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            var config = UpdaterConfig.Load(_configPath);
            string? managed = config.ManagedDir;
            if (managed is null || !YargLocator.IsValidManagedDir(managed))
            {
                _setTooltip("BardQuest — no YARG install selected");
                return;
            }

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("BardQuest-Updater");
            ReleaseInfo? latest = await ReleaseClient.FetchLatestReleaseAsync(
                http, ReleaseClient.DefaultOwner, ReleaseClient.DefaultRepo, ReleaseClient.ModTagPrefix, ct);

            bool seamPresent = SeamPatcher.IsManagedDirPatched(managed);
            UpdateStatus status = UpdateEvaluator.Evaluate(config, latest, seamPresent);
            config.LastCheckUtc = DateTime.UtcNow;

            bool compatible = true;
            if (status.ModUpdateAvailable && latest is { } rel)
            {
                // (Re)download only if we do not already hold this exact version on disk.
                if (_pendingVersion != rel.Tag || _pendingDir is null || !Directory.Exists(_pendingDir))
                {
                    CleanupPending();
                    string temp = Path.Combine(Path.GetTempPath(), "bq-auto-" + Guid.NewGuid());
                    _ = await ReleaseDownloader.DownloadAndExtractAsync(http, rel.AssetUrl, temp, ct);
                    _pendingDir = temp;
                    _pendingVersion = rel.Tag;
                    config.HeldVersion = rel.Tag;
                }

                string? extracted = ReleaseDownloader.ValidateExtracted(_pendingDir);
                ModAssemblyInfo info = extracted is null
                    ? default
                    : ModAssemblyReader.Read(Path.Combine(extracted, "BardQuest.Mod.dll"));
                string? installTag = YargLocator.TagFromManagedDir(managed);
                compatible = YargCompat.Evaluate(info.YargTarget, installTag) != Compatibility.Incompatible;
            }

            config.Save(_configPath);

            AutoUpdateAction action = AutoUpdateDecider.Decide(
                status, YargProcess.IsRunning(), compatible, config.AutoStartEnabled);

            switch (action)
            {
                case AutoUpdateAction.ApplyNow:
                    ApplyPending(managed);
                    break;
                case AutoUpdateAction.WaitForYargExit:
                    _setTooltip($"BardQuest — update {_pendingVersion} will apply when YARG closes");
                    StartYargExitWatch(managed, ct);
                    break;
                case AutoUpdateAction.NeedsAttention:
                    _setTooltip(NeedsAttentionTooltip);
                    break;
                case AutoUpdateAction.None:
                    _setTooltip(status.Installed
                        ? $"BardQuest — up to date ({config.InstalledVersion})"
                        : "BardQuest");
                    break;
                default:
                    break;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _setTooltip("BardQuest — update check failed");
            System.Diagnostics.Debug.WriteLine("BardQuest auto-update: " + ex);
        }
    }

    private void StartYargExitWatch(string managed, CancellationToken ct)
    {
        if (_watching)
        {
            return;
        }

        _watching = true;
        _ = WatchAsync();

        async Task WatchAsync()
        {
            try
            {
                using var timer = new PeriodicTimer(YargExitWatchInterval);
                while (await timer.WaitForNextTickAsync(ct))
                {
                    if (!YargProcess.IsRunning())
                    {
                        ApplyPending(managed);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) { /* shutting down */ }
            finally { _watching = false; }
        }
    }

    // Apply the held download; persist the new installed version. Best-effort — surfaces
    // NeedsAttention on any non-Applied outcome and leaves the prior install intact.
    private void ApplyPending(string managed)
    {
        lock (_applyLock)
        {
            string? extracted = _pendingDir is null ? null : ReleaseDownloader.ValidateExtracted(_pendingDir);
            if (extracted is null)
            {
                if (_pendingDir is not null)
                {
                    // Held download exists but is invalid/corrupt — discard so the next poll re-fetches.
                    CleanupPending();
                    _setTooltip(NeedsAttentionTooltip);
                }

                return;
            }

            try
            {
                string? installTag = YargLocator.TagFromManagedDir(managed);
                ApplyResult result = ModUpdateApplier.GateAndApply(extracted, installTag, managed);
                var config = UpdaterConfig.Load(_configPath);
                if (result.Outcome == ApplyOutcome.Applied)
                {
                    config.InstalledVersion = result.Version ?? _pendingVersion;
                    config.HeldVersion = null;
                    config.Save(_configPath);
                    _setTooltip($"BardQuest — updated to {config.InstalledVersion}");
                    CleanupPending();
                }
                else
                {
                    _setTooltip(NeedsAttentionTooltip);
                }
            }
            catch (Exception ex)
            {
                _setTooltip(NeedsAttentionTooltip);
                System.Diagnostics.Debug.WriteLine("BardQuest auto-apply: " + ex);
            }
        }
    }

    private void CleanupPending()
    {
        if (_pendingDir is not null && Directory.Exists(_pendingDir))
        {
            try { Directory.Delete(_pendingDir, recursive: true); } catch (IOException) { /* best-effort */ }
        }

        _pendingDir = null;
        _pendingVersion = null;
    }
}
