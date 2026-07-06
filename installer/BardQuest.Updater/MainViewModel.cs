using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BardQuest.Updater;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly string _configPath;
    private readonly UpdaterConfig _config;

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
        return true;
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
