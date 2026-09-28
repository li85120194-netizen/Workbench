using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Workbench;

public sealed class ScreenshotSelectionWindow : Window
{
    private readonly Bitmap _desktop;
    private readonly Canvas _canvas = new();
    private readonly System.Windows.Shapes.Rectangle _selection = new() { Stroke = System.Windows.Media.Brushes.DeepSkyBlue, StrokeThickness = 2, Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(38, 40, 130, 240)), Visibility = Visibility.Collapsed };
    private readonly Border _info = new() { Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 15, 24, 38)), CornerRadius = new CornerRadius(5), Padding = new Thickness(8,4,8,4), Child = new TextBlock { Foreground = System.Windows.Media.Brushes.White } };
    private System.Windows.Point _start;
    public Bitmap? Result { get; private set; }

    public ScreenshotSelectionWindow()
    {
        var area = System.Windows.Forms.SystemInformation.VirtualScreen;
        _desktop = new Bitmap(area.Width, area.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(_desktop)) g.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size, CopyPixelOperation.SourceCopy);
        Left=area.Left; Top=area.Top; Width=area.Width; Height=area.Height; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=System.Windows.Media.Brushes.Black; Topmost=true; ShowInTaskbar=false;
        var image = new System.Windows.Controls.Image { Source=ToSource(_desktop), Stretch=Stretch.Fill, Opacity=.82 };
        _canvas.Children.Add(image); _canvas.Children.Add(_selection); _canvas.Children.Add(_info); Content=_canvas;
        Loaded += (_,_) => { image.Width=ActualWidth; image.Height=ActualHeight; _info.Visibility=Visibility.Collapsed; Cursor=System.Windows.Input.Cursors.Cross; };
        MouseLeftButtonDown += Begin; MouseMove += Move; MouseLeftButtonUp += End; KeyDown += (_,e)=>{if(e.Key==Key.Escape){DialogResult=false;Close();}};
    }
    private void Begin(object s, MouseButtonEventArgs e){_start=e.GetPosition(_canvas); _selection.Visibility=Visibility.Visible; _info.Visibility=Visibility.Visible; CaptureMouse(); Update(_start);}
    private void Move(object s, System.Windows.Input.MouseEventArgs e){if(e.LeftButton==MouseButtonState.Pressed)Update(e.GetPosition(_canvas));}
    private void Update(System.Windows.Point p){var x=Math.Min(_start.X,p.X);var y=Math.Min(_start.Y,p.Y);var w=Math.Abs(p.X-_start.X);var h=Math.Abs(p.Y-_start.Y);Canvas.SetLeft(_selection,x);Canvas.SetTop(_selection,y);_selection.Width=w;_selection.Height=h;Canvas.SetLeft(_info,x);Canvas.SetTop(_info,Math.Max(0,y-34));((TextBlock)_info.Child).Text=$"{(int)w} × {(int)h}   X:{(int)p.X} Y:{(int)p.Y}";}
    private void End(object s, MouseButtonEventArgs e){ReleaseMouseCapture();var p=e.GetPosition(_canvas);var x=(int)Math.Min(_start.X,p.X);var y=(int)Math.Min(_start.Y,p.Y);var w=(int)Math.Abs(p.X-_start.X);var h=(int)Math.Abs(p.Y-_start.Y);if(w<4||h<4)return;Result=_desktop.Clone(new System.Drawing.Rectangle(x,y,Math.Min(w,_desktop.Width-x),Math.Min(h,_desktop.Height-y)),_desktop.PixelFormat);DialogResult=true;Close();}
    protected override void OnClosed(EventArgs e){base.OnClosed(e);_desktop.Dispose();}
    public static BitmapSource ToSource(Bitmap bitmap){var h=bitmap.GetHbitmap();try{return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(h,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());}finally{DeleteObject(h);}}
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
}