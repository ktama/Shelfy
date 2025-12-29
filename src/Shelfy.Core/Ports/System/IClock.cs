namespace Shelfy.Core.Ports.System;

/// <summary>
/// 時刻取得のインターフェース
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
