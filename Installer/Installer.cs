using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class WorkbenchInstaller
{
    static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Workbench");
    static readonly string AppPath = Path.Combine(InstallDir, "Workbench.exe");
    static readonly string UninstallPath = Path.Combine(InstallDir, "Uninstall.exe");
    static readonly string DesktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "工作台.lnk");
    static readonly string StartShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "工作台.lnk");

    [STAThread]
    static void Main(string[] args)
    {
        if (Array.Exists(args, a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))) { Uninstall(); return; }
        if (Array.Exists(args, a => a.Equals("/VERYSILENT", StringComparison.OrdinalIgnoreCase))) { Install(true); return; }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }

    internal static bool Install(bool launch)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("Workbench")) { try { p.CloseMainWindow(); p.WaitForExit(4000); if (!p.HasExited) p.Kill(); } catch { } }
            Directory.CreateDirectory(InstallDir);
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("WorkbenchPayload"))
            using (var output = new FileStream(AppPath, FileMode.Create, FileAccess.Write)) { input.CopyTo(output); }
            File.Copy(Assembly.GetExecutingAssembly().Location, UninstallPath, true);
            CreateShortcut(DesktopShortcut, AppPath);
            CreateShortcut(StartShortcut, AppPath);
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Workbench"))
            {
                key.SetValue("DisplayName", "工作台"); key.SetValue("DisplayVersion", "1.0.0"); key.SetValue("Publisher", "Workbench");
                key.SetValue("DisplayIcon", AppPath); key.SetValue("UninstallString", "\"" + UninstallPath + "\" /uninstall");
                key.SetValue("InstallLocation", InstallDir); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
            }
            if (launch) Process.Start(new ProcessStartInfo(AppPath) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) { if (!launch) MessageBox.Show("安装失败：" + ex.Message, "工作台", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }

    static void Uninstall()
    {
        if (MessageBox.Show("确定要卸载工作台吗？", "工作台", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        foreach (var p in Process.GetProcessesByName("Workbench")) { try { p.Kill(); } catch { } }
        TryDelete(AppPath); TryDelete(DesktopShortcut); TryDelete(StartShortcut);
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Workbench", false); } catch { }
        MessageBox.Show("工作台已卸载。用户设置与收纳撤回记录已保留。", "工作台");
        MoveFileEx(UninstallPath, null, 4); MoveFileEx(InstallDir, null, 4);
    }

    static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    static void CreateShortcut(string shortcutPath, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
        Type type = Type.GetTypeFromProgID("WScript.Shell"); dynamic shell = Activator.CreateInstance(type);
        dynamic shortcut = shell.CreateShortcut(shortcutPath); shortcut.TargetPath = target; shortcut.WorkingDirectory = InstallDir;
        shortcut.IconLocation = target + ",0"; shortcut.Description = "工作台"; shortcut.Save();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern bool MoveFileEx(string existing, string next, int flags);

    sealed class InstallerForm : Form
    {
        readonly Button install = new Button(); readonly CheckBox launch = new CheckBox();
        public InstallerForm()
        {
            Text = "安装工作台"; ClientSize = new Size(520, 330); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(244,246,250);
            try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }
            var infinity = new Label { Text="∞", Font=new Font("Segoe UI",52,FontStyle.Bold), ForeColor=Color.FromArgb(48,174,230), AutoSize=true, Location=new Point(42,30) };
            var title = new Label { Text="工作台", Font=new Font("Microsoft YaHei UI",22,FontStyle.Bold), AutoSize=true, Location=new Point(145,48) };
            var desc = new Label { Text="鼠标高亮 · 倒计时 · 桌面收纳", Font=new Font("Microsoft YaHei UI",11), ForeColor=Color.FromArgb(92,104,124), AutoSize=true, Location=new Point(148,92) };
            var path = new Label { Text="安装位置："+InstallDir, Font=new Font("Microsoft YaHei UI",9), ForeColor=Color.FromArgb(92,104,124), AutoSize=true, Location=new Point(45,160) };
            launch.Text="安装完成后启动工作台"; launch.Checked=true; launch.AutoSize=true; launch.Location=new Point(45,205);
            install.Text="立即安装"; install.Font=new Font("Microsoft YaHei UI",11,FontStyle.Bold); install.Size=new Size(150,46); install.Location=new Point(325,245); install.BackColor=Color.FromArgb(22,119,255); install.ForeColor=Color.White; install.FlatStyle=FlatStyle.Flat;
            install.Click += (s,e) => { install.Enabled=false; install.Text="正在安装…"; if(Install(launch.Checked)){ MessageBox.Show("安装完成，桌面已创建“工作台”快捷方式。","工作台"); Close(); } else { install.Enabled=true; install.Text="立即安装"; } };
            Controls.AddRange(new Control[]{infinity,title,desc,path,launch,install});
        }
    }
}
