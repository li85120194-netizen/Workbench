using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
    private LocalAccount? _activeAccount;
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
    private System.Windows.Threading.DispatcherTimer? _homeClockTimer;
    private bool _applyingPreferences;
    private bool _checkingForUpdate;
    private bool _isClosing;
    private readonly Dictionary<UIElement, Border> _openTabs = new();
    private UIElement? _activePage;

    public MainWindow()
    {
        _activeAccount = AccountStore.Find(_state.CurrentAccountId);
        InitializeComponent();
        InitializeFeatureState();
        ShowPage(HomePage, HomeNav, "首页");
        Loaded += (_, _) =>
        {
            RefreshOrganizerStatus();
            AddTimerBackgroundControls();
            ApplyPreferences(CurrentPreferences);
            InitializeHome();
            UpdateProfileHeader();
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
                    case "mouse": ShowPage(MousePage, MouseNav, "鼠标高亮"); break;
                    case "timer": ShowPage(TimerPage, TimerNav, "倒计时"); break;
                    case "pomodoro": ShowPage(PomodoroPage, PomodoroNav, "番茄钟"); break;
                    case "notes": ShowPage(NotesPage, NotesNav, "便签 / 待办"); break;
                    case "rename": ShowPage(RenamePage, RenameNav, "批量重命名"); break;
                    case "settings": ShowPage(SettingsPage, SettingsNav, "设置与更新"); break;
                    case "clipboard": ShowPage(ClipboardPage, ClipboardNav, "剪贴板历史"); break;
                }
                _ = CaptureAndCloseDebugPreviewAsync(capturePath);
            }
