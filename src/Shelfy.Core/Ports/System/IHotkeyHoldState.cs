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

    /// <summary>
    /// ホットキー文字列から修飾キーを設定する（例: "Ctrl+Shift+Space"）
    /// </summary>
    void ConfigureFromHotkeyString(string hotkeyString);
}
