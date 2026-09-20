using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace Workbench;

public sealed class KeyboardDisplayService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmSyskeydown = 0x0104;
    private readonly KeyboardProc _callback;
    private readonly KeyDisplayWindow _window = new();
    private IntPtr _hook;

    public KeyboardDisplayService() => _callback = HookCallback;

    public void Start()
    {
        if (_hook == IntPtr.Zero)
            _hook = SetWindowsHookEx(WhKeyboardLl, _callback, IntPtr.Zero, 0);
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam.ToInt32() == WmKeydown || wParam.ToInt32() == WmSyskeydown))
        {
            int virtualKey = Marshal.ReadInt32(lParam);
            string text = FormatKey(virtualKey);
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => _window.ShowKey(text));
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static string FormatKey(int virtualKey)
    {
        var parts = new List<string>();
        if (IsDown(0x11) && virtualKey != 0x11) parts.Add("Ctrl");
        if (IsDown(0x12) && virtualKey != 0x12) parts.Add("Alt");
        if (IsDown(0x10) && virtualKey != 0x10) parts.Add("Shift");
        if ((IsDown(0x5B) || IsDown(0x5C)) && virtualKey is not (0x5B or 0x5C)) parts.Add("Win");
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        string name = key switch
        {
            Key.LeftCtrl or Key.RightCtrl => "Ctrl",
            Key.LeftShift or Key.RightShift => "Shift",
            Key.LeftAlt or Key.RightAlt => "Alt",
            Key.LWin or Key.RWin => "Win",
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Space => "Space",
            _ => key.ToString()
        };
        parts.Add(name);
        return string.Join(" + ", parts.Distinct());
    }

    private static bool IsDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _window.Close();
    }

    private delegate IntPtr KeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, KeyboardProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
