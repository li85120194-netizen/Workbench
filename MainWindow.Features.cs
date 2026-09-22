using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Workbench;

public partial class MainWindow
{
    private readonly ObservableCollection<ClipboardEntry> _clipboardEntries = new();
    private readonly ObservableCollection<NoteItem> _notes = new();
    private readonly Dictionary<Guid, StickyNoteWindow> _stickyWindows = new();
    private readonly List<string> _renamePaths = new();
    private readonly List<string> _imagePaths = new();
    private IReadOnlyList<RenamePreviewItem> _renamePreview = Array.Empty<RenamePreviewItem>();
    private string? _imageOutputDirectory;
    private DispatcherTimer? _stateSaveTimer;
    private DispatcherTimer? _pomodoroTimer;
    private DateTime _pomodoroEndUtc;
    private int _pomodoroRemainingSeconds = 25 * 60;
    private int _pomodoroTotalSeconds = 25 * 60;
    private int _completedPomodoros;
    private bool _pomodoroRunning;
    private bool _loadingFeatureState = true;
    private bool _trayHintShown;
    private PomodoroStage _pomodoroStage = PomodoroStage.Focus;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Forms.ToolStripMenuItem? _trayHighlightItem;
    private System.Windows.Forms.ToolStripMenuItem? _trayAnnotationItem;

    private enum PomodoroStage { Focus, ShortBreak, LongBreak }

    private void InitializeFeatureState()
    {
        _loadingFeatureState = true;
        ClipboardMonitorCheck.IsChecked = _state.ClipboardMonitoringEnabled;
        AutoUpdateCheck.IsChecked = _state.AutoCheckUpdates;
        MinimizeToTrayCheck.IsChecked = _state.MinimizeToTray;

        foreach (var entry in _state.ClipboardHistory.OrderByDescending(item => item.CreatedAt).Take(100)) _clipboardEntries.Add(entry);
        ClipboardList.ItemsSource = _clipboardEntries;
        foreach (var note in _state.Notes.OrderByDescending(item => item.CreatedAt))
        {
            SubscribeToNote(note);
            _notes.Add(note);
        }
        NotesList.ItemsSource = _notes;

        _stateSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _stateSaveTimer.Tick += (_, _) => { _stateSaveTimer.Stop(); SaveStateNow(); };
        _pomodoroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _pomodoroTimer.Tick += PomodoroTimer_Tick;
        UpdateClipboardCount();
        UpdatePomodoroDisplay();
        _loadingFeatureState = false;
    }

