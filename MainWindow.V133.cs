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
}