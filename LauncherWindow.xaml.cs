using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Workbench;

public partial class LauncherWindow : Window
{
    private readonly Action<string> _openTool;
    private readonly List<LauncherItem> _items = new();
    private readonly string _recentPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Toolbox", "launcher-recent.json");

    public LauncherWindow(Action<string> openTool)
    {
        _openTool = openTool;
        InitializeComponent();
        BuildCatalog();
        Loaded += (_, _) => { QueryBox.Focus(); RefreshResults(); };
    }

    public void OpenLauncher()
    {
        if (!IsVisible) Show();
        WindowState = WindowState.Normal;
        Activate();
        QueryBox.Clear();
        QueryBox.Focus();
        RefreshResults();
    }

    private void BuildCatalog()
    {
        AddTool("home", "首页", "工具箱首页与概览", "⌂", "首页 主页 home");
        AddTool("mouse", "鼠标高亮", "演示定位与屏幕标注", "◎", "鼠标 高亮 标注 mouse");
        AddTool("timer", "倒计时", "桌面悬浮倒计时", "◷", "倒计时 计时器 timer");
        AddTool("pomodoro", "番茄钟", "专注与休息循环", "●", "番茄钟 专注 pomodoro");
        AddTool("clipboard", "剪贴板历史", "查找最近复制内容", "▣", "剪贴板 复制 历史 jtb clipboard");
        AddTool("notes", "便签 / 待办", "本地便签和待办事项", "▤", "便签 待办 note todo");
        AddTool("organizer", "桌面收纳", "预览后安全整理桌面", "▦", "桌面 收纳 整理 organizer");
        AddTool("rename", "批量重命名", "预览并批量修改文件名", "✎", "重命名 批量 plcmm rename");
        AddTool("image", "图片处理", "压缩与格式转换", "▧", "图片 压缩 转换 image");
        AddTool("pdf", "PDF 工具", "合并、拆分、提取与旋转", "PDF", "pdf 合并 拆分 旋转");
        AddTool("settings", "设置与更新", "应用设置和版本更新", "⚙", "设置 更新 setting update");

        foreach (var shortcut in EnumerateShortcuts())
        {
            var captured = shortcut;
            _items.Add(new LauncherItem(shortcut.Name, shortcut.Path, "应用", "▣", shortcut.Name, () => StartPath(captured.Path)));
        }
    }

    private void AddTool(string id, string title, string subtitle, string icon, string keywords) =>
        _items.Add(new LauncherItem(title, subtitle, "工具", icon, keywords, () => _openTool(id)));

    private static IEnumerable<(string Name, string Path)> EnumerateShortcuts()
    {
        var folders = new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) };
        return folders.Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .Select(group => (group.Key, group.First()))
            .OrderBy(item => item.Key)
            .Take(500);
    }

    private void RefreshResults()
    {
        var query = QueryBox.Text.Trim();
        var results = new List<LauncherItem>();
        if (TryCalculate(query, out var calculation)) results.Add(calculation);

        IEnumerable<LauncherItem> matches = string.IsNullOrWhiteSpace(query)
            ? OrderByRecent(_items).Take(10)
            : _items.Where(item => item.Matches(query)).OrderBy(item => item.Category == "工具" ? 0 : 1).ThenBy(item => item.Title).Take(20);
        results.AddRange(matches);
        ResultsList.ItemsSource = results;
        ResultsList.SelectedIndex = results.Count > 0 ? 0 : -1;
        ResultCountText.Text = results.Count == 0 ? "没有匹配结果" : $"{results.Count} 个结果";
    }

    private IEnumerable<LauncherItem> OrderByRecent(IEnumerable<LauncherItem> source)
    {
        var recent = LoadRecent();
        return source.OrderBy(item => { var index = recent.FindIndex(value => string.Equals(value, item.Title, StringComparison.OrdinalIgnoreCase)); return index < 0 ? int.MaxValue : index; }).ThenBy(item => item.Category == "工具" ? 0 : 1).ThenBy(item => item.Title);
    }

    private bool TryCalculate(string query, out LauncherItem item)
    {
        item = null!;
        var expression = query.StartsWith("calc ", StringComparison.OrdinalIgnoreCase) ? query[5..].Trim() : query;
        if (string.IsNullOrWhiteSpace(expression) || !Regex.IsMatch(expression, @"^[0-9\s+\-*/().%]+$") || !expression.Any(char.IsDigit)) return false;
        try
        {
            var result = new DataTable().Compute(expression, null)?.ToString();
            if (string.IsNullOrWhiteSpace(result)) return false;
            item = new LauncherItem(result, expression, "计算", "=", expression, () => System.Windows.Clipboard.SetText(result));
            return true;
        }
        catch { return false; }
    }

    private void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is not LauncherItem item) return;
        try
        {
            item.Execute();
            Remember(item.Title);
            Hide();
        }
        catch (Exception ex) { System.Windows.MessageBox.Show($"无法执行：{ex.Message}", "快捷启动器", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private static void StartPath(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    private void Remember(string title)
    {
        var recent = LoadRecent();
        recent.RemoveAll(value => string.Equals(value, title, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, title);
        if (recent.Count > 30) recent.RemoveRange(30, recent.Count - 30);
        Directory.CreateDirectory(Path.GetDirectoryName(_recentPath)!);
        File.WriteAllText(_recentPath, JsonSerializer.Serialize(recent));
    }
    private List<string> LoadRecent() { try { return File.Exists(_recentPath) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_recentPath)) ?? new() : new(); } catch { return new(); } }

    private void QueryBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshResults();
    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelected();
    private void QueryBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Down && ResultsList.Items.Count > 0) { ResultsList.SelectedIndex = Math.Min(ResultsList.Items.Count - 1, ResultsList.SelectedIndex + 1); ResultsList.ScrollIntoView(ResultsList.SelectedItem); e.Handled = true; }
        else if (e.Key == Key.Up && ResultsList.Items.Count > 0) { ResultsList.SelectedIndex = Math.Max(0, ResultsList.SelectedIndex - 1); ResultsList.ScrollIntoView(ResultsList.SelectedItem); e.Handled = true; }
        else if (e.Key == Key.Enter) { ExecuteSelected(); e.Handled = true; }
    }
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Hide(); }
    private void Window_Deactivated(object? sender, EventArgs e) => Hide();
}

public sealed class LauncherItem
{
    private readonly Action _action;
    public string Title { get; }
    public string Subtitle { get; }
    public string Category { get; }
    public string Icon { get; }
    public string Keywords { get; }
    public LauncherItem(string title, string subtitle, string category, string icon, string keywords, Action action) { Title = title; Subtitle = subtitle; Category = category; Icon = icon; Keywords = keywords; _action = action; }
    public bool Matches(string query) => Title.Contains(query, StringComparison.OrdinalIgnoreCase) || Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase) || Keywords.Contains(query, StringComparison.OrdinalIgnoreCase);
    public void Execute() => _action();
}