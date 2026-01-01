using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Shelves;

/// <summary>
/// Shelf を削除する UseCase（配下のShelf・Itemも削除）
/// </summary>
public class DeleteShelfUseCase
{
    private readonly IShelfRepository _shelfRepository;
    private readonly IItemRepository _itemRepository;

    public DeleteShelfUseCase(
        IShelfRepository shelfRepository,
        IItemRepository itemRepository)
    {
        _shelfRepository = shelfRepository;
        _itemRepository = itemRepository;
    }

    public async Task<DeleteShelfResult> ExecuteAsync(
        ShelfId shelfId,
        CancellationToken cancellationToken = default)
    {
        var shelf = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null)
        {
            return new DeleteShelfResult.ShelfNotFound(shelfId);
        }

        // 配下のShelfを再帰的に削除
        await DeleteRecursiveAsync(shelfId, cancellationToken);

        return new DeleteShelfResult.Success();
    }

    private async Task DeleteRecursiveAsync(ShelfId shelfId, CancellationToken cancellationToken)
    {
        // 子Shelfを取得
        var children = await _shelfRepository.GetChildrenAsync(shelfId, cancellationToken);

        // 子Shelfを再帰的に削除
        foreach (var child in children)
        {
            await DeleteRecursiveAsync(child.Id, cancellationToken);
        }

        // このShelfのItemを削除
        var items = await _itemRepository.GetByShelfIdAsync(shelfId, cancellationToken);
        foreach (var item in items)
        {
            await _itemRepository.DeleteAsync(item.Id, cancellationToken);
        }

        // Shelf自体を削除
        await _shelfRepository.DeleteAsync(shelfId, cancellationToken);
    }
}

/// <summary>
/// DeleteShelf ユースケースの実行結果
/// </summary>
public abstract record DeleteShelfResult
{
    public sealed record Success : DeleteShelfResult;
    public sealed record ShelfNotFound(ShelfId ShelfId) : DeleteShelfResult;
}
