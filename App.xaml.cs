using System.Threading;
using System.Windows;
using HexaDock.Services;

namespace HexaDock;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length == 2 && e.Args[0].Equals("--watchdog", StringComparison.OrdinalIgnoreCase) && int.TryParse(e.Args[1], out var processId))
        {
            DesktopIconService.Watch(processId);
            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--restore-icons", StringComparer.OrdinalIgnoreCase))
        {
            DesktopIconService.SetVisible(true);
            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            DesktopIndex.RunSelfTest();
            Shutdown(0);
            return;
        }

        _singleInstance = new Mutex(true, "Local\\HexaDock.SingleInstance", out var created);
        if (!created)
        {
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
