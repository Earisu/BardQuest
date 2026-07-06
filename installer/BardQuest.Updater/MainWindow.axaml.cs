using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace BardQuest.Updater;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void OnRescan(object? sender, RoutedEventArgs e) => _vm.RefreshInstalls();

    private async void OnChooseFolder(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> picked = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select your YARG Managed folder", AllowMultiple = false });

        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } path)
        {
            _ = _vm.SetManualManagedDir(path);
        }
    }

    private void OnInstall(object? sender, RoutedEventArgs e) => _vm.Install();

    private void OnRemove(object? sender, RoutedEventArgs e) => _vm.Remove();

    private async void OnCheck(object? sender, RoutedEventArgs e) => await _vm.CheckForUpdatesAsync();

    private async void OnUpdate(object? sender, RoutedEventArgs e) => await _vm.UpdateAsync();
}
