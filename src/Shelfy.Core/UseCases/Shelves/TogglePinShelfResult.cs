using Shelfy.Core.Domain.Entities;

namespace Shelfy.Core.UseCases.Shelves;

/// <summary>
/// ピン留め切り替え結果
/// </summary>
public abstract record TogglePinShelfResult
{
    private TogglePinShelfResult() { }

    public sealed record Success(Shelf Shelf) : TogglePinShelfResult;
    public sealed record NotFound(ShelfId ShelfId) : TogglePinShelfResult;
    public sealed record Error(string Message) : TogglePinShelfResult;
}
