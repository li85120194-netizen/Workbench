using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Workbench;

public partial class CountdownWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private DateTime _target;
    private TimeSpan _pausedRemaining;
    private bool _running;

    public CountdownWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Refresh();
        SizeChanged += (_, _) => TimeText.FontSize = Math.Clamp(Math.Min(ActualWidth / 5.5, ActualHeight / 2.2), 24, 110);
    }

    public void Start(int seconds) { _pausedRemaining = TimeSpan.FromSeconds(seconds); _target = DateTime.Now + _pausedRemaining; _running = true; _timer.Start(); Refresh(); }
    public void Toggle()
    {
        if (_running) { _pausedRemaining = _target - DateTime.Now; _running = false; _timer.Stop(); }
        else if (_pausedRemaining.TotalSeconds > 0) { _target = DateTime.Now + _pausedRemaining; _running = true; _timer.Start(); }
    }
    public void Reset() { _timer.Stop(); _running = false; _pausedRemaining = TimeSpan.Zero; TimeText.Text = "00:00"; TimeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 166, 115)); }
    private void Refresh()
    {
        var remaining = _running ? _target - DateTime.Now : _pausedRemaining;
        if (remaining <= TimeSpan.Zero) { remaining = TimeSpan.Zero; _running = false; _timer.Stop(); TimeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)); }
        else TimeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 166, 115));
        TimeText.Text = remaining.TotalHours >= 1 ? $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}" : $"{remaining.Minutes:00}:{remaining.Seconds:00}";
    }
    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) Toggle(); else DragMove(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    protected override void OnClosed(EventArgs e) { _timer.Stop(); base.OnClosed(e); }
}
