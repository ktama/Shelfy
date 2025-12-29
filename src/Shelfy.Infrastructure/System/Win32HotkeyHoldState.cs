using Shelfy.Core.Ports.System;

namespace Shelfy.Infrastructure.System;

/// <summary>
/// ホットキー押下状態の実装（プレースホルダー）
/// </summary>
public class Win32HotkeyHoldState : IHotkeyHoldState
{
    // TODO: 実際のWin32 API実装
    public bool IsHotkeyHeld => false;
}
