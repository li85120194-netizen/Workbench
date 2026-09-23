using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Workbench;

public partial class PomodoroFloatWindow : Window
{
    private bool _allowClose;
    public event Action? ToggleRequested;
    public event Action? SkipRequested;

    public PomodoroFloatWindow() => InitializeComponent();

    public void UpdateState(string stage, string time, bool running)
    {
        StageText.Text = stage;
        TimeText.Text = time;
        ToggleButton.Content = running ? "暂停" : "继续";
    }

    public void Shutdown()
    {
        _allowClose = true;
        Close();
    }

    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e) => ToggleRequested?.Invoke();
    private void Skip_Click(object sender, RoutedEventArgs e) => SkipRequested?.Invoke();
    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }
}
