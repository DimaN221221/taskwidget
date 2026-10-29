using System.Diagnostics;
using System.Windows;

namespace TaskWidget;

public partial class App : Application
{
    // One widget at a time: two copies would overwrite each other's tasks.json.
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        Updater.FinishPrevious(e.Args); // started by an update: let the old version quit first

        _singleInstance = new Mutex(true, @"Local\TaskWidget.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            BringExistingToFront();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void BringExistingToFront()
    {
        using var current = Process.GetCurrentProcess();
        foreach (var other in Process.GetProcessesByName(current.ProcessName))
        {
            if (other.Id != current.Id && other.MainWindowHandle != IntPtr.Zero)
                NativeMethods.Activate(other.MainWindowHandle);
            other.Dispose();
        }
    }
}
