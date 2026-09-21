using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Workbench;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312, HotkeyHighlight = 1001, HotkeyAnnotation = 1002;
    private OverlayWindow? _overlay;
    private AnnotationWindow? _annotation;
    private KeyboardDisplayService? _keyboardDisplay;
    private CountdownWindow? _countdown;
    private HwndSource? _source;
    private System.Windows.Media.Color _selectedColor = Colors.Red;
    private System.Windows.Media.Color _timerBackgroundColor = Colors.White;
    private Slider? _timerBackgroundOpacity;
    private TextBlock? _timerBackgroundOpacityText;
    private Border? _timerBackgroundPreview;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => { RefreshOrganizerStatus(); SwapTimerAndMouseNavigation(); AddTimerBackgroundControls(); };
    }

    private void SwapTimerAndMouseNavigation()
    {
        if (MouseNav.Parent is not StackPanel panel) return;
        panel.Children.Remove(TimerNav);
        panel.Children.Insert(0, TimerNav);
    }

    private void AddTimerBackgroundControls()
    {
        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(4, 14, 0, 0) };
        row.Children.Add(new TextBlock { Text = "背景", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var colorButton = new System.Windows.Controls.Button { Content = "选择颜色…", Height = 32, Padding = new Thickness(14, 0, 14, 0) };
        colorButton.Click += TimerBackgroundButton_Click;
        row.Children.Add(colorButton);
        _timerBackgroundPreview = new Border { Width = 34, Height = 24, CornerRadius = new CornerRadius(4), Background = System.Windows.Media.Brushes.White, BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 210, 220)), BorderThickness = new Thickness(1), Margin = new Thickness(10, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_timerBackgroundPreview);
        row.Children.Add(new TextBlock { Text = "不透明度", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _timerBackgroundOpacity = new Slider { Width = 150, Minimum = 0, Maximum = 100, Value = 0 };
        _timerBackgroundOpacity.ValueChanged += TimerBackgroundOpacity_Changed;
        row.Children.Add(_timerBackgroundOpacity);
        _timerBackgroundOpacityText = new TextBlock { Text = "0%", Width = 42, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_timerBackgroundOpacityText);
        TimerPanel.Children.Add(row);
        TimerPanel.Children.Add(new TextBlock { Text = "默认背景完全透明。倒计时结束后继续负计时，并显示红色数字。", Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(104, 115, 134)), Margin = new Thickness(4, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WindowMessageHook);
        RegisterHotKey(handle, HotkeyHighlight, 0x4000, 0x70);
        RegisterHotKey(handle, HotkeyAnnotation, 0x4000, 0x71);
        if (AutoUpdateCheck.IsChecked == true) _ = CheckForUpdatesAsync(false);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey) return IntPtr.Zero;
        if (wParam.ToInt32() == HotkeyHighlight) ToggleHighlight();
        else if (wParam.ToInt32() == HotkeyAnnotation) ToggleAnnotation();
        handled = true;
        return IntPtr.Zero;
    }

    private void ShowPage(UIElement page, System.Windows.Controls.Button nav)
    {
        foreach (var item in new[] { MousePage, TimerPage, OrganizerPage, SettingsPage }) item.Visibility = Visibility.Collapsed;
        foreach (var item in new[] { MouseNav, TimerNav, OrganizerNav, SettingsNav }) item.Background = System.Windows.Media.Brushes.Transparent;
        page.Visibility = Visibility.Visible;
        nav.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 56, 84));
    }

    private void MouseNav_Click(object sender, RoutedEventArgs e) => ShowPage(MousePage, MouseNav);
    private void TimerNav_Click(object sender, RoutedEventArgs e) => ShowPage(TimerPage, TimerNav);
    private void OrganizerNav_Click(object sender, RoutedEventArgs e) { ShowPage(OrganizerPage, OrganizerNav); RefreshOrganizerStatus(); }
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, SettingsNav);

    private void EnableButton_Click(object sender, RoutedEventArgs e) => EnableHighlight();
    private void DisableButton_Click(object sender, RoutedEventArgs e) => DisableHighlight();
    private void AnnotationButton_Click(object sender, RoutedEventArgs e) => ToggleAnnotation();
    private void ToggleHighlight() { if (_overlay is null) EnableHighlight(); else DisableHighlight(); }

    private void EnableHighlight()
    {
        if (_overlay is null) { _overlay = new OverlayWindow(); ApplyOverlaySettings(); _overlay.Show(); }
        SetStatus(true);
    }

    private void ToggleAnnotation()
    {
        if (_annotation is null)
        {
            _annotation = new AnnotationWindow(_selectedColor);
            _annotation.Closed += (_, _) => { _annotation = null; AnnotationButton.Content = "屏幕标注  F2"; };
            _annotation.Show(); AnnotationButton.Content = "关闭标注  F2";
        }
        else { _annotation.Close(); _annotation = null; AnnotationButton.Content = "屏幕标注  F2"; }
    }

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (SizeText is not null && SizeSlider is not null) SizeText.Text = $"{(int)SizeSlider.Value} px";
        ApplyOverlaySettings();
    }

    private void ApplyOverlaySettings()
    {
        if (_overlay is null || ThemeBox is null || ShapeBox is null || PositionBox is null || SizeSlider is null) return;
        _overlay.Configure((int)SizeSlider.Value, ThemeBox.SelectedIndex, ShapeBox.SelectedIndex, PositionBox.SelectedIndex, _selectedColor, DynamicCheck.IsChecked == true);
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(_selectedColor.R, _selectedColor.G, _selectedColor.B) };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        _selectedColor = System.Windows.Media.Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
        ColorPreview.Background = new SolidColorBrush(_selectedColor); ApplyOverlaySettings();
    }

    private void KeyboardCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (KeyboardCheck.IsChecked == true) { _keyboardDisplay ??= new KeyboardDisplayService(); _keyboardDisplay.Start(); }
        else { _keyboardDisplay?.Dispose(); _keyboardDisplay = null; }
    }

    private bool TryReadTime(out int totalSeconds)
    {
        int.TryParse(TimerMinutes.Text, out int minutes); int.TryParse(TimerSeconds.Text, out int seconds);
        totalSeconds = Math.Max(0, minutes * 60 + seconds);
        if (totalSeconds > 0) return true;
        System.Windows.MessageBox.Show("请输入大于 0 的倒计时时长。", "工具箱", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private void StartTimer_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTime(out int seconds)) return;
        _countdown ??= new CountdownWindow();
        _countdown.Closed += (_, _) => _countdown = null;
        ApplyTimerBackground(); _countdown.Show(); _countdown.Activate(); _countdown.Start(seconds);
    }
    private void ToggleTimer_Click(object sender, RoutedEventArgs e) { _countdown ??= new CountdownWindow(); if (!_countdown.IsVisible) _countdown.Show(); _countdown.Toggle(); }
    private void ResetTimer_Click(object sender, RoutedEventArgs e) => _countdown?.Reset();

    private void TimerBackgroundButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(_timerBackgroundColor.R, _timerBackgroundColor.G, _timerBackgroundColor.B) };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        _timerBackgroundColor = System.Windows.Media.Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
        if (_timerBackgroundPreview is not null) _timerBackgroundPreview.Background = new SolidColorBrush(_timerBackgroundColor);
        ApplyTimerBackground();
    }

    private void TimerBackgroundOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_timerBackgroundOpacityText is not null) _timerBackgroundOpacityText.Text = $"{(int)e.NewValue}%";
        ApplyTimerBackground();
    }

    private void ApplyTimerBackground() => _countdown?.SetBackground(_timerBackgroundColor, (_timerBackgroundOpacity?.Value ?? 0) / 100.0);

    private void PreviewOrganize_Click(object sender, RoutedEventArgs e)
    {
        OrganizerPreview.ItemsSource = DesktopOrganizerService.GetCandidates().Select(p => p.Name);
        RefreshOrganizerStatus();
    }

    private void Organize_Click(object sender, RoutedEventArgs e)
    {
        var files = DesktopOrganizerService.GetCandidates();
        if (files.Count == 0) { System.Windows.MessageBox.Show("桌面没有可整理的办公文档。", "工具箱"); return; }
        if (System.Windows.MessageBox.Show($"将把 {files.Count} 个文档移动到今天的收纳文件夹。是否继续？", "确认桌面收纳", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var result = DesktopOrganizerService.Organize();
        System.Windows.MessageBox.Show($"已移动 {result.Moved} 项，失败 {result.Failed} 项。", "工具箱");
        OrganizerPreview.ItemsSource = null; RefreshOrganizerStatus();
    }

    private void UndoOrganize_Click(object sender, RoutedEventArgs e)
    {
        var result = DesktopOrganizerService.UndoLast();
        System.Windows.MessageBox.Show(result.Message, "工具箱"); RefreshOrganizerStatus();
    }

    private void RefreshOrganizerStatus()
    {
        var desktop = DesktopOrganizerService.DesktopPath;
        var target = DesktopOrganizerService.TodayFolder;
        OrganizerStatus.Text = $"桌面：{desktop}\n今日目标：{target}\n待整理文档：{DesktopOrganizerService.GetCandidates().Count} 项";
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e) => await CheckForUpdatesAsync(true);
    private async Task CheckForUpdatesAsync(bool showResult)
    {
        try
        {
            var result = await UpdateService.CheckAsync();
            if (!result.UpdateAvailable) { if (showResult) System.Windows.MessageBox.Show("当前已经是最新版本。", "工具箱"); return; }
            if (System.Windows.MessageBox.Show($"发现工具箱 {result.Version}，是否下载并安装？", "发现更新", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                await UpdateService.DownloadAndInstallAsync(result);
        }
        catch (Exception ex) { if (showResult) System.Windows.MessageBox.Show($"检查更新失败：{ex.Message}", "工具箱", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void SetStatus(bool enabled)
    {
        StatusText.Text = enabled ? "当前状态：已启用" : "当前状态：未启用";
        StatusDot.Fill = new SolidColorBrush(enabled ? System.Windows.Media.Color.FromRgb(52, 168, 83) : System.Windows.Media.Color.FromRgb(154, 160, 166));
        EnableButton.IsEnabled = !enabled; DisableButton.IsEnabled = enabled;
    }

    private void DisableHighlight() { _overlay?.Close(); _overlay = null; SetStatus(false); }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        DisableHighlight(); _annotation?.Close(); _keyboardDisplay?.Dispose(); _countdown?.Close();
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero) { UnregisterHotKey(handle, HotkeyHighlight); UnregisterHotKey(handle, HotkeyAnnotation); }
        _source?.RemoveHook(WindowMessageHook);
    }

    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
