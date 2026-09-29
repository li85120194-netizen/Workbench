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
    private void AdvancedNav_Click(object sender,RoutedEventArgs e)=>ShowPage(AdvancedPage,AdvancedNav,"文件与实用工具");
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
        var osArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
        var processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
        HomeSystemOsText.Text = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        HomeSystemRuntimeText.Text = $"系统 {osArchitecture} · 进程 {processArchitecture} · .NET {Environment.Version}";
        var systemManufacturer = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer", "");
        var systemProduct = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName", "");
        HomeSystemDeviceText.Text = string.Join(" ", new[] { Environment.MachineName, systemManufacturer, systemProduct }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var cpu = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "未知处理器");
        HomeSystemCpuText.Text = $"{cpu} · {Environment.ProcessorCount} 逻辑处理器";
        var memory = new MemoryStatus { Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MemoryStatus>() };
        HomeSystemMemoryText.Text = GlobalMemoryStatusEx(ref memory) ? $"{memory.TotalPhysical / 1073741824d:F1} GB · 可用 {memory.AvailablePhysical / 1073741824d:F1} GB" : "读取失败";
        HomeSystemGpuText.Text = ReadGpuNames();
        var boardManufacturer = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardManufacturer", "");
        var boardProduct = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardProduct", "");
        HomeSystemBoardText.Text = JoinInfo(boardManufacturer, boardProduct, "未识别");
        var biosVendor = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "BIOSVendor", "");
        var biosVersion = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "BIOSVersion", "");
        var biosDate = ReadRegistryText(Microsoft.Win32.Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "BIOSReleaseDate", "");
        HomeSystemBiosText.Text = JoinInfo(biosVendor, biosVersion, biosDate, "未识别");
        HomeSystemMonitorText.Text = ReadMonitorNames();
        HomeSystemScreenText.Text = string.Join("；", System.Windows.Forms.Screen.AllScreens.Select((screen, index) => $"显示器 {index + 1}: {screen.Bounds.Width}×{screen.Bounds.Height}{(screen.Primary ? " 主屏" : "")}"));
        HomeSystemDiskText.Text = string.Join("；", DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => $"{d.Name} {d.TotalSize / 1073741824d:F0} GB（可用 {d.AvailableFreeSpace / 1073741824d:F0} GB）"));
        var networks = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback).Select(n => n.Speed > 0 ? $"{n.Name} {n.Speed / 1000000d:F0} Mbps" : n.Name).Distinct().ToArray();
        HomeSystemNetworkText.Text = networks.Length == 0 ? "未连接" : string.Join("；", networks);
    }

    private static string ReadRegistryText(Microsoft.Win32.RegistryKey root, string path, string name, string fallback)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            var raw = key?.GetValue(name);
            var value = raw is string[] values ? string.Join(" ", values) : raw?.ToString();
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
        catch { return fallback; }
    }

    private static string ReadGpuNames()
    {
        var names = new List<string>();
        try
        {
            using var video = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
            if (video is not null) foreach (var adapter in video.GetSubKeyNames())
            {
                using var key = video.OpenSubKey(adapter + @"\0000");
                var name = key?.GetValue("DriverDesc")?.ToString() ?? key?.GetValue("HardwareInformation.AdapterString")?.ToString();
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)) names.Add(name);
            }
        }
        catch { }
        return names.Count == 0 ? "未识别" : string.Join("；", names.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string ReadMonitorNames()
    {
        var names = new List<string>();
        try
        {
            using var display = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY");
            if (display is not null) foreach (var vendor in display.GetSubKeyNames())
            {
                using var vendorKey = display.OpenSubKey(vendor);
                if (vendorKey is null) continue;
                foreach (var instance in vendorKey.GetSubKeyNames())
                {
                    using var instanceKey = vendorKey.OpenSubKey(instance);
                    var name = instanceKey?.GetValue("FriendlyName")?.ToString() ?? instanceKey?.GetValue("DeviceDesc")?.ToString();
                    if (!string.IsNullOrWhiteSpace(name)) { var separator = name.LastIndexOf(';'); names.Add(separator >= 0 ? name[(separator + 1)..] : name); }
                }
            }
        }
        catch { }
        return names.Count == 0 ? $"{System.Windows.Forms.Screen.AllScreens.Length} 台显示器" : string.Join("；", names.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string JoinInfo(params string[] values)
    {
        var fallback = values[^1];
        var result = string.Join(" ", values.Take(values.Length - 1).Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

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