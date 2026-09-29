using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;

namespace Workbench;

public partial class MainWindow
{
    private const string GeekDownloadUrl = "https://geekuninstaller.com/geek.zip";
    private static readonly string GeekToolDirectory = Path.Combine(StateStore.DirectoryPath, "tools", "GeekUninstaller");
    private bool _geekDownloadRunning;

    private static string? FindGeekExecutable()
    {
        if (!Directory.Exists(GeekToolDirectory)) return null;
        return Directory.EnumerateFiles(GeekToolDirectory, "geek.exe", SearchOption.AllDirectories).FirstOrDefault()
            ?? Directory.EnumerateFiles(GeekToolDirectory, "geek64.exe", SearchOption.AllDirectories).FirstOrDefault();
    }

    private void UpdateGeekStatus()
    {
        var executable = FindGeekExecutable();
        GeekLaunchButton.Content = executable is null ? "下载并启动" : "启动 Geek Uninstaller";
        GeekDownloadStatus.Text = executable is null
            ? "尚未下载。点击后将从官网获取约 3 MB 的便携版。"
            : $"已准备就绪：{executable}";
    }

    private async void DownloadAndLaunchGeek_Click(object sender, RoutedEventArgs e)
    {
        if (_geekDownloadRunning) return;
        var executable = FindGeekExecutable();
        if (executable is null) executable = await DownloadGeekAsync(false);
        if (executable is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            GeekDownloadStatus.Text = "Geek Uninstaller 已启动。";
        }
        catch (Exception ex) { GeekDownloadStatus.Text = "启动失败：" + ex.Message; }
    }

    private async void RefreshGeek_Click(object sender, RoutedEventArgs e)
    {
        if (_geekDownloadRunning) return;
        await DownloadGeekAsync(true);
    }

    private async Task<string?> DownloadGeekAsync(bool replace)
    {
        _geekDownloadRunning = true;
        GeekLaunchButton.IsEnabled = false;
        GeekDownloadProgress.Visibility = Visibility.Visible;
        GeekDownloadProgress.IsIndeterminate = true;
        GeekDownloadStatus.Text = "正在从 Geek Uninstaller 官网下载…";
        try
        {
            Directory.CreateDirectory(GeekToolDirectory);
            var zipPath = Path.Combine(GeekToolDirectory, "geek.zip");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XiaoEr-Toolbox/1.3.11");
            var bytes = await client.GetByteArrayAsync(GeekDownloadUrl);
            await File.WriteAllBytesAsync(zipPath, bytes);
            GeekDownloadStatus.Text = "下载完成，正在解压…";
            if (replace)
            {
                foreach (var file in Directory.EnumerateFiles(GeekToolDirectory, "geek*.exe", SearchOption.AllDirectories))
                    File.Delete(file);
            }
            ZipFile.ExtractToDirectory(zipPath, GeekToolDirectory, true);
            File.Delete(zipPath);
            var executable = FindGeekExecutable();
            if (executable is null) throw new InvalidDataException("压缩包中未找到 geek.exe。");
            GeekDownloadStatus.Text = $"已准备就绪：{executable}";
            GeekLaunchButton.Content = "启动 Geek Uninstaller";
            return executable;
        }
        catch (Exception ex)
        {
            GeekDownloadStatus.Text = "下载失败：" + ex.Message;
            return null;
        }
        finally
        {
            _geekDownloadRunning = false;
            GeekLaunchButton.IsEnabled = true;
            GeekDownloadProgress.IsIndeterminate = false;
            GeekDownloadProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void OpenGeekFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(GeekToolDirectory);
        Process.Start(new ProcessStartInfo(GeekToolDirectory) { UseShellExecute = true });
    }

    private void OpenGeekWebsite_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://geekuninstaller.com/download") { UseShellExecute = true });
}