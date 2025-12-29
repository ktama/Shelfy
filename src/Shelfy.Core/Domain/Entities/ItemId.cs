namespace Shelfy.Core.Domain.Entities;

/// <summary>
/// Item の識別子
/// </summary>
public readonly record struct ItemId(Guid Value)
{
    public static ItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
