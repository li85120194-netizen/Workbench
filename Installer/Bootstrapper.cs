using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("工具箱在线安装程序")]
[assembly: AssemblyProduct("工具箱")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]
[assembly: AssemblyInformationalVersion("1.2.1")]

internal static class WorkbenchBootstrapper
{
    const string Version = "1.2.1";
    const string OriginUrl = "https://github.com/li85120194-netizen/Workbench/releases/download/v1.2.1/WorkbenchFullSetup.exe";
    static long ExpectedSize;
    static string ExpectedSha256;

    [STAThread]
    static void Main(string[] args)
    {
        try { LoadPayloadInfo(); }
        catch (Exception ex)
        {
            MessageBox.Show("更新程序配置损坏：" + ex.Message, "工具箱", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        ServicePointManager.DefaultConnectionLimit = 12;
        bool downloadOnly = Array.Exists(args, delegate(string value) { return value.Equals("/DOWNLOADONLY", StringComparison.OrdinalIgnoreCase); });
        if (downloadOnly) Environment.ExitCode = 1;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new UpdateForm(downloadOnly));
    }

    static void LoadPayloadInfo()
    {
        using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WorkbenchPayloadInfo"))
        using (var reader = new StreamReader(stream))
        {
            string[] values = reader.ReadToEnd().Trim().Split('|');
            if (values.Length != 2 || !long.TryParse(values[0], out ExpectedSize) || values[1].Length != 64)
                throw new InvalidDataException("无法读取完整安装包校验信息。");
            ExpectedSha256 = values[1];
        }
    }

    sealed class UpdateForm : Form
    {
        const int SegmentCount = 4;
        static readonly string ProxyCom = "https://gh-proxy.com/" + OriginUrl;
        static readonly string ProxyOrg = "https://gh-proxy.org/" + OriginUrl;
        static readonly string GhFast = "https://ghfast.top/" + OriginUrl;
        static readonly string GhProxyNet = "https://ghproxy.net/" + OriginUrl;

        readonly Label status = new Label();
        readonly Label amount = new Label();
        readonly Label percent = new Label();
        readonly Label speed = new Label();
        readonly Label elapsed = new Label();
        readonly Label remaining = new Label();
        readonly ProgressBar progress = new ProgressBar();
        readonly Button cancel = new Button();
        readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();
        readonly Stopwatch stopwatch = new Stopwatch();
        readonly string target = Path.Combine(Path.GetTempPath(), "WorkbenchFullSetup-" + Version + ".exe");
        readonly bool downloadOnly;

        long receivedBytes;
        int completedSegments;
        int completionSignaled;
        volatile bool stopRequested;
        bool finished;

        internal UpdateForm(bool isDownloadOnly)
        {
            downloadOnly = isDownloadOnly;
            Text = "工具箱自动更新";
            ClientSize = new Size(560, 350);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(244, 246, 250);
            Font = new Font("Microsoft YaHei UI", 9F);
            if (downloadOnly) { ShowInTaskbar = false; Opacity = 0; }
            try { Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location); } catch { }

            var title = new Label { Text = "正在更新工具箱 v" + Version, Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold), AutoSize = true, Location = new Point(30, 24), ForeColor = Color.FromArgb(23, 32, 51) };
            status.Text = "正在连接国内加速线路…";
            status.AutoSize = true;
            status.Location = new Point(32, 70);
            status.ForeColor = Color.FromArgb(104, 115, 134);

            progress.Location = new Point(32, 108);
            progress.Size = new Size(496, 20);
            progress.Minimum = 0;
            progress.Maximum = 100;

            amount.Text = "准备下载…";
            amount.AutoSize = true;
            amount.Location = new Point(32, 142);
            amount.ForeColor = Color.FromArgb(57, 68, 90);
            percent.Text = "--%";
            percent.AutoSize = true;
            percent.Location = new Point(485, 142);
            percent.ForeColor = Color.FromArgb(22, 119, 255);
            percent.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);

            var infoPanel = new Panel { Location = new Point(32, 178), Size = new Size(496, 72), BackColor = Color.White };
            AddInfo(infoPanel, "下载速度", speed, 14);
            AddInfo(infoPanel, "已用时间", elapsed, 180);
            AddInfo(infoPanel, "预计剩余", remaining, 346);
            speed.Text = "--";
            elapsed.Text = "00:00";
            remaining.Text = "计算中…";

