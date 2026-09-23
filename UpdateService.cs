using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Workbench;

public sealed record UpdateResult(bool UpdateAvailable, string Version, string DownloadUrl, long Size);

public sealed record DownloadProgressInfo(
    long BytesReceived,
    long? TotalBytes,
    double? Percentage,
    double BytesPerSecond,
    TimeSpan Elapsed,
    TimeSpan? Remaining);

public static class UpdateService
{
    private const string RepositoryUrl = "https://github.com/li85120194-netizen/Workbench";
    private const string LatestReleaseApi = "https://api.github.com/repos/li85120194-netizen/Workbench/releases/latest";
    private const string LatestReleaseUrl = RepositoryUrl + "/releases/latest";
    private const string LatestInstallerUrl = RepositoryUrl + "/releases/latest/download/WorkbenchSetup.exe";
    private const int BufferSize = 128 * 1024;

    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Workbench-Updater/1.2.0");
        return client;
    }

    public static async Task<UpdateResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        Exception? apiError = null;
        try
        {
            using var response = await SendWithRetryAsync(
                CreateApiRequest,
                HttpCompletionOption.ResponseContentRead,
                TimeSpan.FromSeconds(45),
                2,
                cancellationToken);

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            string tag = document.RootElement.GetProperty("tag_name").GetString() ?? "0.0.0";
            var asset = document.RootElement.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(a => string.Equals(a.GetProperty("name").GetString(), "WorkbenchSetup.exe", StringComparison.OrdinalIgnoreCase));

            if (asset.ValueKind == JsonValueKind.Undefined)
                throw new InvalidDataException("最新发布中没有 WorkbenchSetup.exe 安装包。");

            string url = asset.GetProperty("browser_download_url").GetString()
                ?? throw new InvalidDataException("安装包下载地址无效。");
            long size = asset.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0;
            return BuildResult(tag, url, size);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            apiError = ex;
        }

        try
        {
            using var response = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl),
                HttpCompletionOption.ResponseHeadersRead,
                TimeSpan.FromSeconds(45),
                1,
                cancellationToken);

            string finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? string.Empty;
            const string marker = "/releases/tag/";
            int markerIndex = finalUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                throw new InvalidDataException("无法识别 GitHub 最新版本号。");

            string tag = Uri.UnescapeDataString(finalUrl[(markerIndex + marker.Length)..]).TrimEnd('/');
            return BuildResult(tag, LatestInstallerUrl, 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception fallbackError)
        {
            throw new HttpRequestException(
                "暂时无法连接 GitHub 更新服务，请检查网络后重试。",
                new AggregateException(apiError!, fallbackError));
        }
    }

    public static async Task<string> DownloadAsync(
        UpdateResult update,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken cancellationToken = default)
    {
        string target = Path.Combine(Path.GetTempPath(), $"WorkbenchSetup-{update.Version}.exe");
        try
        {
            using var response = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl),
                HttpCompletionOption.ResponseHeadersRead,
                TimeSpan.FromSeconds(90),
                3,
                cancellationToken);

            long? totalBytes = response.Content.Headers.ContentLength;
            if ((!totalBytes.HasValue || totalBytes.Value <= 0) && update.Size > 0)
                totalBytes = update.Size;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                target,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var stopwatch = Stopwatch.StartNew();
            var buffer = new byte[BufferSize];
            long received = 0;
            long lastReportedBytes = 0;
            long lastReportedMilliseconds = 0;

            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0) break;

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;

                long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                if (elapsedMilliseconds - lastReportedMilliseconds >= 150 || totalBytes.HasValue && received >= totalBytes.Value)
                {
                    ReportProgress(progress, received, totalBytes, stopwatch.Elapsed);
                    lastReportedMilliseconds = elapsedMilliseconds;
                    lastReportedBytes = received;
                }
            }

            if (received != lastReportedBytes)
                ReportProgress(progress, received, totalBytes, stopwatch.Elapsed);

            if (totalBytes.HasValue && received < totalBytes.Value)
                throw new EndOfStreamException("安装包下载不完整，请重新尝试更新。");

            await destination.FlushAsync(cancellationToken);
            return target;
        }
        catch
        {
            try { if (File.Exists(target)) File.Delete(target); } catch { }
            throw;
        }
    }

    public static void StartInstaller(string installerPath)
    {
        int processId = Process.GetCurrentProcess().Id;
        var process = Process.Start(new ProcessStartInfo(
            installerPath,
            $"/VERYSILENT /UPDATE /PID {processId}")
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? Path.GetTempPath()
        });

        if (process is null)
            throw new InvalidOperationException("无法启动新版安装包。");
    }

    private static UpdateResult BuildResult(string tag, string downloadUrl, long size)
    {
        string versionText = tag.Trim().TrimStart('v', 'V');
        int suffixIndex = versionText.IndexOfAny(new[] { '-', '+' });
        if (suffixIndex >= 0) versionText = versionText[..suffixIndex];
        if (!Version.TryParse(versionText, out var latest))
            throw new InvalidDataException($"无法识别版本号：{tag}");

        var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        return new UpdateResult(latest > current, versionText, downloadUrl, size);
    }

    private static HttpRequestMessage CreateApiRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return request;
    }

    private static void ReportProgress(
        IProgress<DownloadProgressInfo>? progress,
        long received,
        long? totalBytes,
        TimeSpan elapsed)
    {
        double seconds = Math.Max(elapsed.TotalSeconds, 0.001);
        double speed = received / seconds;
        double? percentage = totalBytes is > 0 ? received * 100d / totalBytes.Value : null;
        TimeSpan? remaining = totalBytes is > 0 && speed > 0
            ? TimeSpan.FromSeconds(Math.Max(0, (totalBytes.Value - received) / speed))
            : null;
        progress?.Report(new DownloadProgressInfo(received, totalBytes, percentage, speed, elapsed, remaining));
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        HttpCompletionOption completionOption,
        TimeSpan attemptTimeout,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(attemptTimeout);
            using var request = requestFactory();
            try
            {
                var response = await Client.SendAsync(request, completionOption, timeout.Token);
                if (response.IsSuccessStatusCode) return response;

                if (attempt < maxAttempts && ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout))
                {
                    lastError = new HttpRequestException($"更新服务器返回 {(int)response.StatusCode}。", null, response.StatusCode);
                    response.Dispose();
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = new TimeoutException($"连接更新服务器超过 {attemptTimeout.TotalSeconds:0} 秒。", ex);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                lastError = ex;
            }

            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(attempt * 1.5), cancellationToken);
        }

        throw lastError ?? new HttpRequestException("连接更新服务器失败。");
    }
}
