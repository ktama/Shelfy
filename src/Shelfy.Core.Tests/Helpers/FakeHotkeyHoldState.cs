using Shelfy.Core.Ports.System;

namespace Shelfy.Core.Tests.Helpers;

/// <summary>
/// テスト用の IHotkeyHoldState 実装
/// </summary>
public class FakeHotkeyHoldState : IHotkeyHoldState
{
    public bool IsHotkeyHeld { get; set; } = false;
}
