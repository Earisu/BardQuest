using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using BardQuest.Updater.Core.AutoStart;
using BardQuest.Updater.Core.Config;
using BardQuest.Updater.Core.Discovery;
using BardQuest.Updater.Core.Patching;
using BardQuest.Updater.Core.Releases;
using BardQuest.Updater.Core.Updating;

namespace BardQuest.Updater;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly string _configPath;
    private readonly UpdaterConfig _config;

    private IAutoStartManager AutoStart =>
        field ??= AutoStartManager.ForCurrentOs(Environment.ProcessPath ?? "");

    public MainViewModel() : this(UpdaterConfig.DefaultPath()) { }

    public MainViewModel(string configPath)
    {
        _configPath = configPath;
        _config = UpdaterConfig.Load(configPath);
        RefreshInstalls();
    }

    public ObservableCollection<YargInstall> Installs { get; } = [];

    private YargInstall? _selectedInstall;
    public YargInstall? SelectedInstall
    {
        get => _selectedInstall;
        set
        {
            _selectedInstall = value;
            if (value is { } install)
            {
                _config.ManagedDir = install.ManagedDir;
                _config.Save(_configPath);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(ManagedDir));
            OnPropertyChanged(nameof(HasManagedDir));
            RefreshInstalledDisplay();
        }
    }

    public string? ManagedDir => _config.ManagedDir;
    public bool HasManagedDir => !string.IsNullOrEmpty(_config.ManagedDir) && YargLocator.IsValidManagedDir(_config.ManagedDir);

    public string Status
    {
        get;
        set { field = value; OnPropertyChanged(); }
    } = "";

    public void RefreshInstalls()
    {
        Installs.Clear();
        foreach (YargInstall install in YargLocator.Discover())
        {
            Installs.Add(install);
        }

        // Re-select the configured install if it's still present & valid; else clear a stale path.
        if (_config.ManagedDir is { } saved && YargLocator.IsValidManagedDir(saved))
        {
            _selectedInstall = FindByManagedDir(saved);
        }
        else if (_config.ManagedDir is not null)
        {
            _config.ManagedDir = null;
            _config.Save(_configPath);
        }

        Status = Installs.Count switch
        {
            0 => "No YARG installs found. Use \"Choose folder…\" to pick your YARG Managed folder.",
            1 => "Found 1 YARG install.",
            _ => $"Found {Installs.Count} YARG installs. Select which one to use.",
        };

        OnPropertyChanged(nameof(SelectedInstall));
        OnPropertyChanged(nameof(ManagedDir));
        OnPropertyChanged(nameof(HasManagedDir));
        RefreshInstalledDisplay();
    }

    // Sets a manually-picked Managed folder (from the folder picker) after validation.
    public bool SetManualManagedDir(string managedDir)
    {
        if (!YargLocator.IsValidManagedDir(managedDir))
        {
            Status = "That folder has no Assembly-CSharp.dll — not a YARG Managed folder.";
            return false;
        }

        _config.ManagedDir = managedDir;
        _config.Save(_configPath);
        _selectedInstall = null;
        Status = "Using manually selected folder.";
        OnPropertyChanged(nameof(ManagedDir));
        OnPropertyChanged(nameof(HasManagedDir));
        OnPropertyChanged(nameof(SelectedInstall));
        RefreshInstalledDisplay();
        return true;
    }

    public bool Busy
    {
        get;
        set { field = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanAct)); }
    }

    // Remove / Check are allowed whenever a valid Managed dir is selected and we're idle.
    public bool CanAct => HasManagedDir && !Busy;

    // Single opt-out setting: registers/removes the login item that runs the background
    // tray updater, and gates whether it auto-applies. Persisted in AutoStartEnabled.
    public bool AutoUpdateEnabled
    {
        get => _config.AutoStartEnabled;
        set
        {
            if (_config.AutoStartEnabled == value)
            {
                return;
            }

            _config.AutoStartEnabled = value;
            _config.Save(_configPath);
            try
            {
                if (value) { AutoStart.Enable(); } else { AutoStart.Disable(); }
            }
            catch (Exception ex)
            {
                Status = "Could not change the login item: " + ex.Message;
            }

            OnPropertyChanged();
        }
    }

    public string InstalledDisplay
    {
        get;
        private set { field = value; OnPropertyChanged(); }
    } = "(not installed)";

    public string CandidateDisplay
    {
        get;
        private set { field = value; OnPropertyChanged(); }
    } = "(unknown — click Check for updates)";

    private string? CurrentInstallTag() =>
        SelectedInstall?.Label
        ?? (HasManagedDir ? YargLocator.TagFromManagedDir(_config.ManagedDir!) : null);

    // Recomputes the installed-version display from the deployed mod DLL's baked markers.
    // Purely local (no network); safe to call from selection-change notify sites.
    public void RefreshInstalledDisplay()
    {
        ModAssemblyInfo installed = HasManagedDir
            ? ModAssemblyReader.Read(Path.Combine(_config.ManagedDir!, "BardQuest.Mod.dll"))
            : default;
        InstalledDisplay = BuildInstalledDisplay(installed);
        OnPropertyChanged(nameof(CanAct));
    }

    private static string BuildInstalledDisplay(ModAssemblyInfo installed)
    {
        if (installed.ModVersion is not { } iv)
        {
            return "(not installed)";
        }

        string target = installed.YargTarget is { } it ? $" (for YARG {it})" : "";
        return iv + target;
    }

    public async Task InstallAsync()
    {
        if (!CanAct)
        {
            return;
        }

        Busy = true;
        Status = "Fetching latest mod release…";
        string? temp = null;
        try
        {
            string managed = _config.ManagedDir!;
            ReleaseInfo? latest = await FetchLatestModAsync();
            if (latest is not { } rel)
            {
                Status = "No mod release found on GitHub.";
                return;
            }

            CandidateDisplay = rel.Tag;
            // Install always writes DLLs into the Managed dir and patches the seam, so a
            // running YARG blocks it even on a first (unpatched) install — not just re-installs.
            if (IsYargRunning())
            {
                Status = "Close YARG before installing, then click Install again.";
                return;
            }

            temp = NewTempDir("bq-install-");
            string? version = await DownloadGateApplyAsync(rel, managed, temp, "Install");
            if (version is not null)
            {
                if (!_config.AutoStartEnabled)
                {
                    AutoUpdateEnabled = true; // default ON on first install; registers the login item
                }

                Status = $"Installed {version}.";
            }
        }
        catch (Exception ex)
        {
            Status = "Install failed: " + ex.Message;
        }
        finally
        {
            CleanupTemp(temp);
            Busy = false;
            RefreshInstalledDisplay();
        }
    }

    // Download rel into temp, gate the downloaded build's YARG target against the selected
    // install, and apply. Returns the applied version, or null if it aborted (Status set to
    // the reason). Temp is cleaned by the caller.
    private async Task<string?> DownloadGateApplyAsync(ReleaseInfo rel, string managed, string temp, string abortVerb)
    {
        Status = $"Downloading mod {rel.Tag}…";
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BardQuest-Updater");
        string? installTag = CurrentInstallTag();

        ApplyResult result = await ModUpdateApplier.DownloadGateApplyAsync(http, rel, temp, installTag, managed);
        switch (result.Outcome)
        {
            case ApplyOutcome.MissingFiles:
                Status = "Downloaded release did not contain the expected mod files.";
                return null;
            case ApplyOutcome.Incompatible:
                Status = $"⚠ This mod targets YARG {result.ModTarget}; selected install is {installTag}. {abortVerb} aborted.";
                return null;
            default:
                string version = result.Version ?? rel.Tag;
                _config.InstalledVersion = version;
                _config.LastCheckUtc = DateTime.UtcNow;
                _config.Save(_configPath);
                return version;
        }
    }

    private static async Task<ReleaseInfo?> FetchLatestModAsync()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BardQuest-Updater");
        return await ReleaseClient.FetchLatestReleaseAsync(
            http, ReleaseClient.DefaultOwner, ReleaseClient.DefaultRepo, ReleaseClient.ModTagPrefix);
    }

    private static string NewTempDir(string prefix) =>
        Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid());

    private static void CleanupTemp(string? temp)
    {
        if (temp is not null && Directory.Exists(temp))
        {
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { /* best-effort cleanup */ }
        }
    }

    public void Remove()
    {
        RunAction("Removing…", managed =>
        {
            SeamPatcher.Restore(managed);
            ModDeployer.Delete(managed);
            _config.InstalledVersion = null;
            _config.Save(_configPath);
            Status = "Removed BardQuest.";
        });
    }

    // Passive check run when the window opens: fetch the latest published release and show
    // what's available. Unlike the "Check for updates" button it never mutates the install
    // (no seam re-apply) and works even before a YARG install is selected.
    public async Task CheckOnLaunchAsync()
    {
        if (Busy)
        {
            return;
        }

        Busy = true;
        Status = "Checking for the latest BardQuest…";
        try
        {
            ReleaseInfo? latest = await FetchLatestModAsync();
            CandidateDisplay = latest?.Tag ?? "(none published)";
            if (latest is { } rel)
            {
                if (HasManagedDir)
                {
                    bool seamPresent = SeamPatcher.IsManagedDirPatched(_config.ManagedDir!);
                    UpdateStatus status = UpdateEvaluator.Evaluate(_config, latest, seamPresent);
                    if (status.ModUpdateAvailable)
                    {
                        Status = $"Update available: {status.AvailableVersion}. Close YARG, then click Update.";
                    }
                    else
                    {
                        Status = status.Installed ? "BardQuest is up to date." : $"BardQuest {rel.Tag} is available — click Install.";
                    }
                }
                else
                {
                    Status = $"BardQuest {rel.Tag} is available — select your YARG install, then click Install.";
                }
            }
            else
            {
                Status = "No BardQuest release found on GitHub yet.";
            }

            _config.LastCheckUtc = DateTime.UtcNow;
            _config.Save(_configPath);
        }
        catch (Exception ex)
        {
            Status = "Update check failed: " + ex.Message;
        }
        finally { Busy = false; RefreshInstalledDisplay(); }
    }

    public async Task CheckForUpdatesAsync()
    {
        if (!CanAct)
        {
            return;
        }

        Busy = true;
        Status = "Checking for updates…";
        try
        {
            string managed = _config.ManagedDir!;
            bool seamPresent = SeamPatcher.IsManagedDirPatched(managed);
            ReleaseInfo? latest = await FetchLatestModAsync();
            CandidateDisplay = latest?.Tag ?? "(none published)";
            UpdateStatus status = UpdateEvaluator.Evaluate(_config, latest, seamPresent);

            if (status.SeamMissing)
            {
                // The YARC launcher replaced the game DLL and wiped our seam — re-apply safely
                // (EnsurePatched discards the now-stale backup so we don't revert the new YARG build).
                SeamPatcher.EnsurePatched(managed);
                Status = "Re-applied BardQuest patch (YARG had been updated).";
            }
            else
            {
                Status = status.ModUpdateAvailable
                    ? $"Update available: {status.AvailableVersion}. Close YARG, then click Update."
                    : status.Installed ? "BardQuest is up to date." : "BardQuest is not installed.";
            }

            _config.LastCheckUtc = DateTime.UtcNow;
            _config.Save(_configPath);
        }
        catch (Exception ex)
        {
            Status = "Update check failed: " + ex.Message;
        }
        finally { Busy = false; RefreshInstalledDisplay(); }
    }

    public async Task UpdateAsync()
    {
        if (!CanAct)
        {
            return;
        }

        Busy = true;
        Status = "Checking for updates…";
        string? temp = null;
        try
        {
            string managed = _config.ManagedDir!;
            ReleaseInfo? latest = await FetchLatestModAsync();
            if (latest is not { } rel)
            {
                Status = "No mod release found on GitHub.";
                return;
            }

            CandidateDisplay = rel.Tag;
            bool newer = _config.InstalledVersion is { } installed
                && SemVer.TryParse(rel.Tag, out _) && SemVer.TryParse(installed, out _)
                && SemVer.IsNewer(rel.Tag, installed);

            if (!newer)
            {
                Status = "BardQuest is up to date.";
                return;
            }

            if (SeamPatcher.IsManagedDirPatched(managed) && IsYargRunning())
            {
                Status = "Close YARG before updating, then click Update again.";
                return;
            }

            temp = NewTempDir("bq-update-");
            string? version = await DownloadGateApplyAsync(rel, managed, temp, "Update");
            if (version is not null)
            {
                Status = $"Updated to {version}.";
            }
        }
        catch (Exception ex)
        {
            Status = "Update failed: " + ex.Message;
        }
        finally
        {
            CleanupTemp(temp);
            Busy = false;
            RefreshInstalledDisplay();
        }
    }

    // True if a YARG process is currently running (patching while it runs would fail/corrupt).
    private static bool IsYargRunning() => YargProcess.IsRunning();

    private void RunAction(string runningStatus, Action<string> action)
    {
        if (!CanAct)
        {
            return;
        }

        Busy = true;
        Status = runningStatus;
        try
        {
            action(_config.ManagedDir!);
        }
        catch (Exception ex)
        {
            Status = "Failed: " + ex.Message;
        }
        finally { Busy = false; RefreshInstalledDisplay(); }
    }

    private YargInstall? FindByManagedDir(string managedDir)
    {
        foreach (YargInstall install in Installs)
        {
            if (install.ManagedDir == managedDir)
            {
                return install;
            }
        }

        return null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
