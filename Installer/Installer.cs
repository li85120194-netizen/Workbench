using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("工具箱安装程序")]
[assembly: AssemblyProduct("工具箱")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]
[assembly: AssemblyInformationalVersion("1.2.1")]

internal static class WorkbenchInstaller
{
    static readonly string DefaultInstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Workbench");
    static readonly string DesktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "工具箱.lnk");
    static readonly string StartShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "工具箱.lnk");
    static readonly string LegacyDesktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "工作台.lnk");

    [STAThread]
    static void Main(string[] args)
    {
        if (Array.Exists(args, a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))) { Uninstall(); return; }
        if (Array.Exists(args, a => a.Equals("/VERYSILENT", StringComparison.OrdinalIgnoreCase)))
        {
            WaitForParentProcess(args);
            Install(true, GetExistingInstallDir());
            return;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }

    internal static bool Install(bool launch, string installDirectory)
    {
        try
        {
            string installDir = Path.GetFullPath(string.IsNullOrWhiteSpace(installDirectory) ? DefaultInstallDir : installDirectory.Trim());
            string appPath = Path.Combine(installDir, "Workbench.exe");
            string uninstallPath = Path.Combine(installDir, "Uninstall.exe");
            foreach (var p in Process.GetProcessesByName("Workbench")) { try { p.CloseMainWindow(); p.WaitForExit(4000); if (!p.HasExited) p.Kill(); } catch { } }
            Directory.CreateDirectory(installDir);
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("WorkbenchPayload"))
            using (var output = new FileStream(appPath, FileMode.Create, FileAccess.Write)) { input.CopyTo(output); }
            File.Copy(Assembly.GetExecutingAssembly().Location, uninstallPath, true);
            CreateShortcut(DesktopShortcut, appPath, installDir);
            CreateShortcut(StartShortcut, appPath, installDir);
            TryDelete(LegacyDesktopShortcut);
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Workbench"))
            {
                key.SetValue("DisplayName", "工具箱"); key.SetValue("DisplayVersion", "1.2.1"); key.SetValue("Publisher", "Toolbox");
                key.SetValue("DisplayIcon", appPath); key.SetValue("UninstallString", "\"" + uninstallPath + "\" /uninstall");
                key.SetValue("InstallLocation", installDir); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
            }
            if (launch) Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) { MessageBox.Show("安装失败：" + ex.Message, "工具箱", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }

    static void Uninstall()
    {
        if (MessageBox.Show("确定要卸载工具箱吗？", "工具箱", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        string installDir = GetExistingInstallDir();
        string appPath = Path.Combine(installDir, "Workbench.exe");
        string uninstallPath = Path.Combine(installDir, "Uninstall.exe");
        foreach (var p in Process.GetProcessesByName("Workbench")) { try { p.Kill(); } catch { } }
        TryDelete(appPath); TryDelete(DesktopShortcut); TryDelete(StartShortcut); TryDelete(LegacyDesktopShortcut);
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Workbench", false); } catch { }
        MessageBox.Show("工具箱已卸载。用户设置与收纳撤回记录已保留。", "工具箱");
        MoveFileEx(uninstallPath, null, 4); MoveFileEx(installDir, null, 4);
    }

    static string GetExistingInstallDir()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Workbench"))
            {
                string value = key == null ? null : key.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        catch { }
        return DefaultInstallDir;
    }

    static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    static void WaitForParentProcess(string[] args)
    {
        int index = Array.FindIndex(args, a => a.Equals("/PID", StringComparison.OrdinalIgnoreCase));
        int processId;
        if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out processId)) return;
        try { Process.GetProcessById(processId).WaitForExit(20000); } catch { }
    }

    static void CreateShortcut(string shortcutPath, string target, string workingDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
        Type type = Type.GetTypeFromProgID("WScript.Shell"); dynamic shell = Activator.CreateInstance(type);
        dynamic shortcut = shell.CreateShortcut(shortcutPath); shortcut.TargetPath = target; shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = target + ",0"; shortcut.Description = "工具箱"; shortcut.Save();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern bool MoveFileEx(string existing, string next, int flags);

    sealed class InstallerForm : Form
    {
        readonly Button install = new Button(); readonly CheckBox launch = new CheckBox(); readonly TextBox installPath = new TextBox();
        public InstallerForm()
        {
            Text = "安装工具箱"; ClientSize = new Size(560, 390); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(244,246,250);
            try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }
            var infinity = new Label { Text="∞", Font=new Font("Segoe UI",52,FontStyle.Bold), ForeColor=Color.FromArgb(48,174,230), AutoSize=true, Location=new Point(42,30) };
            var title = new Label { Text="工具箱", Font=new Font("Microsoft YaHei UI",22,FontStyle.Bold), AutoSize=true, Location=new Point(145,48) };
            var desc = new Label { Text="演示 · 计时 · 文件 · 图片 · PDF", Font=new Font("Microsoft YaHei UI",11), ForeColor=Color.FromArgb(92,104,124), AutoSize=true, Location=new Point(148,92) };
            var pathLabel = new Label { Text="安装位置", Font=new Font("Microsoft YaHei UI",9), ForeColor=Color.FromArgb(92,104,124), AutoSize=true, Location=new Point(45,145) };
            installPath.Text=GetExistingInstallDir(); installPath.Font=new Font("Microsoft YaHei UI",9); installPath.Location=new Point(45,170); installPath.Size=new Size(400,28);
            var browse = new Button { Text="浏览…", Font=new Font("Microsoft YaHei UI",9), Location=new Point(455,169), Size=new Size(72,30) };
            browse.Click += (s,e) => { using(var dialog=new FolderBrowserDialog { Description="选择工具箱安装位置", SelectedPath=installPath.Text, ShowNewFolderButton=true }) if(dialog.ShowDialog()==DialogResult.OK) installPath.Text=dialog.SelectedPath; };
            var hint = new Label { Text="更新时会继续使用当前安装位置。建议选择当前用户有写入权限的目录。", Font=new Font("Microsoft YaHei UI",8), ForeColor=Color.FromArgb(112,123,143), AutoSize=true, Location=new Point(45,207) };
            launch.Text="安装完成后启动工具箱"; launch.Checked=true; launch.AutoSize=true; launch.Location=new Point(45,245);
            install.Text="立即安装"; install.Font=new Font("Microsoft YaHei UI",11,FontStyle.Bold); install.Size=new Size(150,46); install.Location=new Point(365,310); install.BackColor=Color.FromArgb(22,119,255); install.ForeColor=Color.White; install.FlatStyle=FlatStyle.Flat;
            install.Click += async (s,e) =>
            {
                if(string.IsNullOrWhiteSpace(installPath.Text)){MessageBox.Show("请选择安装位置。","工具箱");return;}
                bool launchAfterInstall = launch.Checked;
                string selectedInstallPath = installPath.Text;
                install.Enabled=false; install.Text="正在安装…"; UseWaitCursor=true;
                bool ok=await System.Threading.Tasks.Task.Run(() => Install(launchAfterInstall,selectedInstallPath));
                UseWaitCursor=false;
                if(ok){ MessageBox.Show("安装完成，桌面已创建“工具箱”快捷方式。","工具箱"); Close(); }
                else { install.Enabled=true; install.Text="立即安装"; }
            };
            Controls.AddRange(new Control[]{infinity,title,desc,pathLabel,installPath,browse,hint,launch,install});
        }
    }
}
