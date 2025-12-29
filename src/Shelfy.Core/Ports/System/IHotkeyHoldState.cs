namespace Shelfy.Core.Ports.System;

/// <summary>
/// ホットキーの押下状態を確認するインターフェース
/// </summary>
public interface IHotkeyHoldState
{
    /// <summary>
    /// ホットキーが現在押下されているかどうか
    /// </summary>
    bool IsHotkeyHeld { get; }
}
