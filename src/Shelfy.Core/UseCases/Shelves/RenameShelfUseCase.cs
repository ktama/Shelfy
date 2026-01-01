using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Shelves;

/// <summary>
/// Shelf 名を変更する UseCase
/// </summary>
public class RenameShelfUseCase
{
    private readonly IShelfRepository _shelfRepository;

    public RenameShelfUseCase(IShelfRepository shelfRepository)
    {
        _shelfRepository = shelfRepository;
    }

    public async Task<RenameShelfResult> ExecuteAsync(
        ShelfId shelfId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return new RenameShelfResult.ValidationError("Shelf name cannot be empty.");
        }

        var shelf = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null)
        {
            return new RenameShelfResult.ShelfNotFound(shelfId);
        }

        shelf.Rename(newName);
        await _shelfRepository.UpdateAsync(shelf, cancellationToken);

        return new RenameShelfResult.Success(shelf);
    }
}

/// <summary>
/// RenameShelf ユースケースの実行結果
/// </summary>
public abstract record RenameShelfResult
{
    public sealed record Success(Shelf Shelf) : RenameShelfResult;
    public sealed record ValidationError(string Message) : RenameShelfResult;
    public sealed record ShelfNotFound(ShelfId ShelfId) : RenameShelfResult;
}
