using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Workbench;

public sealed record UpdateResult(bool UpdateAvailable, string Version, string DownloadUrl);

public static class UpdateService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/li85120194-netizen/Workbench/releases/latest";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    static UpdateService() => Client.DefaultRequestHeaders.UserAgent.ParseAdd("Workbench-Updater/1.0");

    public static async Task<UpdateResult> CheckAsync()
    {
        string json = await Client.GetStringAsync(LatestReleaseApi);
        using var document = JsonDocument.Parse(json);
        string tag = document.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? "0.0.0";
        string url = document.RootElement.GetProperty("assets").EnumerateArray()
            .FirstOrDefault(a => string.Equals(a.GetProperty("name").GetString(), "WorkbenchSetup.exe", StringComparison.OrdinalIgnoreCase))
            .GetProperty("browser_download_url").GetString() ?? throw new InvalidDataException("发布中没有 WorkbenchSetup.exe。");
        var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        return new(new Version(tag) > current, tag, url);
    }

    public static async Task DownloadAndInstallAsync(UpdateResult update)
    {
        string target = Path.Combine(Path.GetTempPath(), $"WorkbenchSetup-{update.Version}.exe");
        await File.WriteAllBytesAsync(target, await Client.GetByteArrayAsync(update.DownloadUrl));
        Process.Start(new ProcessStartInfo(target, "/VERYSILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS") { UseShellExecute = true });
        System.Windows.Application.Current.Shutdown();
    }
}
