using Shelfy.Core.Ports.System;

namespace Shelfy.Core.Tests.Helpers;

/// <summary>
/// テスト用の IHotkeyHoldState 実装
/// </summary>
public class FakeHotkeyHoldState : IHotkeyHoldState
{
    public bool IsHotkeyHeld { get; set; } = false;
    public string? LastConfiguredHotkeyString { get; private set; }
    public void ConfigureFromHotkeyString(string hotkeyString) => LastConfiguredHotkeyString = hotkeyString;
}
