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
    private bool _hasStarted;

    public CountdownWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Refresh();
        SizeChanged += (_, _) => TimeText.FontSize = Math.Clamp(Math.Min(ActualWidth / 5.5, ActualHeight / 2.2), 24, 110);
    }

    public void Start(int seconds) { _pausedRemaining = TimeSpan.FromSeconds(seconds); _target = DateTime.Now + _pausedRemaining; _running = true; _hasStarted = true; _timer.Start(); Refresh(); }
    public void Toggle()
    {
        if (_running) { _pausedRemaining = _target - DateTime.Now; _running = false; _timer.Stop(); }
        else if (_hasStarted) { _target = DateTime.Now + _pausedRemaining; _running = true; _timer.Start(); }
    }
    public void Reset() { _timer.Stop(); _running = false; _hasStarted = false; _pausedRemaining = TimeSpan.Zero; TimeText.Text = "00:00"; TimeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 166, 115)); }
    public void SetBackground(System.Windows.Media.Color color, double opacity)
    {
        TimerBackground.Background = opacity <= 0 ? System.Windows.Media.Brushes.Transparent : new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)Math.Round(255 * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B));
        TimerBackground.BorderBrush = opacity <= 0 ? System.Windows.Media.Brushes.Transparent : new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)Math.Round(60 * Math.Clamp(opacity, 0, 1)), 0, 0, 0));
    }
    private void Refresh()
    {
        var remaining = _running ? _target - DateTime.Now : _pausedRemaining;
        bool overtime = remaining < TimeSpan.Zero;
        TimeText.Foreground = new SolidColorBrush(overtime ? System.Windows.Media.Color.FromRgb(231, 76, 60) : System.Windows.Media.Color.FromRgb(23, 166, 115));
        var shown = remaining.Duration();
        string value = shown.TotalHours >= 1 ? $"{(int)shown.TotalHours:00}:{shown.Minutes:00}:{shown.Seconds:00}" : $"{shown.Minutes:00}:{shown.Seconds:00}";
        TimeText.Text = overtime ? $"-{value}" : value;
    }
    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) Toggle(); else DragMove(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    protected override void OnClosed(EventArgs e) { _timer.Stop(); base.OnClosed(e); }
}
