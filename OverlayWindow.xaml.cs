using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Workbench;

public partial class OverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExNoactivate = 0x08000000;
    private const int WhMouseLl = 14;
    private const uint SwpNoactivate = 0x0010;

    private readonly LowLevelMouseProc _mouseProc;
    private IntPtr _mouseHook;
    private IntPtr _handle;
    private int _size = 32;
    private int _position;
    private Point _lastCursor;

    public OverlayWindow()
    {
        InitializeComponent();
        _mouseProc = MouseHookCallback;
        SourceInitialized += OnSourceInitialized;
    }

    public void Configure(int size, int theme, int shape, int position, System.Windows.Media.Color customColor, bool dynamic)
    {
        _size = Math.Clamp(size, 24, 72);
        _position = Math.Clamp(position, 0, 2);
        ApplyTheme(theme, customColor);
        ApplyShape(shape);
        ApplyAnimation(dynamic);
        MoveTo(_lastCursor);
    }

    private void ApplyTheme(int theme, System.Windows.Media.Color customColor)
    {
        HorizontalMark.Visibility = Visibility.Collapsed;
        VerticalMark.Visibility = Visibility.Collapsed;
        CenterDot.Visibility = Visibility.Collapsed;
        Glow.Fill = System.Windows.Media.Brushes.Transparent;
        var selected = new SolidColorBrush(customColor);

        switch (theme)
        {
            case 1:
                selected = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
                Ring.Stroke = selected;
                Ring.StrokeThickness = 3;
                Glow.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(42, 255, 214, 0));
                CenterDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
                CenterDot.Visibility = Visibility.Visible;
                break;
            case 2:
                selected = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 220, 255));
                Ring.Stroke = selected;
                Ring.StrokeThickness = 3;
                Glow.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(35, 0, 220, 255));
                break;
            default:
                Ring.Stroke = selected;
                Ring.StrokeThickness = 3.2;
                break;
        }
        Square.Stroke = selected;
        HorizontalMark.Stroke = selected;
        VerticalMark.Stroke = selected;
    }

    private void ApplyShape(int shape)
    {
        Ring.Visibility = shape == 0 ? Visibility.Visible : Visibility.Collapsed;
        Square.Visibility = shape == 1 ? Visibility.Visible : Visibility.Collapsed;
        HorizontalMark.Visibility = shape == 2 ? Visibility.Visible : Visibility.Collapsed;
        VerticalMark.Visibility = shape == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyAnimation(bool enabled)
    {
        PulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        VisualRoot.BeginAnimation(OpacityProperty, null);
        PulseScale.ScaleX = PulseScale.ScaleY = 1;
        VisualRoot.Opacity = 1;
        if (!enabled) return;
        var scale = new DoubleAnimation(0.88, 1.08, TimeSpan.FromMilliseconds(650))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        var opacity = new DoubleAnimation(0.58, 1, TimeSpan.FromMilliseconds(650))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        PulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        PulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        VisualRoot.BeginAnimation(OpacityProperty, opacity);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        long style = GetWindowLongPtr(_handle, GwlExstyle).ToInt64();
        SetWindowLongPtr(_handle, GwlExstyle, new IntPtr(style | WsExTransparent | WsExToolwindow | WsExNoactivate));
        GetCursorPos(out _lastCursor);
        MoveTo(_lastCursor);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, IntPtr.Zero, 0);
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && _handle != IntPtr.Zero)
        {
            var info = Marshal.PtrToStructure<MouseHookData>(lParam);
            _lastCursor = info.Point;
            MoveTo(_lastCursor);
        }
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void MoveTo(Point cursor)
    {
        if (_handle == IntPtr.Zero) return;
        int radius = _size / 2;
        int offset = _position == 0 ? 0 : (_position == 1 ? radius : -radius);
        SetWindowPos(_handle, new IntPtr(-1), cursor.X - radius + offset, cursor.Y - radius + offset,
            _size, _size, SwpNoactivate);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        base.OnClosed(e);
    }

    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelMouseProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int index);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, index) : GetWindowLong32(hWnd, index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr newStyle);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int index, IntPtr newStyle);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr newStyle) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, index, newStyle) : SetWindowLong32(hWnd, index, newStyle);
}
