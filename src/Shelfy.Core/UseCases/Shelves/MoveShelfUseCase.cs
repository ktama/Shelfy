using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Shelves;

/// <summary>
/// Shelf を別の親 Shelf に移動する（階層移動）UseCase
/// </summary>
public class MoveShelfUseCase
{
    private readonly IShelfRepository _shelfRepository;

    public MoveShelfUseCase(IShelfRepository shelfRepository)
    {
        _shelfRepository = shelfRepository;
    }

    public async Task<MoveShelfResult> ExecuteAsync(
        ShelfId shelfId,
        ShelfId? newParentId,
        CancellationToken cancellationToken = default)
    {
        var shelf = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null)
        {
            return new MoveShelfResult.ShelfNotFound(shelfId);
        }

        // 自分自身を親にすることはできない
        if (newParentId is not null && newParentId == shelfId)
        {
            return new MoveShelfResult.InvalidMove("Cannot move a shelf into itself.");
        }

        // 新しい親が存在するか確認（nullの場合はルートへ移動）
        if (newParentId is not null)
        {
            var newParent = await _shelfRepository.GetByIdAsync(newParentId.Value, cancellationToken);
            if (newParent is null)
            {
                return new MoveShelfResult.ParentNotFound(newParentId.Value);
            }

            // 循環参照チェック：新しい親が自分の子孫でないか確認
            if (await IsDescendantAsync(newParentId.Value, shelfId, cancellationToken))
            {
                return new MoveShelfResult.InvalidMove("Cannot move a shelf into its own descendant.");
            }
        }

        shelf.MoveTo(newParentId);
        await _shelfRepository.UpdateAsync(shelf, cancellationToken);

        return new MoveShelfResult.Success(shelf);
    }

    /// <summary>
    /// targetId が potentialAncestorId の子孫かどうかを確認する
    /// </summary>
    private async Task<bool> IsDescendantAsync(
        ShelfId targetId,
        ShelfId potentialAncestorId,
        CancellationToken cancellationToken)
    {
        var current = await _shelfRepository.GetByIdAsync(targetId, cancellationToken);
        while (current?.ParentId is not null)
        {
            if (current.ParentId == potentialAncestorId)
            {
                return true;
            }
            current = await _shelfRepository.GetByIdAsync(current.ParentId.Value, cancellationToken);
        }
        return false;
    }
}

/// <summary>
/// MoveShelf ユースケースの実行結果
/// </summary>
public abstract record MoveShelfResult
{
    public sealed record Success(Shelf Shelf) : MoveShelfResult;
    public sealed record ShelfNotFound(ShelfId ShelfId) : MoveShelfResult;
    public sealed record ParentNotFound(ShelfId ParentId) : MoveShelfResult;
    public sealed record InvalidMove(string Message) : MoveShelfResult;
}