    private void InitializeTrayIcon()
    {
        if (_trayIcon is not null) return;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开工具箱", null, (_, _) => Dispatcher.Invoke(RestoreMainWindow));
        _trayHighlightItem = new System.Windows.Forms.ToolStripMenuItem("启用鼠标高亮", null, (_, _) => Dispatcher.Invoke(ToggleHighlight));
        _trayAnnotationItem = new System.Windows.Forms.ToolStripMenuItem("开启屏幕标注", null, (_, _) => Dispatcher.Invoke(ToggleAnnotation));
        menu.Items.Add(_trayHighlightItem);
        menu.Items.Add(_trayAnnotationItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出工具箱", null, (_, _) => Dispatcher.Invoke(Close));

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "工具箱",
            Visible = true,
            ContextMenuStrip = menu
        };
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
            if (icon is not null) _trayIcon.Icon = (System.Drawing.Icon)icon.Clone();
        }
        catch { }
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreMainWindow);
        UpdateTrayMenuText();
    }

    private void RestoreMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ShowTrayHintOnce()
    {
        if (_trayHintShown || _trayIcon is null) return;
        _trayHintShown = true;
        _trayIcon.ShowBalloonTip(2200, "工具箱仍在运行", "双击托盘中的“∞”图标可重新打开。", System.Windows.Forms.ToolTipIcon.Info);
    }

    private void UpdateTrayMenuText()
    {
        if (_trayHighlightItem is not null) _trayHighlightItem.Text = _overlay is null ? "启用鼠标高亮" : "关闭鼠标高亮";
        if (_trayAnnotationItem is not null) _trayAnnotationItem.Text = _annotation is null ? "开启屏幕标注" : "关闭屏幕标注";
    }

    private void GeneralSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingFeatureState) return;
        ScheduleStateSave();
    }

    private void ScheduleStateSave()
    {
        if (_stateSaveTimer is null || _loadingFeatureState) return;
        _stateSaveTimer.Stop();
        _stateSaveTimer.Start();
    }

    private void SaveStateNow()
    {
        try
        {
            _state.ClipboardMonitoringEnabled = ClipboardMonitorCheck.IsChecked == true;
            _state.AutoCheckUpdates = AutoUpdateCheck.IsChecked == true;
            _state.MinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
            _state.ClipboardHistory = _clipboardEntries.Take(100).ToList();
            _state.Notes = _notes.ToList();
            StateStore.Save(_state);
        }
        catch { }
    }

    private void CaptureClipboardText()
    {
        if (ClipboardMonitorCheck?.IsChecked != true) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            try
            {
                if (ClipboardMonitorCheck.IsChecked != true) return;
                if (!System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText)) return;
                var text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText);
                if (string.IsNullOrWhiteSpace(text) || text.Length > 200_000) return;
                var existing = _clipboardEntries.FirstOrDefault(item => item.Text == text);
                if (existing is not null) _clipboardEntries.Remove(existing);
                _clipboardEntries.Insert(0, new ClipboardEntry { Text = text, CreatedAt = DateTime.Now });
                while (_clipboardEntries.Count > 100) _clipboardEntries.RemoveAt(_clipboardEntries.Count - 1);
                UpdateClipboardCount();
                ScheduleStateSave();
            }
            catch { }
        });
    }

    private void ClipboardMonitorCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingFeatureState) return;
        ScheduleStateSave();
        if (ClipboardMonitorCheck.IsChecked == true) CaptureClipboardText();
    }

    private void ClipboardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClipboardPreview.Text = (ClipboardList.SelectedItem as ClipboardEntry)?.Text ?? string.Empty;
    }

    private void ClipboardList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => CopySelectedClipboardEntry();
    private void CopyClipboardEntry_Click(object sender, RoutedEventArgs e) => CopySelectedClipboardEntry();

    private void CopySelectedClipboardEntry()
    {
        if (ClipboardList.SelectedItem is not ClipboardEntry entry) return;
        try { System.Windows.Clipboard.SetText(entry.Text); }
        catch (Exception ex) { System.Windows.MessageBox.Show("复制失败：" + ex.Message, "工具箱", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void DeleteClipboardEntry_Click(object sender, RoutedEventArgs e)
    {
        if (ClipboardList.SelectedItem is not ClipboardEntry entry) return;
        _clipboardEntries.Remove(entry);
        ClipboardPreview.Clear();
        UpdateClipboardCount();
        ScheduleStateSave();
    }

    private void ClearClipboardHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_clipboardEntries.Count == 0) return;
        if (System.Windows.MessageBox.Show("确定清空全部剪贴板历史吗？", "工具箱", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _clipboardEntries.Clear();
        ClipboardPreview.Clear();
        UpdateClipboardCount();
        ScheduleStateSave();
    }

    private void UpdateClipboardCount() => ClipboardCountText.Text = $"{_clipboardEntries.Count} 条";

    private void PomodoroStart_Click(object sender, RoutedEventArgs e)
    {
        if (_pomodoroRunning)
        {
            _pomodoroRemainingSeconds = Math.Max(0, (int)Math.Ceiling((_pomodoroEndUtc - DateTime.UtcNow).TotalSeconds));
            _pomodoroRunning = false;
            _pomodoroTimer?.Stop();
        }
        else
        {
            if (_pomodoroRemainingSeconds <= 0) ResetPomodoroStage(_pomodoroStage);
            _pomodoroEndUtc = DateTime.UtcNow.AddSeconds(_pomodoroRemainingSeconds);
            _pomodoroRunning = true;
            _pomodoroTimer?.Start();
        }
        UpdatePomodoroDisplay();
    }

    private void PomodoroReset_Click(object sender, RoutedEventArgs e)
    {
        _pomodoroRunning = false;
        _pomodoroTimer?.Stop();
        _pomodoroStage = PomodoroStage.Focus;
        ResetPomodoroStage(_pomodoroStage);
    }

    private void PomodoroSkip_Click(object sender, RoutedEventArgs e) => AdvancePomodoro(false);

    private void PomodoroTimer_Tick(object? sender, EventArgs e)
    {
        if (!_pomodoroRunning) return;
        _pomodoroRemainingSeconds = Math.Max(0, (int)Math.Ceiling((_pomodoroEndUtc - DateTime.UtcNow).TotalSeconds));
        if (_pomodoroRemainingSeconds == 0) AdvancePomodoro(true);
        else UpdatePomodoroDisplay();
    }

    private void AdvancePomodoro(bool notify)
    {
        var wasRunning = _pomodoroRunning;
        if (_pomodoroStage == PomodoroStage.Focus)
        {
            _completedPomodoros++;
            var rounds = ReadPositive(PomodoroRoundCount.Text, 4, 1, 12);
            _pomodoroStage = _completedPomodoros % rounds == 0 ? PomodoroStage.LongBreak : PomodoroStage.ShortBreak;
        }
        else _pomodoroStage = PomodoroStage.Focus;
        ResetPomodoroStage(_pomodoroStage);
        _pomodoroRunning = wasRunning;
        if (wasRunning)
        {
            _pomodoroEndUtc = DateTime.UtcNow.AddSeconds(_pomodoroRemainingSeconds);
            _pomodoroTimer?.Start();
        }
        if (notify)
        {
            SystemSounds.Asterisk.Play();
            _trayIcon?.ShowBalloonTip(5000, "番茄钟", _pomodoroStage == PomodoroStage.Focus ? "休息结束，开始下一轮专注。" : "专注完成，休息一下吧。", System.Windows.Forms.ToolTipIcon.Info);
        }
        UpdatePomodoroDisplay();
    }

    private void ResetPomodoroStage(PomodoroStage stage)
    {
        var minutes = stage switch
        {
            PomodoroStage.Focus => ReadPositive(PomodoroWorkMinutes.Text, 25, 1, 240),
            PomodoroStage.ShortBreak => ReadPositive(PomodoroShortMinutes.Text, 5, 1, 120),
            _ => ReadPositive(PomodoroLongMinutes.Text, 15, 1, 180)
        };
        _pomodoroRemainingSeconds = minutes * 60;
        _pomodoroTotalSeconds = _pomodoroRemainingSeconds;
        UpdatePomodoroDisplay();
    }

    private void UpdatePomodoroDisplay()
    {
        if (PomodoroTimeText is null) return;
        var span = TimeSpan.FromSeconds(Math.Max(0, _pomodoroRemainingSeconds));
        PomodoroTimeText.Text = $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
        PomodoroStageText.Text = _pomodoroStage switch { PomodoroStage.Focus => "专注时间", PomodoroStage.ShortBreak => "短休息", _ => "长休息" };
        PomodoroStartButton.Content = _pomodoroRunning ? "暂停" : "开始";
        PomodoroProgress.Value = _pomodoroTotalSeconds <= 0 ? 0 : 100.0 * (_pomodoroTotalSeconds - _pomodoroRemainingSeconds) / _pomodoroTotalSeconds;
        PomodoroCountText.Text = $"今日已完成 {_completedPomodoros} 个番茄";
    }

    private static int ReadPositive(string text, int fallback, int minimum, int maximum) => int.TryParse(text, out var value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private void SubscribeToNote(NoteItem note) => note.PropertyChanged += Note_PropertyChanged;
    private void Note_PropertyChanged(object? sender, PropertyChangedEventArgs e) => ScheduleStateSave();

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        var note = new NoteItem();
        SubscribeToNote(note);
        _notes.Insert(0, note);
        NotesList.SelectedItem = note;
        ScheduleStateSave();
    }

    private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NotesList.SelectedItem is not NoteItem note)
        {
            NoteTitleBox.Clear(); NoteContentBox.Clear(); NoteDoneCheck.IsChecked = false; return;
        }
        NoteTitleBox.Text = note.Title;
        NoteContentBox.Text = note.Content;
        NoteDoneCheck.IsChecked = note.IsDone;
    }

    private void SaveNote_Click(object sender, RoutedEventArgs e)
    {
        if (NotesList.SelectedItem is not NoteItem note)
        {
            NewNote_Click(sender, e);
            note = (NoteItem)NotesList.SelectedItem;
        }
        note.Title = string.IsNullOrWhiteSpace(NoteTitleBox.Text) ? "未命名便签" : NoteTitleBox.Text.Trim();
        note.Content = NoteContentBox.Text;
        note.IsDone = NoteDoneCheck.IsChecked == true;
        NotesList.Items.Refresh();
        ScheduleStateSave();
    }

    private void ShowStickyNote_Click(object sender, RoutedEventArgs e)
    {
        if (NotesList.SelectedItem is not NoteItem note) { System.Windows.MessageBox.Show("请先选择或新建一条便签。", "工具箱"); return; }
        SaveNote_Click(sender, e);
        if (_stickyWindows.TryGetValue(note.Id, out var existing)) { existing.Show(); existing.Activate(); return; }
        var window = new StickyNoteWindow(note, () => { NotesList.Items.Refresh(); ScheduleStateSave(); });
        _stickyWindows[note.Id] = window;
        window.Closed += (_, _) => _stickyWindows.Remove(note.Id);
        window.Show();
    }

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (NotesList.SelectedItem is not NoteItem note) return;
        if (System.Windows.MessageBox.Show($"确定删除“{note.Title}”吗？", "工具箱", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (_stickyWindows.Remove(note.Id, out var window)) window.Close();
        note.PropertyChanged -= Note_PropertyChanged;
        _notes.Remove(note);
        ScheduleStateSave();
    }

    private void SelectRenameFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "选择要重命名的文件", Filter = "所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        _renamePaths.Clear(); _renamePaths.AddRange(dialog.FileNames); UpdateRenamePreview();
    }

    private void SelectRenameFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "选择包含待重命名文件的文件夹", UseDescriptionForTitle = true };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        _renamePaths.Clear(); _renamePaths.AddRange(Directory.EnumerateFiles(dialog.SelectedPath, "*", SearchOption.TopDirectoryOnly)); UpdateRenamePreview();
    }

    private void RenameRule_Changed(object sender, RoutedEventArgs e) => UpdateRenamePreview();

    private void UpdateRenamePreview()
    {
        if (RenameFindBox is null || RenameReplaceBox is null || RenamePrefixBox is null || RenameSuffixBox is null ||
            RenameRegexCheck is null || RenameNumberCheck is null || RenameNumberStart is null ||
            RenamePreviewGrid is null || RenameSelectionText is null) return;
        try
        {
            var start = int.TryParse(RenameNumberStart.Text, out var number) ? number : 1;
            var options = new RenameOptions(RenameFindBox.Text, RenameReplaceBox.Text, RenamePrefixBox.Text, RenameSuffixBox.Text, RenameRegexCheck.IsChecked == true, RenameNumberCheck.IsChecked == true, start);
            _renamePreview = BatchRenameService.Preview(_renamePaths, options);
            RenamePreviewGrid.ItemsSource = _renamePreview;
            RenameSelectionText.Text = _renamePaths.Count == 0 ? "尚未选择文件" : $"已选择 {_renamePaths.Count} 个文件";
        }
        catch (Exception ex)
        {
            _renamePreview = Array.Empty<RenamePreviewItem>();
            RenamePreviewGrid.ItemsSource = null;
            RenameSelectionText.Text = "规则有误：" + ex.Message;
        }
    }

    private void ApplyRename_Click(object sender, RoutedEventArgs e)
    {
        if (_renamePreview.Count == 0) { System.Windows.MessageBox.Show("请先选择文件并确认预览。", "工具箱"); return; }
        if (System.Windows.MessageBox.Show("确认按当前预览重命名这些文件吗？", "批量重命名", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var result = BatchRenameService.Apply(_renamePreview);
        System.Windows.MessageBox.Show(result.Message, "工具箱", MessageBoxButton.OK, result.Renamed > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        _renamePaths.Clear(); _renamePreview = Array.Empty<RenamePreviewItem>(); RenamePreviewGrid.ItemsSource = null; RenameSelectionText.Text = "尚未选择文件";
    }

    private void SelectImages_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "选择图片", Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.gif|所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        _imagePaths.Clear(); _imagePaths.AddRange(dialog.FileNames);
        _imageOutputDirectory ??= Path.Combine(Path.GetDirectoryName(_imagePaths[0])!, "工具箱-图片输出");
        ImageSelectionText.Text = $"已选择 {_imagePaths.Count} 张图片";
        ImageOutputText.Text = "输出目录：" + _imageOutputDirectory;
    }

    private void SelectImageOutput_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "选择图片输出目录", UseDescriptionForTitle = true };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        _imageOutputDirectory = dialog.SelectedPath;
        ImageOutputText.Text = "输出目录：" + _imageOutputDirectory;
    }

    private void ImageQualitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ImageQualityText is not null) ImageQualityText.Text = ((int)e.NewValue).ToString();
    }

    private async void ConvertImages_Click(object sender, RoutedEventArgs e)
    {
        if (_imagePaths.Count == 0) { System.Windows.MessageBox.Show("请先选择图片。", "工具箱"); return; }
        _imageOutputDirectory ??= Path.Combine(Path.GetDirectoryName(_imagePaths[0])!, "工具箱-图片输出");
        var format = ((ComboBoxItem)ImageFormatBox.SelectedItem).Content?.ToString() ?? "JPEG";
        var maxEdge = int.TryParse(ImageMaxEdgeBox.Text, out var edge) ? Math.Max(0, edge) : 0;
        var options = new ImageConversionOptions(format, (int)ImageQualitySlider.Value, maxEdge, _imageOutputDirectory);
        ImageConvertButton.IsEnabled = false; ImageProgress.Value = 0; ImageStatusText.Text = "正在处理…";
        try
        {
            var progress = new Progress<int>(value => ImageProgress.Value = value);
            var result = await ImageProcessingService.ProcessAsync(_imagePaths, options, progress);
            ImageStatusText.Text = $"完成：成功 {result.Success}，失败 {result.Failed}";
            if (result.Errors.Count > 0) System.Windows.MessageBox.Show(string.Join("\n", result.Errors.Take(8)), "部分图片处理失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) { ImageStatusText.Text = "处理失败"; System.Windows.MessageBox.Show(ex.Message, "工具箱", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { ImageConvertButton.IsEnabled = true; }
    }

    private async void MergePdf_Click(object sender, RoutedEventArgs e)
    {
        var open = new OpenFileDialog { Multiselect = true, Filter = "PDF 文件|*.pdf", Title = "按合并顺序选择 PDF" };
        if (open.ShowDialog(this) != true || open.FileNames.Length < 2) { PdfStatusText.Text = "合并至少需要选择两个 PDF。"; return; }
        var save = new SaveFileDialog { Filter = "PDF 文件|*.pdf", FileName = "合并.pdf", InitialDirectory = Path.GetDirectoryName(open.FileNames[0]) };
        if (save.ShowDialog(this) != true) return;
        if (open.FileNames.Any(path => string.Equals(path, save.FileName, StringComparison.OrdinalIgnoreCase))) { PdfStatusText.Text = "输出文件不能覆盖输入文件。"; return; }
        await RunPdfActionAsync("正在合并…", () => { PdfToolsService.Merge(open.FileNames, save.FileName); return $"已合并 {open.FileNames.Length} 个文件：{save.FileName}"; });
    }

    private async void SplitPdf_Click(object sender, RoutedEventArgs e)
    {
        var input = SelectSinglePdf("选择要拆分的 PDF"); if (input is null) return;
        using var folder = new System.Windows.Forms.FolderBrowserDialog { Description = "选择拆分后的输出目录", SelectedPath = Path.GetDirectoryName(input), UseDescriptionForTitle = true };
        if (folder.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var outputDirectory = folder.SelectedPath!;
        await RunPdfActionAsync("正在拆分…", () => { var count = PdfToolsService.Split(input, outputDirectory); return $"已拆分 {count} 页到：{outputDirectory}"; });
    }

    private async void ExtractPdf_Click(object sender, RoutedEventArgs e)
    {
        var input = SelectSinglePdf("选择要提取页面的 PDF"); if (input is null) return;
        var save = new SaveFileDialog { Filter = "PDF 文件|*.pdf", FileName = Path.GetFileNameWithoutExtension(input) + "_提取.pdf", InitialDirectory = Path.GetDirectoryName(input) };
        if (save.ShowDialog(this) != true) return;
        if (string.Equals(input, save.FileName, StringComparison.OrdinalIgnoreCase)) { PdfStatusText.Text = "输出文件不能覆盖输入文件。"; return; }
        var range = PdfRangeBox.Text;
        await RunPdfActionAsync("正在提取页面…", () => { var count = PdfToolsService.Extract(input, save.FileName, range); return $"已提取 {count} 页：{save.FileName}"; });
    }

    private async void RotatePdf_Click(object sender, RoutedEventArgs e)
    {
        var input = SelectSinglePdf("选择要旋转的 PDF"); if (input is null) return;
        var save = new SaveFileDialog { Filter = "PDF 文件|*.pdf", FileName = Path.GetFileNameWithoutExtension(input) + "_旋转.pdf", InitialDirectory = Path.GetDirectoryName(input) };
        if (save.ShowDialog(this) != true) return;
        if (string.Equals(input, save.FileName, StringComparison.OrdinalIgnoreCase)) { PdfStatusText.Text = "输出文件不能覆盖输入文件。"; return; }
        var degrees = int.TryParse(((ComboBoxItem)PdfRotateBox.SelectedItem).Tag?.ToString(), out var value) ? value : 90;
        await RunPdfActionAsync("正在旋转页面…", () => { var count = PdfToolsService.Rotate(input, save.FileName, degrees); return $"已旋转 {count} 页：{save.FileName}"; });
    }

    private string? SelectSinglePdf(string title)
    {
        var dialog = new OpenFileDialog { Filter = "PDF 文件|*.pdf", Title = title };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private async Task RunPdfActionAsync(string runningText, Func<string> action)
    {
        PdfStatusText.Text = runningText;
        try { PdfStatusText.Text = await Task.Run(action); }
        catch (Exception ex) { PdfStatusText.Text = "处理失败：" + ex.Message; System.Windows.MessageBox.Show(PdfStatusText.Text, "PDF 工具", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ShutdownFeatures()
    {
        _pomodoroTimer?.Stop();
        _stateSaveTimer?.Stop();
        foreach (var window in _stickyWindows.Values.ToList()) window.Close();
        _stickyWindows.Clear();
        SaveStateNow();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.ContextMenuStrip?.Dispose();
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
