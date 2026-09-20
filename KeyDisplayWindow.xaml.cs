using System.Windows;
using System.Windows.Threading;

namespace Workbench;

public partial class KeyDisplayWindow : Window
{
    private readonly DispatcherTimer _hideTimer;

    public KeyDisplayWindow()
    {
        InitializeComponent();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); Hide(); };
        Loaded += (_, _) => PositionWindow();
    }

    public void ShowKey(string text)
    {
        KeyText.Text = text;
        Show();
        PositionWindow();
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void PositionWindow()
    {
        Left = SystemParameters.WorkArea.Right - ActualWidth - 28;
        Top = SystemParameters.WorkArea.Bottom - ActualHeight - 28;
    }
}
