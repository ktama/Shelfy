using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// Item を Shelf に追加する UseCase
/// </summary>
public class AddItemUseCase
{
    private readonly IItemRepository _itemRepository;
    private readonly IShelfRepository _shelfRepository;
    private readonly IClock _clock;

    public AddItemUseCase(
        IItemRepository itemRepository,
        IShelfRepository shelfRepository,
        IClock clock)
    {
        _itemRepository = itemRepository;
        _shelfRepository = shelfRepository;
        _clock = clock;
    }

    public async Task<AddItemResult> ExecuteAsync(
        ShelfId shelfId,
        ItemType type,
        string target,
        string displayName,
        string? memo = null,
        CancellationToken cancellationToken = default)
    {
        // バリデーション
        if (string.IsNullOrWhiteSpace(target))
        {
            return new AddItemResult.ValidationError("Target cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return new AddItemResult.ValidationError("Display name cannot be empty.");
        }

        // Shelfの存在確認
        var shelf = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null)
        {
            return new AddItemResult.ShelfNotFound(shelfId);
        }

        // 重複チェック（同一Shelf内で同一参照は不可、ファイルパスは大文字小文字無視）
        var existingItems = await _itemRepository.GetByShelfIdAsync(shelfId, cancellationToken);
        if (existingItems.Any(i => i.Type == type && string.Equals(i.Target, target,
            type == ItemType.Url ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)))
        {
            return new AddItemResult.DuplicateItem($"An item with the same type and target already exists in this shelf.");
        }

        // 新しいアイテムの SortOrder を既存の最大値 + 1 に設定
        var maxSortOrder = existingItems.Count > 0 ? existingItems.Max(i => i.SortOrder) + 1 : 0;

        var item = new Item(
            id: ItemId.New(),
            shelfId: shelfId,
            type: type,
            target: target,
            displayName: displayName,
            createdAt: _clock.UtcNow,
            memo: memo,
            sortOrder: maxSortOrder
        );

        await _itemRepository.AddAsync(item, cancellationToken);

        return new AddItemResult.Success(item);
    }
}

/// <summary>
/// AddItem ユースケースの実行結果
/// </summary>
public abstract record AddItemResult
{
    public sealed record Success(Item Item) : AddItemResult;
    public sealed record ValidationError(string Message) : AddItemResult;
    public sealed record ShelfNotFound(ShelfId ShelfId) : AddItemResult;
    public sealed record DuplicateItem(string Message) : AddItemResult;
}
