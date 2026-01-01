using Shelfy.Core.Ports.System;

namespace Shelfy.Core.Tests.Helpers;

/// <summary>
/// テスト用の固定時刻 IClock 実装
/// </summary>
public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
