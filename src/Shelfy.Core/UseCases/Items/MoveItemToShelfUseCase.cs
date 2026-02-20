using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// Item を別の Shelf に移動する UseCase
/// </summary>
public class MoveItemToShelfUseCase
{
    private readonly IItemRepository _itemRepository;
    private readonly IShelfRepository _shelfRepository;

    public MoveItemToShelfUseCase(
        IItemRepository itemRepository,
        IShelfRepository shelfRepository)
    {
        _itemRepository = itemRepository;
        _shelfRepository = shelfRepository;
    }

    public async Task<MoveItemToShelfResult> ExecuteAsync(
        ItemId itemId,
        ShelfId targetShelfId,
        CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            return new MoveItemToShelfResult.ItemNotFound(itemId);
        }

        var targetShelf = await _shelfRepository.GetByIdAsync(targetShelfId, cancellationToken);
        if (targetShelf is null)
        {
            return new MoveItemToShelfResult.ShelfNotFound(targetShelfId);
        }

        // 同一 Shelf 内で同一参照（type + target）は重複不可（ファイルパスは大文字小文字無視）
        var existingItems = await _itemRepository.GetByShelfIdAsync(targetShelfId, cancellationToken);
        var duplicate = existingItems.FirstOrDefault(i =>
            i.Id != itemId &&
            i.Type == item.Type &&
            string.Equals(i.Target, item.Target,
                item.Type == ItemType.Url ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));

        if (duplicate is not null)
        {
            return new MoveItemToShelfResult.DuplicateItem(
                $"An item with the same reference already exists in the target shelf.");
        }

        // 移動先の既存アイテム最大 SortOrder + 1 を設定
        var maxSortOrder = existingItems.Count > 0 ? existingItems.Max(i => i.SortOrder) + 1 : 0;
        item.SetSortOrder(maxSortOrder);
        item.MoveToShelf(targetShelfId);
        await _itemRepository.UpdateAsync(item, cancellationToken);

        return new MoveItemToShelfResult.Success(item);
    }
}

/// <summary>
/// MoveItemToShelf ユースケースの実行結果
/// </summary>
public abstract record MoveItemToShelfResult
{
    public sealed record Success(Item Item) : MoveItemToShelfResult;
    public sealed record ItemNotFound(ItemId ItemId) : MoveItemToShelfResult;
    public sealed record ShelfNotFound(ShelfId ShelfId) : MoveItemToShelfResult;
    public sealed record DuplicateItem(string Message) : MoveItemToShelfResult;
}
