using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Shelfy.App;

/// <summary>
/// グローバルホットキーを管理するクラス
/// </summary>
public partial class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    // 修飾キーフラグ
    private const int MOD_ALT = 0x0001;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT = 0x0004;

    private readonly Window _window;
    private readonly int _hotkeyId;
    private readonly int _modifiers;
    private readonly int _virtualKey;
    private HwndSource? _source;
    private bool _isRegistered;

    public event Action? HotkeyPressed;

    /// <summary>
    /// 文字列指定のホットキー（例: "Ctrl+Shift+Space"）
    /// </summary>
    public GlobalHotkey(Window window, string hotkeyString, int hotkeyId = 9000)
    {
        _window = window;
        _hotkeyId = hotkeyId;
        (_modifiers, _virtualKey) = ParseHotkeyString(hotkeyString);
    }

    /// <summary>
    /// デフォルト（Ctrl+Shift+Space）
    /// </summary>
    public GlobalHotkey(Window window, int hotkeyId = 9000)
        : this(window, "Ctrl+Shift+Space", hotkeyId)
    {
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

        _isRegistered = RegisterHotKey(hwnd, _hotkeyId, _modifiers, _virtualKey);
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

    /// <summary>
    /// ホットキー文字列（例: "Ctrl+Shift+Space"）を修飾キーフラグと仮想キーコードに解析する
    /// </summary>
    public static (int Modifiers, int VirtualKey) ParseHotkeyString(string hotkeyString)
    {
        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        int modifiers = 0;
        int virtualKey = 0;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                default:
                    // Key 名を仮想キーコードに変換
                    virtualKey = KeyToVirtualKey(part);
                    break;
            }
        }

        // デフォルト: Ctrl+Shift+Space
        if (modifiers == 0 && virtualKey == 0)
        {
            modifiers = MOD_CONTROL | MOD_SHIFT;
            virtualKey = 0x20; // VK_SPACE
        }

        return (modifiers, virtualKey);
    }

    private static int KeyToVirtualKey(string keyName)
    {
        // WPF Key enum でパース可能な場合
        if (Enum.TryParse<Key>(keyName, ignoreCase: true, out var key))
        {
            return KeyInterop.VirtualKeyFromKey(key);
        }

        // よく使われるキー名の直接マッピング
        return keyName.ToLowerInvariant() switch
        {
            "space" => 0x20,
            "enter" => 0x0D,
            "tab" => 0x09,
            "escape" or "esc" => 0x1B,
            _ => 0
        };
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
