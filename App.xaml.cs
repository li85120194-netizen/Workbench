using System.Threading;
using System.Windows;

namespace Workbench;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\Workbench_5DE4AD7B_1647_48C5_87E0_9013F7C68BB9";
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("工具箱已经在运行。", "工具箱", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }
        base.OnStartup(e);
        MainWindow = new MainWindow(); MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _singleInstanceMutex?.ReleaseMutex(); } catch (ApplicationException) { }
        _singleInstanceMutex?.Dispose(); base.OnExit(e);
    }
}
