using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;

internal static class WorkbenchBootstrapper
{
    const string Version = "1.0.4";
    const string FullInstallerUrl = "https://github.com/li85120194-netizen/Workbench/releases/download/v1.0.4/WorkbenchFullSetup.exe";

    [STAThread]
    static void Main()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new UpdateForm());
    }

    sealed class UpdateForm : Form
    {
        readonly Label status = new Label();
        readonly Label amount = new Label();
        readonly Label percent = new Label();
        readonly Label speed = new Label();
        readonly Label elapsed = new Label();
        readonly Label remaining = new Label();
        readonly ProgressBar progress = new ProgressBar();
        readonly Button cancel = new Button();
        readonly Timer clock = new Timer();
        readonly Stopwatch stopwatch = new Stopwatch();
        readonly string target = Path.Combine(Path.GetTempPath(), "WorkbenchFullSetup-" + Version + ".exe");

        TimeoutWebClient client;
        long receivedBytes;
        long totalBytes;
        int attempt;
        bool finished;
        bool userCancelled;

        internal UpdateForm()
        {
            Text = "工具箱自动更新";
            ClientSize = new Size(560, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(244, 246, 250);
            Font = new Font("Microsoft YaHei UI", 9F);
            try { Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location); } catch { }

            var title = new Label { Text = "正在更新工具箱 v" + Version, Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold), AutoSize = true, Location = new Point(30, 24), ForeColor = Color.FromArgb(23, 32, 51) };
            status.Text = "正在连接 GitHub，准备下载完整安装包…";
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

            var hint = new Label { Text = "下载完成后将自动安装，并重新打开工具箱。", AutoSize = true, Location = new Point(32, 282), ForeColor = Color.FromArgb(104, 115, 134) };
            cancel.Text = "取消更新";
            cancel.Size = new Size(96, 34);
            cancel.Location = new Point(432, 272);
            cancel.Click += delegate { CancelDownload(); };

            Controls.AddRange(new Control[] { title, status, progress, amount, percent, infoPanel, hint, cancel });
            Shown += delegate { BeginDownload(); };
            FormClosing += OnFormClosing;
            clock.Interval = 500;
            clock.Tick += delegate { RefreshTiming(); };
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
            attempt++;
            receivedBytes = 0;
            totalBytes = 0;
            progress.Value = 0;
            percent.Text = "--%";
            amount.Text = "准备下载…";
            status.Text = attempt == 1 ? "正在下载完整安装包…" : "网络波动，正在第 " + attempt + " 次重试…";
            try { if (File.Exists(target)) File.Delete(target); } catch { }

            client = new TimeoutWebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "Workbench-Bootstrapper/" + Version;
            client.DownloadProgressChanged += OnDownloadProgress;
            client.DownloadFileCompleted += OnDownloadCompleted;
            stopwatch.Restart();
            clock.Start();
            client.DownloadFileAsync(new Uri(FullInstallerUrl), target);
        }

        void OnDownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            receivedBytes = e.BytesReceived;
            totalBytes = e.TotalBytesToReceive;
            int value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
            progress.Value = value;
            percent.Text = value + "%";
            amount.Text = totalBytes > 0
                ? "已下载 " + FormatBytes(receivedBytes) + " / " + FormatBytes(totalBytes)
                : "已下载 " + FormatBytes(receivedBytes);
            RefreshTiming();
        }

        void OnDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            client.Dispose();
            client = null;
            if (e.Cancelled || userCancelled) { finished = true; Close(); return; }
            if (e.Error != null)
            {
                if (attempt < 3)
                {
                    var retry = new Timer { Interval = attempt * 1500 };
                    retry.Tick += delegate { retry.Stop(); retry.Dispose(); BeginDownload(); };
                    retry.Start();
                    return;
                }
                clock.Stop();
                status.Text = "下载失败。";
                cancel.Text = "关闭";
                cancel.Enabled = true;
                MessageBox.Show("更新下载失败：" + e.Error.Message + "\n\n请检查网络后重试。", "工具箱", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                finished = true;
                return;
            }

            try
            {
                ValidateInstaller(target);
                clock.Stop();
                progress.Value = 100;
                percent.Text = "100%";
                remaining.Text = "00:00";
                status.Text = "下载完成，正在启动安装程序…";
                cancel.Enabled = false;
                var start = new ProcessStartInfo(target, "/VERYSILENT /UPDATE /PID " + Process.GetCurrentProcess().Id) { UseShellExecute = true };
                if (Process.Start(start) == null) throw new InvalidOperationException("无法启动完整安装包。");
                finished = true;
                Close();
            }
            catch (Exception ex)
            {
                clock.Stop();
                finished = true;
                cancel.Text = "关闭";
                MessageBox.Show("启动安装失败：" + ex.Message, "工具箱", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void RefreshTiming()
        {
            elapsed.Text = FormatTime(stopwatch.Elapsed);
            double seconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
            double bytesPerSecond = receivedBytes / seconds;
            speed.Text = receivedBytes > 0 ? FormatBytes((long)bytesPerSecond) + "/秒" : "--";
            remaining.Text = totalBytes > 0 && bytesPerSecond > 0
                ? FormatTime(TimeSpan.FromSeconds(Math.Max(0, (totalBytes - receivedBytes) / bytesPerSecond)))
                : "计算中…";
        }

        void CancelDownload()
        {
            if (finished) { Close(); return; }
            userCancelled = true;
            cancel.Enabled = false;
            status.Text = "正在取消更新…";
            if (client != null) client.CancelAsync();
            else { finished = true; Close(); }
        }

        void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (finished) return;
            e.Cancel = true;
            CancelDownload();
        }

        static void ValidateInstaller(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < 10 * 1024 * 1024) throw new InvalidDataException("完整安装包下载不完整。");
            using (var stream = file.OpenRead())
            {
                if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') throw new InvalidDataException("下载的文件不是有效安装包。");
            }
        }

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

    sealed class TimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            request.Timeout = 60000;
            var http = request as HttpWebRequest;
            if (http != null) http.ReadWriteTimeout = 60000;
            return request;
        }
    }
}
