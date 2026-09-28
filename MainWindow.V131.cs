using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Workbench;
public partial class MainWindow
{
    private Bitmap? _lastScreenshot;
    private string _textUndo = string.Empty;
    private readonly List<PinImageWindow> _pins = new();

    private void ScreenshotNav_Click(object sender, RoutedEventArgs e)=>ShowPage(ScreenshotPage,ScreenshotNav,"截图与贴图");
    private void TextNav_Click(object sender, RoutedEventArgs e)=>ShowPage(TextPage,TextNav,"文本工具");
    private void CaptureRegion_Click(object sender,RoutedEventArgs e){Hide();var w=new ScreenshotSelectionWindow();if(w.ShowDialog()==true&&w.Result!=null)SetScreenshot(w.Result);Show();Activate();}
    private void CaptureFull_Click(object sender,RoutedEventArgs e){Hide();System.Threading.Thread.Sleep(180);var a=System.Windows.Forms.SystemInformation.VirtualScreen;var b=new Bitmap(a.Width,a.Height);using(var g=Graphics.FromImage(b))g.CopyFromScreen(a.Left,a.Top,0,0,a.Size);SetScreenshot(b);Show();Activate();}
    private void SetScreenshot(Bitmap b){_lastScreenshot?.Dispose();_lastScreenshot=(Bitmap)b.Clone();ScreenshotPreview.Source=ScreenshotSelectionWindow.ToSource(_lastScreenshot);ScreenshotHint.Text=$"已捕获 {_lastScreenshot.Width} × {_lastScreenshot.Height}";b.Dispose();}
    private void CopyScreenshot_Click(object sender,RoutedEventArgs e){if(_lastScreenshot!=null)System.Windows.Clipboard.SetImage(ScreenshotSelectionWindow.ToSource(_lastScreenshot));}
    private void SaveScreenshot_Click(object sender,RoutedEventArgs e){if(_lastScreenshot==null)return;var d=new Microsoft.Win32.SaveFileDialog{Filter="PNG 图片|*.png|JPEG 图片|*.jpg",FileName=$"截图_{DateTime.Now:yyyyMMdd_HHmmss}.png"};if(d.ShowDialog(this)==true)_lastScreenshot.Save(d.FileName,d.FilterIndex==2?System.Drawing.Imaging.ImageFormat.Jpeg:System.Drawing.Imaging.ImageFormat.Png);}
    private void PinScreenshot_Click(object sender,RoutedEventArgs e){if(_lastScreenshot==null)return;var p=new PinImageWindow(_lastScreenshot);_pins.Add(p);p.Closed+=(_,_)=>_pins.Remove(p);p.Show();}
    private void AnnotateScreenshot_Click(object sender,RoutedEventArgs e)=>ToggleAnnotation();

    private void TextOperation_Click(object sender,RoutedEventArgs e){if(sender is not System.Windows.Controls.Button b)return;_textUndo=TextInputBox.Text;var s=TextInputBox.Text;try{TextOutputBox.Text=(b.Tag?.ToString()) switch{"trim"=>string.Join(Environment.NewLine,s.Replace("\r","").Split('\n').Select(x=>x.Trim())),"blank"=>string.Join(Environment.NewLine,s.Replace("\r","").Split('\n').Where(x=>!string.IsNullOrWhiteSpace(x))),"spaces"=>Regex.Replace(s,@"[ \t]+"," "),"unique"=>string.Join(Environment.NewLine,s.Replace("\r","").Split('\n').Distinct()),"upper"=>s.ToUpperInvariant(),"lower"=>s.ToLowerInvariant(),"sort"=>string.Join(Environment.NewLine,s.Replace("\r","").Split('\n').OrderBy(x=>x,StringComparer.CurrentCultureIgnoreCase)),"html"=>Regex.Replace(s,"<[^>]+>",string.Empty),"json"=>JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(s),new JsonSerializerOptions{WriteIndented=true}),"urlencode"=>Uri.EscapeDataString(s),"urldecode"=>Uri.UnescapeDataString(s),"b64e"=>Convert.ToBase64String(Encoding.UTF8.GetBytes(s)),"b64d"=>Encoding.UTF8.GetString(Convert.FromBase64String(s)),_=>s};UpdateTextStats();}catch(Exception ex){TextOutputBox.Text="处理失败："+ex.Message;}}
    private void TextInput_Changed(object sender,TextChangedEventArgs e)=>UpdateTextStats();
    private void UpdateTextStats(){if(TextStats==null)return;var s=TextInputBox?.Text??"";TextStats.Text=$"字符 {s.Length} · 行 {Math.Max(1,s.Replace("\r","").Split('\n').Length)} · 非空白 {s.Count(c=>!char.IsWhiteSpace(c))}";}
    private void TextSwap_Click(object sender,RoutedEventArgs e)=>(TextInputBox.Text,TextOutputBox.Text)=(TextOutputBox.Text,TextInputBox.Text);
    private void TextCopy_Click(object sender,RoutedEventArgs e){if(!string.IsNullOrEmpty(TextOutputBox.Text))System.Windows.Clipboard.SetText(TextOutputBox.Text);}
    private void TextUndo_Click(object sender,RoutedEventArgs e){TextInputBox.Text=_textUndo;}
    private void TextImport_Click(object sender,RoutedEventArgs e){var d=new Microsoft.Win32.OpenFileDialog{Filter="文本文件|*.txt;*.md;*.json;*.xml;*.csv|所有文件|*.*"};if(d.ShowDialog(this)==true)TextInputBox.Text=System.IO.File.ReadAllText(d.FileName);}
    private void TextExport_Click(object sender,RoutedEventArgs e){var d=new Microsoft.Win32.SaveFileDialog{Filter="文本文件|*.txt",FileName="小二文本结果.txt"};if(d.ShowDialog(this)==true)System.IO.File.WriteAllText(d.FileName,TextOutputBox.Text);}
    private void NavSearch_Changed(object sender,TextChangedEventArgs e){var q=NavSearchBox.Text.Trim();foreach(var b in new[]{MouseNav,TimerNav,PomodoroNav,ClipboardNav,NotesNav,ScreenshotNav,TextNav,OrganizerNav,RenameNav,ImageNav,PdfNav}){var label=(b.Content as TextBlock)?.Text??"";b.Visibility=string.IsNullOrEmpty(q)||label.Contains(q,StringComparison.OrdinalIgnoreCase)?Visibility.Visible:Visibility.Collapsed;}PresentationExpander.IsExpanded=true;}
}