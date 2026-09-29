using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Workbench;
public partial class MainWindow
{
    private string? _advancedFolder;
    private async void OcrImage_Click(object sender,RoutedEventArgs e){var d=new Microsoft.Win32.OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff"};if(d.ShowDialog(this)!=true)return;ScreenshotHint.Text="正在使用 Windows 本地 OCR…";try{var text=await LocalOcrService.RecognizeAsync(d.FileName);TextInputBox.Text=text;TextOutputBox.Text=text;ScreenshotHint.Text=$"识别完成：{text.Length} 个字符";ShowPage(TextPage,TextNav,"文本工具");}catch(Exception ex){ScreenshotHint.Text="OCR 失败："+ex.Message;}}
    private void AdvancedNav_Click(object sender,RoutedEventArgs e){ShowPage(AdvancedPage,AdvancedNav,"文件与实用工具");RefreshSystemInfo();}
    private void SelectAdvancedFolder_Click(object sender,RoutedEventArgs e){using var d=new System.Windows.Forms.FolderBrowserDialog{Description="选择要搜索或扫描的文件夹"};if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK){_advancedFolder=d.SelectedPath;AdvancedFolderText.Text=d.SelectedPath;}}
    private async void FileSearch_Click(object sender,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(_advancedFolder))return;var q=FileSearchQuery.Text.Trim();AdvancedResults.ItemsSource=new[]{"正在搜索…"};try{var items=await Task.Run(()=>Directory.EnumerateFiles(_advancedFolder,"*",SearchOption.AllDirectories).Where(x=>string.IsNullOrEmpty(q)||Path.GetFileName(x).Contains(q,StringComparison.OrdinalIgnoreCase)).Take(1000).ToList());AdvancedResults.ItemsSource=items;AdvancedStatus.Text=$"找到 {items.Count} 项（最多显示 1000 项）";}catch(Exception ex){AdvancedStatus.Text=ex.Message;}}
    private async void HashFile_Click(object sender,RoutedEventArgs e){var d=new Microsoft.Win32.OpenFileDialog();if(d.ShowDialog(this)!=true)return;AdvancedStatus.Text="正在计算哈希…";var lines=await Task.Run(()=>{using var s=File.OpenRead(d.FileName);var sha=Convert.ToHexString(SHA256.Create().ComputeHash(s));s.Position=0;var md5=Convert.ToHexString(MD5.Create().ComputeHash(s));return new[]{d.FileName,"SHA-256  "+sha,"MD5       "+md5};});AdvancedResults.ItemsSource=lines;AdvancedStatus.Text="哈希计算完成";}
    private async void DuplicateScan_Click(object sender,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(_advancedFolder))return;AdvancedStatus.Text="正在按大小和内容扫描…";try{var groups=await Task.Run(()=>Directory.EnumerateFiles(_advancedFolder,"*",SearchOption.AllDirectories).Select(x=>new FileInfo(x)).Where(x=>x.Length>0).GroupBy(x=>x.Length).Where(g=>g.Count()>1).SelectMany(g=>g.GroupBy(x=>HashPath(x.FullName)).Where(h=>h.Count()>1).Select(h=>$"{h.Count()} 个重复 · {FormatSize(g.Key)}\n  "+string.Join("\n  ",h.Select(x=>x.FullName)))).Take(200).ToList());AdvancedResults.ItemsSource=groups;AdvancedStatus.Text=$"发现 {groups.Count} 组重复文件；这里只预览，不会自动删除。";}catch(Exception ex){AdvancedStatus.Text=ex.Message;}}
    private static string HashPath(string p){using var s=File.OpenRead(p);return Convert.ToHexString(SHA256.Create().ComputeHash(s));}
    private static string FormatSize(long n)=>n>1048576?$"{n/1048576d:F1} MB":$"{n/1024d:F1} KB";
    private void GeneratePassword_Click(object sender,RoutedEventArgs e){const string chars="ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";var bytes=RandomNumberGenerator.GetBytes(20);PasswordResult.Text=new string(bytes.Select(b=>chars[b%chars.Length]).ToArray());}
    private void CopyPassword_Click(object sender,RoutedEventArgs e){if(!string.IsNullOrEmpty(PasswordResult.Text))System.Windows.Clipboard.SetText(PasswordResult.Text);}
    private void ConvertUnit_Click(object sender,RoutedEventArgs e){if(!double.TryParse(UnitInput.Text,out var v)){UnitResult.Text="请输入数字";return;}UnitResult.Text=(UnitType.SelectedIndex switch{0=>$"{v*1000:0.###} 米",1=>$"{v/1000:0.###} 千米",2=>$"{v*2.20462262:0.###} 磅",3=>$"{(v-32)*5/9:0.###} °C",_=>v.ToString()});}
    private void AnalyzeDisk_Click(object sender,RoutedEventArgs e){AdvancedResults.ItemsSource=DriveInfo.GetDrives().Where(d=>d.IsReady).Select(d=>$"{d.Name}  可用 {FormatSize(d.AvailableFreeSpace)} / 总计 {FormatSize(d.TotalSize)}  {d.DriveFormat}").ToList();AdvancedStatus.Text="磁盘概览已刷新";}
    private void MarkdownPreview_Click(object sender,RoutedEventArgs e){var s=MarkdownInput.Text;MarkdownOutput.Text=System.Text.RegularExpressions.Regex.Replace(s,@"(?m)^#{1,6}\s*","").Replace("**","").Replace("`","");}

    private void RefreshSystemInfo_Click(object sender, RoutedEventArgs e) => RefreshSystemInfo();

    private void RefreshSystemInfo()
    {
        SystemOsText.Text = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} · {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}";
        SystemCpuText.Text = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "未知处理器");
        var memory = new MemoryStatus { Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MemoryStatus>() };
        SystemMemoryText.Text = GlobalMemoryStatusEx(ref memory) ? $"{memory.TotalPhysical / 1073741824d:F1} GB" : "读取失败";
        SystemGpuText.Text = ReadGpuName();
        var manufacturer = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardManufacturer", "");
        var product = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardProduct", "");
        SystemBoardText.Text = string.Join(" ", new[] { manufacturer, product }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (string.IsNullOrWhiteSpace(SystemBoardText.Text)) SystemBoardText.Text = Environment.MachineName;
        SystemDiskText.Text = string.Join("   ", DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => $"{d.Name} {d.TotalSize / 1073741824d:F0} GB"));
    }

    private static string ReadRegistryText(Microsoft.Win32.RegistryKey root, string path, string name, string fallback)
    {
        try { using var key = root.OpenSubKey(path); return key?.GetValue(name)?.ToString()?.Trim() is { Length: > 0 } value ? value : fallback; }
        catch { return fallback; }
    }

    private static string ReadGpuName()
    {
        try
        {
            using var video = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
            if (video is null) return "未识别";
            foreach (var adapter in video.GetSubKeyNames())
            {
                using var key = video.OpenSubKey(adapter + @"\0000");
                var name = key?.GetValue("DriverDesc")?.ToString() ?? key?.GetValue("HardwareInformation.AdapterString")?.ToString();
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)) return name;
            }
        }
        catch { }
        return "未识别";
    }

    private void OpenGeekUninstaller_Click(object sender, RoutedEventArgs e)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "geek.exe"),
            Path.Combine(AppContext.BaseDirectory, "Geek.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "geek.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "geek.exe")
        };
        var executable = candidates.FirstOrDefault(File.Exists);
        if (executable is null)
        {
            GeekStatusText.Text = "未找到 geek.exe，已打开官方下载页面。下载并解压后可放到小二目录、桌面或下载目录。";
            OpenUrl("https://geekuninstaller.com/download");
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = true });
            GeekStatusText.Text = $"已启动：{executable}";
        }
        catch (Exception ex) { GeekStatusText.Text = "启动失败：" + ex.Message; }
    }

    private void DownloadGeekUninstaller_Click(object sender, RoutedEventArgs e) => OpenUrl("https://geekuninstaller.com/download");

    private void OpenWindowsApps_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true }); }
        catch (Exception ex) { GeekStatusText.Text = "无法打开系统设置：" + ex.Message; }
    }

    private static void OpenUrl(string url) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);}