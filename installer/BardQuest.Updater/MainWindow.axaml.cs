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
}