#endif
        };
    }

    private ToolboxPreferences CurrentPreferences => _activeAccount?.Preferences ?? _state.GuestPreferences;

    private void InitializeHome()
    {
        UpdateHomeClock();
        UpdateHomeMetrics();
        _homeClockTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _homeClockTimer.Tick += (_, _) => { UpdateHomeClock(); UpdateHomeMetrics(); };
        _homeClockTimer.Start();
    }

    private void UpdateHomeClock()
    {
        if (HomeClockText is null) return;
        var now = DateTime.Now;
        HomeClockText.Text = now.ToString("HH:mm");
        var weekdays = new[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        HomeDateText.Text = $"{now:M月d日}  {weekdays[(int)now.DayOfWeek]}";
    }

    private void UpdateHomeMetrics()
    {
        if (HomeNotesCount is null) return;
        HomeNotesCount.Text = $"{_notes.Count(item => !item.IsDone)} 条";
        HomeClipboardCount.Text = $"{_clipboardEntries.Count} 条";
        HomeHighlightStatus.Text = _overlay is null ? "未启用" : "已启用";
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
        _timerBackgroundOpacity = new Slider { Width = 150, Minimum = 0, Maximum = 100, Value = 50 };
        _timerBackgroundOpacity.ValueChanged += TimerBackgroundOpacity_Changed;
        row.Children.Add(_timerBackgroundOpacity);
        _timerBackgroundOpacityText = new TextBlock { Text = "50%", Width = 42, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_timerBackgroundOpacityText);
        TimerBackgroundHost.Children.Add(row);
        TimerBackgroundHost.Children.Add(new TextBlock { Text = "默认背景为 50% 透明度。倒计时结束后继续负计时，并显示红色数字。", Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(104, 115, 134)), Margin = new Thickness(4, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
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

    private void ShowPage(UIElement page, System.Windows.Controls.Button nav, string title)
    {
        EnsureTab(page, nav, title);
        foreach (var item in new[] { HomePage, MousePage, TimerPage, PomodoroPage, ClipboardPage, NotesPage, OrganizerPage, RenamePage, ImagePage, PdfPage, SettingsPage }) item.Visibility = Visibility.Collapsed;
        foreach (var item in new[] { HomeNav, MouseNav, TimerNav, PomodoroNav, ClipboardNav, NotesNav, OrganizerNav, RenameNav, ImageNav, PdfNav, SettingsNav })
        {
            item.Background = System.Windows.Media.Brushes.Transparent;
            item.BorderBrush = System.Windows.Media.Brushes.Transparent;
        }
        page.Visibility = Visibility.Visible;
        _activePage = page;
        nav.Background = (System.Windows.Media.Brush)FindResource("ActiveNavBrush");
        nav.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 216, 255));
        CurrentPageTitle.Text = title;
        UpdateTabStyles();
        if (nav == MouseNav) PresentationExpander.IsExpanded = true;
        else if (nav == TimerNav || nav == PomodoroNav) TimeExpander.IsExpanded = true;
        else if (nav == ClipboardNav || nav == NotesNav) RecordExpander.IsExpanded = true;
        else if (nav == OrganizerNav || nav == RenameNav || nav == ImageNav || nav == PdfNav) FileExpander.IsExpanded = true;
    }

    private void EnsureTab(UIElement page, System.Windows.Controls.Button nav, string title)
    {
        if (_openTabs.ContainsKey(page)) return;
        var tab = new Border { Background = System.Windows.Media.Brushes.White, BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(207, 218, 232)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 0, 6, 0) };
        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        var selectButton = new System.Windows.Controls.Button { Content = title, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(13, 7, 8, 7), Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 66, 92)), Cursor = System.Windows.Input.Cursors.Hand };
        selectButton.Click += (_, _) => ShowPage(page, nav, title);
        var closeButton = new System.Windows.Controls.Button { Content = "×", Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(7, 5, 10, 7), Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 124, 146)), FontSize = 15, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "关闭" };
        closeButton.Click += (_, e) => { e.Handled = true; CloseTab(page); };
        row.Children.Add(selectButton);
        row.Children.Add(closeButton);
        tab.Child = row;
        _openTabs.Add(page, tab);
        OpenTabsPanel.Children.Add(tab);
    }

    private void CloseTab(UIElement page)
    {
        if (!_openTabs.TryGetValue(page, out var tab)) return;
        var closedIndex = OpenTabsPanel.Children.IndexOf(tab);
        OpenTabsPanel.Children.Remove(tab);
        _openTabs.Remove(page);
        page.Visibility = Visibility.Collapsed;
        if (_activePage != page) return;
        _activePage = null;
        if (OpenTabsPanel.Children.Count == 0)
        {
            CurrentPageTitle.Text = string.Empty;
            foreach (var nav in new[] { HomeNav, MouseNav, TimerNav, PomodoroNav, ClipboardNav, NotesNav, OrganizerNav, RenameNav, ImageNav, PdfNav, SettingsNav }) { nav.Background = System.Windows.Media.Brushes.Transparent; nav.BorderBrush = System.Windows.Media.Brushes.Transparent; }
            return;
        }
        var nextIndex = Math.Min(closedIndex, OpenTabsPanel.Children.Count - 1);
        var nextTab = (Border)OpenTabsPanel.Children[nextIndex];
        var nextPage = _openTabs.First(pair => pair.Value == nextTab).Key;
        ShowPage(nextPage, GetNavButton(nextPage), GetPageTitle(nextPage));
    }

    private System.Windows.Controls.Button GetNavButton(UIElement page) => page == HomePage ? HomeNav : page == MousePage ? MouseNav : page == TimerPage ? TimerNav : page == PomodoroPage ? PomodoroNav : page == ClipboardPage ? ClipboardNav : page == NotesPage ? NotesNav : page == OrganizerPage ? OrganizerNav : page == RenamePage ? RenameNav : page == ImagePage ? ImageNav : page == PdfPage ? PdfNav : SettingsNav;

    private string GetPageTitle(UIElement page)
    {
        var tab = _openTabs[page];
        var row = (StackPanel)tab.Child;
        return ((System.Windows.Controls.Button)row.Children[0]).Content?.ToString() ?? string.Empty;
    }

    private void UpdateTabStyles()
    {
        foreach (var pair in _openTabs)
        {
            var active = pair.Key == _activePage;
            pair.Value.Background = active ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 238, 255)) : System.Windows.Media.Brushes.White;
            pair.Value.BorderBrush = active ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 139, 239)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(207, 218, 232));
        }
    }
    private void HomeNav_Click(object sender, RoutedEventArgs e) => ShowPage(HomePage, HomeNav, "首页");
    private void MouseNav_Click(object sender, RoutedEventArgs e) => ShowPage(MousePage, MouseNav, "鼠标高亮");
    private void TimerNav_Click(object sender, RoutedEventArgs e) => ShowPage(TimerPage, TimerNav, "倒计时");
    private void PomodoroNav_Click(object sender, RoutedEventArgs e) => ShowPage(PomodoroPage, PomodoroNav, "番茄钟");
    private void ClipboardNav_Click(object sender, RoutedEventArgs e) => ShowPage(ClipboardPage, ClipboardNav, "剪贴板历史");
    private void NotesNav_Click(object sender, RoutedEventArgs e) => ShowPage(NotesPage, NotesNav, "便签 / 待办");
    private void OrganizerNav_Click(object sender, RoutedEventArgs e) { ShowPage(OrganizerPage, OrganizerNav, "桌面收纳"); RefreshOrganizerStatus(); }
    private void RenameNav_Click(object sender, RoutedEventArgs e) => ShowPage(RenamePage, RenameNav, "批量重命名");
    private void ImageNav_Click(object sender, RoutedEventArgs e) => ShowPage(ImagePage, ImageNav, "图片处理");
    private void PdfNav_Click(object sender, RoutedEventArgs e) => ShowPage(PdfPage, PdfNav, "PDF 工具");
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, SettingsNav, "设置与更新");

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
        if (!_applyingPreferences) ScheduleStateSave();
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
        ScheduleStateSave();
    }

    private void KeyboardCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (KeyboardCheck.IsChecked == true) { _keyboardDisplay ??= new KeyboardDisplayService(); _keyboardDisplay.Start(); }
        else { _keyboardDisplay?.Dispose(); _keyboardDisplay = null; }
        if (!_applyingPreferences) ScheduleStateSave();
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

    private void ToggleTimer_Click(object sender, RoutedEventArgs e)
    {
        _countdown ??= CreateCountdownWindow();
        ApplyTimerBackground();
        if (!_countdown.IsVisible) _countdown.Show();
        _countdown.Toggle();
    }
    private void ResetTimer_Click(object sender, RoutedEventArgs e) => _countdown?.Reset();

    private void TimerBackgroundButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(_timerBackgroundColor.R, _timerBackgroundColor.G, _timerBackgroundColor.B) };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        _timerBackgroundColor = System.Windows.Media.Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
        if (_timerBackgroundPreview is not null) _timerBackgroundPreview.Background = new SolidColorBrush(_timerBackgroundColor);
        ApplyTimerBackground();
        ScheduleStateSave();
    }

    private void TimerBackgroundOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_timerBackgroundOpacityText is not null) _timerBackgroundOpacityText.Text = $"{(int)e.NewValue}%";
        ApplyTimerBackground();
        if (!_applyingPreferences) ScheduleStateSave();
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
        if (HomeHighlightStatus is not null) HomeHighlightStatus.Text = enabled ? "已启用" : "未启用";
    }

    private void SidebarSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        CurrentPreferences.SidebarWidth = SidebarColumn.ActualWidth;
        ScheduleStateSave();
    }

    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        SaveStateNow();
        var dialog = new AccountWindow(_activeAccount) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (dialog.LoggedOut)
        {
            _activeAccount = null;
            _state.CurrentAccountId = null;
            ApplyPreferences(_state.GuestPreferences);
        }
        else if (dialog.SignedInAccount is not null)
        {
            _activeAccount = dialog.SignedInAccount;
            _state.CurrentAccountId = _activeAccount.Id;
            ApplyPreferences(_activeAccount.Preferences);
        }
        UpdateProfileHeader();
        SaveStateNow();
    }

    private void UpdateProfileHeader()
    {
        var name = _activeAccount?.DisplayName;
        ProfileNameText.Text = string.IsNullOrWhiteSpace(name) ? "登录 / 注册" : name;
        ProfileHintText.Text = _activeAccount is null ? "本机账户 · 可选" : "个人中心 · 本机保存";
        ProfileInitialText.Text = string.IsNullOrWhiteSpace(name) ? "登" : name.Trim()[0].ToString();
        ProfileInitialText.Visibility = Visibility.Visible;
        ProfileAvatarShape.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 234, 255));
        if (string.IsNullOrWhiteSpace(_activeAccount?.AvatarPath) || !File.Exists(_activeAccount.AvatarPath)) return;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(_activeAccount.AvatarPath, UriKind.Absolute);
            image.EndInit();
            ProfileAvatarShape.Fill = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            ProfileInitialText.Visibility = Visibility.Collapsed;
        }
        catch { }
    }

    private void ApplyPreferences(ToolboxPreferences preferences)
    {
        _applyingPreferences = true;
        try
        {
            SidebarColumn.Width = new GridLength(Math.Clamp(preferences.SidebarWidth, SidebarColumn.MinWidth, SidebarColumn.MaxWidth));
            ThemeBox.SelectedIndex = Math.Clamp(preferences.HighlightTheme, 0, ThemeBox.Items.Count - 1);
            ShapeBox.SelectedIndex = Math.Clamp(preferences.HighlightShape, 0, ShapeBox.Items.Count - 1);
            PositionBox.SelectedIndex = Math.Clamp(preferences.HighlightPosition, 0, PositionBox.Items.Count - 1);
            SizeSlider.Value = Math.Clamp(preferences.HighlightSize, (int)SizeSlider.Minimum, (int)SizeSlider.Maximum);
            try { _selectedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(preferences.HighlightColor); }
            catch { _selectedColor = Colors.Red; }
            ColorPreview.Background = new SolidColorBrush(_selectedColor);
            ColorHexText.Text = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
            DynamicCheck.IsChecked = preferences.DynamicHighlight;
            KeyboardCheck.IsChecked = preferences.KeyboardDisplay;
            try { _timerBackgroundColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(preferences.TimerBackgroundColor); }
            catch { _timerBackgroundColor = Colors.White; }
            if (_timerBackgroundPreview is not null) _timerBackgroundPreview.Background = new SolidColorBrush(_timerBackgroundColor);
            if (_timerBackgroundOpacity is not null) _timerBackgroundOpacity.Value = Math.Clamp(preferences.TimerBackgroundOpacity, 0, 100);
            PomodoroWorkMinutes.Text = preferences.PomodoroWorkMinutes.ToString();
            PomodoroShortMinutes.Text = preferences.PomodoroShortMinutes.ToString();
            PomodoroLongMinutes.Text = preferences.PomodoroLongMinutes.ToString();
            PomodoroRoundCount.Text = preferences.PomodoroRounds.ToString();
            ApplyOverlaySettings();
            ApplyTimerBackground();
        }
        finally { _applyingPreferences = false; }
    }

    private void CapturePreferences()
    {
        var preferences = CurrentPreferences;
        preferences.SidebarWidth = Math.Clamp(SidebarColumn.ActualWidth, SidebarColumn.MinWidth, SidebarColumn.MaxWidth);
        preferences.HighlightTheme = ThemeBox.SelectedIndex;
        preferences.HighlightShape = ShapeBox.SelectedIndex;
        preferences.HighlightPosition = PositionBox.SelectedIndex;
        preferences.HighlightSize = (int)SizeSlider.Value;
        preferences.HighlightColor = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
        preferences.DynamicHighlight = DynamicCheck.IsChecked == true;
        preferences.KeyboardDisplay = KeyboardCheck.IsChecked == true;
        preferences.TimerBackgroundOpacity = (int)(_timerBackgroundOpacity?.Value ?? 50);
        preferences.TimerBackgroundColor = $"#{_timerBackgroundColor.R:X2}{_timerBackgroundColor.G:X2}{_timerBackgroundColor.B:X2}";
        preferences.PomodoroWorkMinutes = ReadPositive(PomodoroWorkMinutes.Text, 25, 1, 240);
        preferences.PomodoroShortMinutes = ReadPositive(PomodoroShortMinutes.Text, 5, 1, 120);
        preferences.PomodoroLongMinutes = ReadPositive(PomodoroLongMinutes.Text, 15, 1, 180);
        preferences.PomodoroRounds = ReadPositive(PomodoroRoundCount.Text, 4, 1, 12);
        if (_activeAccount is not null) AccountStore.Save(_activeAccount);
    }

    private void HomeSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) { OpenHomeSearch(); e.Handled = true; }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => OpenHomeSearch();

    private void HomeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (HomeSearchPlaceholder is not null) HomeSearchPlaceholder.Visibility = string.IsNullOrEmpty(HomeSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HomeStartPomodoro_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(PomodoroPage, PomodoroNav, "番茄钟");
        if (!_pomodoroRunning) PomodoroStart_Click(sender, e);
        else ShowPomodoroWindow();
    }

    private void OpenHomeSearch()
    {
        var text = HomeSearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        string url;
        if (Uri.TryCreate(text, UriKind.Absolute, out var direct) && direct.Scheme is "http" or "https") url = direct.AbsoluteUri;
        else if (!text.Contains(' ') && text.Contains('.')) url = "https://" + text;
        else url = "https://www.baidu.com/s?wd=" + Uri.EscapeDataString(text);
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { System.Windows.MessageBox.Show("无法打开浏览器：" + ex.Message, "工具箱", MessageBoxButton.OK, MessageBoxImage.Warning); }
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
