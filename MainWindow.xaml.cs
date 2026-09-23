using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Workbench;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int WmClipboardUpdate = 0x031D;
    private const int HotkeyHighlight = 1001;
    private const int HotkeyAnnotation = 1002;

    private readonly ToolboxState _state = StateStore.Load();
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
    private bool _checkingForUpdate;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        InitializeFeatureState();
        Loaded += (_, _) =>
        {
            RefreshOrganizerStatus();
            AddTimerBackgroundControls();
            InitializeTrayIcon();
#if DEBUG
            var capturePath = Environment.GetEnvironmentVariable("TOOLBOX_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                if (Environment.GetEnvironmentVariable("TOOLBOX_CAPTURE_SIZE")?.Equals("compact", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Width = MinWidth;
                    Height = MinHeight;
                }
                switch (Environment.GetEnvironmentVariable("TOOLBOX_CAPTURE_PAGE")?.ToLowerInvariant())
                {
                    case "timer": ShowPage(TimerPage, TimerNav); break;
                    case "settings": ShowPage(SettingsPage, SettingsNav); break;
                    case "clipboard": ShowPage(ClipboardPage, ClipboardNav); break;
                }
                _ = CaptureAndCloseDebugPreviewAsync(capturePath);
            }
#endif
        };
    }

#if DEBUG
    private async Task CaptureAndCloseDebugPreviewAsync(string path)
    {
        await Task.Delay(450);
        CaptureDebugScreenshot(path);
        Close();
    }

    private void CaptureDebugScreenshot(string path)
    {
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var pixelWidth = Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
#endif

    private void AddTimerBackgroundControls()
    {
        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(4, 14, 0, 0) };
        row.Children.Add(new TextBlock { Text = "背景", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var colorButton = new System.Windows.Controls.Button { Content = "选择颜色…" };
        colorButton.SetResourceReference(FrameworkElement.StyleProperty, "ActionButton");
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
        TimerBackgroundHost.Children.Add(row);
        TimerBackgroundHost.Children.Add(new TextBlock { Text = "默认背景完全透明。倒计时结束后继续负计时，并显示红色数字。", Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(104, 115, 134)), Margin = new Thickness(4, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WindowMessageHook);
        RegisterHotKey(handle, HotkeyHighlight, 0x4000, 0x70);
        RegisterHotKey(handle, HotkeyAnnotation, 0x4000, 0x71);
        AddClipboardFormatListener(handle);
        if (AutoUpdateCheck.IsChecked == true) _ = CheckForUpdatesAsync(false);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey)
        {
            if (wParam.ToInt32() == HotkeyHighlight) ToggleHighlight();
            else if (wParam.ToInt32() == HotkeyAnnotation) ToggleAnnotation();
            handled = true;
        }
        else if (message == WmClipboardUpdate)
        {
            CaptureClipboardText();
        }
        return IntPtr.Zero;
    }

    private void ShowPage(UIElement page, System.Windows.Controls.Button nav)
    {
        foreach (var item in new[] { MousePage, TimerPage, PomodoroPage, ClipboardPage, NotesPage, OrganizerPage, RenamePage, ImagePage, PdfPage, SettingsPage }) item.Visibility = Visibility.Collapsed;
        foreach (var item in new[] { MouseNav, TimerNav, PomodoroNav, ClipboardNav, NotesNav, OrganizerNav, RenameNav, ImageNav, PdfNav, SettingsNav })
        {
            item.Background = System.Windows.Media.Brushes.Transparent;
            item.BorderBrush = System.Windows.Media.Brushes.Transparent;
        }
        page.Visibility = Visibility.Visible;
        nav.Background = (System.Windows.Media.Brush)FindResource("ActiveNavBrush");
        nav.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 216, 255));
    }

    private void MouseNav_Click(object sender, RoutedEventArgs e) => ShowPage(MousePage, MouseNav);
    private void TimerNav_Click(object sender, RoutedEventArgs e) => ShowPage(TimerPage, TimerNav);
    private void PomodoroNav_Click(object sender, RoutedEventArgs e) => ShowPage(PomodoroPage, PomodoroNav);
    private void ClipboardNav_Click(object sender, RoutedEventArgs e) => ShowPage(ClipboardPage, ClipboardNav);
    private void NotesNav_Click(object sender, RoutedEventArgs e) => ShowPage(NotesPage, NotesNav);
    private void OrganizerNav_Click(object sender, RoutedEventArgs e) { ShowPage(OrganizerPage, OrganizerNav); RefreshOrganizerStatus(); }
    private void RenameNav_Click(object sender, RoutedEventArgs e) => ShowPage(RenamePage, RenameNav);
    private void ImageNav_Click(object sender, RoutedEventArgs e) => ShowPage(ImagePage, ImageNav);
    private void PdfNav_Click(object sender, RoutedEventArgs e) => ShowPage(PdfPage, PdfNav);
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, SettingsNav);

    private void EnableButton_Click(object sender, RoutedEventArgs e) => EnableHighlight();
    private void DisableButton_Click(object sender, RoutedEventArgs e) => DisableHighlight();
    private void AnnotationButton_Click(object sender, RoutedEventArgs e) => ToggleAnnotation();
    private void ToggleHighlight() { if (_overlay is null) EnableHighlight(); else DisableHighlight(); }

    private void EnableHighlight()
    {
        if (_overlay is null) { _overlay = new OverlayWindow(); ApplyOverlaySettings(); _overlay.Show(); }
        SetStatus(true);
        UpdateTrayMenuText();
    }

    private void ToggleAnnotation()
    {
        if (_annotation is null)
        {
            _annotation = new AnnotationWindow(_selectedColor);
            _annotation.Closed += (_, _) => { _annotation = null; AnnotationButton.Content = "屏幕标注  F2"; UpdateTrayMenuText(); };
            _annotation.Show(); AnnotationButton.Content = "关闭标注  F2";
        }
        else { _annotation.Close(); _annotation = null; AnnotationButton.Content = "屏幕标注  F2"; }
        UpdateTrayMenuText();
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
        ColorPreview.Background = new SolidColorBrush(_selectedColor);
        ColorHexText.Text = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
        ApplyOverlaySettings();
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
        _countdown ??= CreateCountdownWindow();
        ApplyTimerBackground(); _countdown.Show(); _countdown.Activate(); _countdown.Start(seconds);
    }

    private CountdownWindow CreateCountdownWindow()
    {
        var window = new CountdownWindow();
        window.Closed += (_, _) => _countdown = null;
        return window;
    }

    private void ToggleTimer_Click(object sender, RoutedEventArgs e) { _countdown ??= CreateCountdownWindow(); if (!_countdown.IsVisible) _countdown.Show(); _countdown.Toggle(); }
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
        if (_checkingForUpdate) return;
        _checkingForUpdate = true;
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = "正在检查…";
        try
        {
            var result = await UpdateService.CheckAsync();
            if (!result.UpdateAvailable) { if (showResult) System.Windows.MessageBox.Show("当前已经是最新版本。", "工具箱"); return; }
            if (System.Windows.MessageBox.Show($"发现工具箱 {result.Version}，是否下载并安装？", "发现更新", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                var progressWindow = new UpdateProgressWindow(result.Version) { Owner = this };
                progressWindow.Show();
                IsEnabled = false;
                try
                {
                    if (await progressWindow.DownloadAndStartInstallerAsync(result))
                    {
                        System.Windows.Application.Current.Shutdown();
                        return;
                    }
                }
                finally
                {
                    if (IsVisible) IsEnabled = true;
                    if (progressWindow.IsVisible) progressWindow.CloseSafely();
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (showResult) System.Windows.MessageBox.Show("检查更新已取消。", "工具箱", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            if (showResult) System.Windows.MessageBox.Show($"更新失败：{ex.Message}\n\n请稍后重试；下载程序会自动选择可用线路。", "工具箱", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _checkingForUpdate = false;
            if (CheckUpdateButton is not null) { CheckUpdateButton.IsEnabled = true; CheckUpdateButton.Content = "立即检查更新"; }
        }
    }

    private void SetStatus(bool enabled)
    {
        StatusText.Text = enabled ? "当前状态：已启用" : "当前状态：未启用";
        StatusDot.Fill = new SolidColorBrush(enabled ? System.Windows.Media.Color.FromRgb(52, 168, 83) : System.Windows.Media.Color.FromRgb(154, 160, 166));
        EnableButton.IsEnabled = !enabled; DisableButton.IsEnabled = enabled;
    }

    private void DisableHighlight()
    {
        _overlay?.Close(); _overlay = null; SetStatus(false); UpdateTrayMenuText();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowBorder is not null)
        {
            WindowBorder.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(16);
        }
        if (SidebarBorder is not null)
        {
            SidebarBorder.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(15, 0, 0, 15);
        }
        if (MaximizeButton is not null)
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }
        if (WindowState == WindowState.Minimized && MinimizeToTrayCheck.IsChecked == true)
        {
            Hide();
            ShowTrayHintOnce();
        }
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;
        DisableHighlight(); _annotation?.Close(); _keyboardDisplay?.Dispose(); _countdown?.Close();
        ShutdownFeatures();
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            UnregisterHotKey(handle, HotkeyHighlight);
            UnregisterHotKey(handle, HotkeyAnnotation);
            RemoveClipboardFormatListener(handle);
        }
        _source?.RemoveHook(WindowMessageHook);
    }

    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
