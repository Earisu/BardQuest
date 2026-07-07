using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;

namespace BardQuest.Updater;

public partial class App : Application
{
    private TrayIcon? _tray;
    private MainWindow? _window;
    private BackgroundUpdateService? _background;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Program.TrayMode)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _tray = BuildTrayIcon(desktop);

                desktop.ShutdownRequested += (_, _) => _background?.Stop();
                _background = new BackgroundUpdateService(
                    UpdaterConfig.DefaultPath(),
                    text => Dispatcher.UIThread.Post(() =>
                    {
                        _tray?.ToolTipText = text;
                    }));
                _background.Start();
            }
            else
            {
                desktop.MainWindow = new MainWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private TrayIcon BuildTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        using Stream iconStream = AssetLoader.Open(
            new Uri("avares://BardQuest.Updater/Assets/BardQuest-logo.png"));
        var tray = new TrayIcon
        {
            Icon = new WindowIcon(iconStream),
            ToolTipText = "BardQuest",
            IsVisible = true,
        };

        var open = new NativeMenuItem("Open BardQuest Updater");
        open.Click += (_, _) => ShowWindow();
        var check = new NativeMenuItem("Check for updates now");
        check.Click += async (_, _) => { if (_background is not null) { await _background.CheckNowAsync(); } };
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => desktop.Shutdown();

        tray.Menu = [open, check, new NativeMenuItemSeparator(), quit];
        tray.Clicked += (_, _) => ShowWindow();
        return tray;
    }

    // Lazily create the single window; closing it hides back to the tray.
    private void ShowWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow();
            _window.Closing += (_, e) =>
            {
                e.Cancel = true;
                _window.Hide();
            };
        }

        _window.Show();
        _window.Activate();
    }
}
