namespace Shelfy.Core.Domain.Entities;

/// <summary>
/// Shelf の識別子
/// </summary>
public readonly record struct ShelfId(Guid Value)
{
    public static ShelfId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
