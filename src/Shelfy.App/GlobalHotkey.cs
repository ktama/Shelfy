using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Shelfy.App;

/// <summary>
/// グローバルホットキーを管理するクラス
/// </summary>
public partial class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT = 0x0004;

    // スペースキーの仮想キーコード
    private const int VK_SPACE = 0x20;

    private readonly Window _window;
    private readonly int _hotkeyId;
    private HwndSource? _source;
    private bool _isRegistered;

    public event Action? HotkeyPressed;

    public GlobalHotkey(Window window, int hotkeyId = 9000)
    {
        _window = window;
        _hotkeyId = hotkeyId;
    }

    public bool Register()
    {
        var helper = new WindowInteropHelper(_window);
        var hwnd = helper.Handle;

        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(HwndHook);

        // Ctrl+Shift+Space をホットキーとして登録
        _isRegistered = RegisterHotKey(hwnd, _hotkeyId, MOD_CONTROL | MOD_SHIFT, VK_SPACE);
        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered && _source is not null && _source.Handle != IntPtr.Zero)
        {
            UnregisterHotKey(_source.Handle, _hotkeyId);
            _isRegistered = false;
        }

        _source?.RemoveHook(HwndHook);
        _source = null;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
