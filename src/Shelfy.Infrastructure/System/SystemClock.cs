using Shelfy.Core.Ports.System;

namespace Shelfy.Infrastructure.System;

/// <summary>
/// システム時刻の実装
/// </summary>
public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
