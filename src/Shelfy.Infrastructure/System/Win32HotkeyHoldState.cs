using System.Runtime.InteropServices;
using Shelfy.Core.Ports.System;

namespace Shelfy.Infrastructure.System;

/// <summary>
/// ホットキー押下状態の Win32 実装
/// </summary>
public partial class Win32HotkeyHoldState : IHotkeyHoldState
{
    // 仮想キーコード
    private const int VK_LWIN = 0x5B;  // Left Windows key
    private const int VK_RWIN = 0x5C;  // Right Windows key
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_MENU = 0x12;  // Alt key

    // GetAsyncKeyState の戻り値で押下中を示すビット
    private const short KEY_PRESSED = unchecked((short)0x8000);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);

    /// <summary>
    /// 登録されているホットキーの修飾キー
    /// デフォルトは Ctrl+Shift（GlobalHotkey のデフォルトと一致）
    /// </summary>
    public HotkeyModifierKeys HotkeyModifiers { get; set; } = HotkeyModifierKeys.Control | HotkeyModifierKeys.Shift;

    /// <summary>
    /// ホットキー文字列からの修飾キー設定
    /// </summary>
    public void ConfigureFromHotkeyString(string hotkeyString)
    {
        var modifiers = HotkeyModifierKeys.None;
        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= HotkeyModifierKeys.Control; break;
                case "shift": modifiers |= HotkeyModifierKeys.Shift; break;
                case "alt": modifiers |= HotkeyModifierKeys.Alt; break;
                case "win": case "windows": modifiers |= HotkeyModifierKeys.Win; break;
            }
        }
        if (modifiers != HotkeyModifierKeys.None)
            HotkeyModifiers = modifiers;
    }

    /// <summary>
    /// ホットキーが現在押下されているかどうか
    /// </summary>
    public bool IsHotkeyHeld => CheckModifiersHeld();

    private bool CheckModifiersHeld()
    {
        if (HotkeyModifiers == HotkeyModifierKeys.None)
            return false;

        // 各修飾キーの押下状態をチェック
        if (HotkeyModifiers.HasFlag(HotkeyModifierKeys.Win))
        {
            if (!IsKeyPressed(VK_LWIN) && !IsKeyPressed(VK_RWIN))
                return false;
        }

        if (HotkeyModifiers.HasFlag(HotkeyModifierKeys.Control))
        {
            if (!IsKeyPressed(VK_CONTROL))
                return false;
        }

        if (HotkeyModifiers.HasFlag(HotkeyModifierKeys.Shift))
        {
            if (!IsKeyPressed(VK_SHIFT))
                return false;
        }

        if (HotkeyModifiers.HasFlag(HotkeyModifierKeys.Alt))
        {
            if (!IsKeyPressed(VK_MENU))
                return false;
        }

        return true;
    }

    private static bool IsKeyPressed(int virtualKeyCode)
    {
        return (GetAsyncKeyState(virtualKeyCode) & KEY_PRESSED) != 0;
    }
}

/// <summary>
/// 修飾キーのフラグ
/// </summary>
[Flags]
public enum HotkeyModifierKeys
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
    Win = 8
}