            var sourceHint = new Label { Text = "四路并行下载 · 失败自动换线 · 完成后校验 SHA-256", AutoSize = true, Location = new Point(32, 266), ForeColor = Color.FromArgb(22, 119, 255) };
            var hint = new Label { Text = "下载完成后将自动安装，并重新打开工具箱。", AutoSize = true, Location = new Point(32, 304), ForeColor = Color.FromArgb(104, 115, 134) };
            cancel.Text = "取消更新";
            cancel.Size = new Size(96, 34);
            cancel.Location = new Point(432, 294);
            cancel.Click += delegate { CancelDownload(); };

            Controls.AddRange(new Control[] { title, status, progress, amount, percent, infoPanel, sourceHint, hint, cancel });
            Shown += delegate { BeginDownload(); };
            FormClosing += OnFormClosing;
            clock.Interval = 250;
            clock.Tick += delegate { RefreshProgress(); };
        }

        static void AddInfo(Control parent, string caption, Label value, int left)
        {
            var label = new Label { Text = caption, AutoSize = true, Location = new Point(left, 12), ForeColor = Color.FromArgb(122, 132, 150), Font = new Font("Microsoft YaHei UI", 8F) };
            value.AutoSize = true;
            value.Location = new Point(left, 38);
            value.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            parent.Controls.Add(label);
            parent.Controls.Add(value);
        }

        void BeginDownload()
        {
            CleanupParts(true);
            receivedBytes = 0;
            completedSegments = 0;
            completionSignaled = 0;
            stopRequested = false;
            status.Text = "正在使用国内加速线路并行下载完整安装包…";
            stopwatch.Restart();
            clock.Start();
            for (int i = 0; i < SegmentCount; i++) ThreadPool.QueueUserWorkItem(DownloadSegment, i);
        }

        void DownloadSegment(object state)
        {
            int index = (int)state;
            long segmentSize = ExpectedSize / SegmentCount;
            long start = index * segmentSize;
            long end = index == SegmentCount - 1 ? ExpectedSize - 1 : start + segmentSize - 1;
            long expectedLength = end - start + 1;
            string partPath = GetPartPath(index);
            Exception lastError = null;

            foreach (string url in GetSources(index))
            {
                if (stopRequested) return;
                long written = 0;
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "GET";
                    request.UserAgent = "Workbench-Bootstrapper/" + Version;
                    request.Timeout = 15000;
                    request.ReadWriteTimeout = 20000;
                    request.KeepAlive = false;
                    request.AddRange(start, end);

                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        string contentRange = response.Headers["Content-Range"];
                        string expectedPrefix = "bytes " + start + "-";
                        if (response.StatusCode != HttpStatusCode.PartialContent || string.IsNullOrEmpty(contentRange) || !contentRange.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("下载线路不支持可靠的分段传输。");

                        using (var input = response.GetResponseStream())
                        using (var output = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[128 * 1024];
                            while (written < expectedLength)
                            {
                                if (stopRequested) throw new OperationCanceledException();
                                int wanted = (int)Math.Min(buffer.Length, expectedLength - written);
                                int read = input.Read(buffer, 0, wanted);
                                if (read <= 0) throw new EndOfStreamException("分段下载提前结束。");
                                output.Write(buffer, 0, read);
                                written += read;
                                Interlocked.Add(ref receivedBytes, read);
                            }
                        }
                    }

                    if (written != expectedLength) throw new EndOfStreamException("分段大小不完整。");
                    if (Interlocked.Increment(ref completedSegments) == SegmentCount) SignalCompletion(true, null);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (written > 0) Interlocked.Add(ref receivedBytes, -written);
                    TryDelete(partPath);
                    if (stopRequested) return;
                }
            }

            stopRequested = true;
            SignalCompletion(false, lastError ?? new WebException("所有下载线路均不可用。"));
        }

        string[] GetSources(int segment)
        {
            if (segment == 0) return new[] { ProxyCom, ProxyOrg, GhFast, GhProxyNet, OriginUrl };
            if (segment == 1) return new[] { ProxyOrg, GhFast, ProxyCom, GhProxyNet, OriginUrl };
            if (segment == 2) return new[] { GhFast, ProxyCom, ProxyOrg, GhProxyNet, OriginUrl };
            return new[] { ProxyCom, GhFast, ProxyOrg, GhProxyNet, OriginUrl };
        }

        void SignalCompletion(bool success, Exception error)
        {
            if (Interlocked.Exchange(ref completionSignaled, 1) != 0) return;
            try { BeginInvoke((MethodInvoker)delegate { CompleteDownload(success, error); }); } catch { }
        }

        void CompleteDownload(bool success, Exception error)
        {
            clock.Stop();
            if (!success)
            {
                CleanupParts(true);
                if (downloadOnly)
                {
                    Environment.ExitCode = 2;
                    finished = true;
                    Close();
                    return;
                }
                status.Text = "所有下载线路均连接失败。";
                cancel.Text = "关闭";
                cancel.Enabled = true;
                finished = true;
                MessageBox.Show("更新下载失败：" + error.Message + "\n\n请稍后重试；程序会自动重新选择线路。", "工具箱", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                status.Text = "下载完成，正在合并并校验安装包…";
                Application.DoEvents();
                CombineParts();
                ValidateInstaller(target);
                CleanupParts(false);
                progress.Value = 100;
                percent.Text = "100%";
                amount.Text = "已下载 " + FormatBytes(ExpectedSize) + " / " + FormatBytes(ExpectedSize);
                remaining.Text = "00:00";

                if (downloadOnly)
                {
                    TryDelete(target);
                    Environment.ExitCode = 0;
                    finished = true;
                    Close();
                    return;
                }

                status.Text = "校验通过，正在启动安装程序…";
                cancel.Enabled = false;
                var start = new ProcessStartInfo(target, "/VERYSILENT /UPDATE /PID " + Process.GetCurrentProcess().Id) { UseShellExecute = true };
                if (Process.Start(start) == null) throw new InvalidOperationException("无法启动完整安装包。");
                finished = true;
                Close();
            }
            catch (Exception ex)
            {
                CleanupParts(true);
                finished = true;
                if (downloadOnly)
                {
                    Environment.ExitCode = 3;
                    Close();
                    return;
                }
                cancel.Text = "关闭";
                MessageBox.Show("安装包校验或启动失败：" + ex.Message, "工具箱", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void CombineParts()
        {
            using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                for (int i = 0; i < SegmentCount; i++)
                {
                    using (var input = new FileStream(GetPartPath(i), FileMode.Open, FileAccess.Read, FileShare.Read)) input.CopyTo(output);
                }
            }
        }

        static void ValidateInstaller(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length != ExpectedSize) throw new InvalidDataException("完整安装包大小不正确。");
            string hash;
            using (var stream = file.OpenRead())
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            if (!hash.Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 校验失败，文件可能不完整，已拒绝运行。");
        }

        void RefreshProgress()
        {
            long received = Math.Max(0, Math.Min(ExpectedSize, Interlocked.Read(ref receivedBytes)));
            int value = (int)Math.Min(100, received * 100L / ExpectedSize);
            progress.Value = value;
            percent.Text = value + "%";
            amount.Text = "已下载 " + FormatBytes(received) + " / " + FormatBytes(ExpectedSize);
            elapsed.Text = FormatTime(stopwatch.Elapsed);
            double seconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
            double bytesPerSecond = received / seconds;
            speed.Text = received > 0 ? FormatBytes((long)bytesPerSecond) + "/秒" : "--";
            remaining.Text = received > 0 && bytesPerSecond > 0
                ? FormatTime(TimeSpan.FromSeconds(Math.Max(0, (ExpectedSize - received) / bytesPerSecond)))
                : "计算中…";
        }

        void CancelDownload()
        {
            if (finished) { Close(); return; }
            stopRequested = true;
            cancel.Enabled = false;
            status.Text = "正在取消更新…";
            var closeTimer = new System.Windows.Forms.Timer { Interval = 600 };
            closeTimer.Tick += delegate { closeTimer.Stop(); closeTimer.Dispose(); finished = true; CleanupParts(true); Close(); };
            closeTimer.Start();
        }

        void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (finished) return;
            e.Cancel = true;
            CancelDownload();
        }

        string GetPartPath(int index) { return target + ".part" + index; }
        void CleanupParts(bool includeTarget)
        {
            for (int i = 0; i < SegmentCount; i++) TryDelete(GetPartPath(i));
            if (includeTarget) TryDelete(target);
        }
        static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

        static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = Math.Max(0, bytes);
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return value.ToString("0.##") + " " + units[unit];
        }

        static string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1) return string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
            return string.Format("{0:00}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }
    }
}
