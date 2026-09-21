using System.ComponentModel;
using System.Windows;

namespace Workbench;

public partial class UpdateProgressWindow : Window
{
    private readonly CancellationTokenSource _cancellation = new();
    private bool _allowClose;

    public UpdateProgressWindow(string version)
    {
        InitializeComponent();
        StatusText.Text = $"正在下载工具箱 v{version} 更新引导程序…";
    }

    public async Task<bool> DownloadAndStartInstallerAsync(UpdateResult update)
    {
        try
        {
            var progress = new Progress<DownloadProgressInfo>(UpdateProgress);
            string installerPath = await UpdateService.DownloadAsync(update, progress, _cancellation.Token);

            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = 100;
            PercentText.Text = "100%";
            StatusText.Text = "下载完成，正在启动安装程序…";
            DownloadedText.Text = "更新引导程序已就绪，即将继续下载完整安装包。";
            RemainingText.Text = "00:00";
            CancelButton.IsEnabled = false;

            await Task.Delay(350);
            UpdateService.StartInstaller(installerPath);
            _allowClose = true;
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "更新已取消。";
            return false;
        }
    }

    public void CloseSafely()
    {
        _allowClose = true;
        Close();
    }

    private void UpdateProgress(DownloadProgressInfo info)
    {
        DownloadProgressBar.IsIndeterminate = !info.Percentage.HasValue;
        if (info.Percentage.HasValue)
        {
            DownloadProgressBar.Value = Math.Clamp(info.Percentage.Value, 0, 100);
            PercentText.Text = $"{info.Percentage.Value:0}%";
        }
        else
        {
            PercentText.Text = "--%";
        }

        DownloadedText.Text = info.TotalBytes is > 0
            ? $"已下载 {FormatBytes(info.BytesReceived)} / {FormatBytes(info.TotalBytes.Value)}"
            : $"已下载 {FormatBytes(info.BytesReceived)}";
        SpeedText.Text = $"{FormatBytes((long)info.BytesPerSecond)}/秒";
        ElapsedText.Text = FormatTime(info.Elapsed);
        RemainingText.Text = info.Remaining.HasValue ? FormatTime(info.Remaining.Value) : "计算中…";
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cancellation.IsCancellationRequested) return;
        CancelButton.IsEnabled = false;
        StatusText.Text = "正在取消下载…";
        _cancellation.Cancel();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        CancelButton_Click(this, new RoutedEventArgs());
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
}
