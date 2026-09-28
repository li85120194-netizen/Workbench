using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Workbench;
public sealed class PinImageWindow : Window
{
    private readonly Bitmap _bitmap;
    public PinImageWindow(Bitmap bitmap)
    {
        _bitmap=(Bitmap)bitmap.Clone(); WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=System.Windows.Media.Brushes.Transparent; Topmost=true; ShowInTaskbar=false; ResizeMode=ResizeMode.CanResizeWithGrip; SizeToContent=SizeToContent.WidthAndHeight;
        var image=new System.Windows.Controls.Image{Source=ScreenshotSelectionWindow.ToSource(_bitmap),MaxWidth=900,MaxHeight=700,Stretch=Stretch.Uniform}; Content=new System.Windows.Controls.Border{BorderBrush=System.Windows.Media.Brushes.DeepSkyBlue,BorderThickness=new Thickness(1),Background=System.Windows.Media.Brushes.White,Child=image};
        MouseLeftButtonDown += (_,e)=>{if(e.ClickCount==2){Width=_bitmap.Width;Height=_bitmap.Height;}else DragMove();}; MouseWheel += (_,e)=>{var f=e.Delta>0?1.1:.9;Width=Math.Max(120,ActualWidth*f);Height=Math.Max(80,ActualHeight*f);};
        var menu=new System.Windows.Controls.ContextMenu(); var copy=new System.Windows.Controls.MenuItem{Header="复制"};copy.Click+=(_,_)=>System.Windows.Clipboard.SetImage(ScreenshotSelectionWindow.ToSource(_bitmap)); var save=new System.Windows.Controls.MenuItem{Header="另存为"};save.Click+=(_,_)=>Save();var close=new System.Windows.Controls.MenuItem{Header="关闭"};close.Click+=(_,_)=>Close();menu.Items.Add(copy);menu.Items.Add(save);menu.Items.Add(close);ContextMenu=menu;
    }
    private void Save(){var d=new Microsoft.Win32.SaveFileDialog{Filter="PNG 图片|*.png",FileName=$"截图_{DateTime.Now:yyyyMMdd_HHmmss}.png"};if(d.ShowDialog(this)==true)_bitmap.Save(d.FileName,System.Drawing.Imaging.ImageFormat.Png);}
    protected override void OnClosed(EventArgs e){base.OnClosed(e);_bitmap.Dispose();}
}