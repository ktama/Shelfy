using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// Item の並び順を変更する UseCase
/// </summary>
public class ReorderItemsUseCase
{
    private readonly IItemRepository _itemRepository;

    public ReorderItemsUseCase(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// 指定された順序で Item の SortOrder を更新する
    /// </summary>
    /// <param name="orderedItemIds">新しい順序での ItemId リスト</param>
    public async Task<ReorderItemsResult> ExecuteAsync(
        IReadOnlyList<ItemId> orderedItemIds,
        CancellationToken cancellationToken = default)
    {
        if (orderedItemIds.Count == 0)
        {
            return new ReorderItemsResult.Success();
        }

        var updatedItems = new List<Item>();

        for (var i = 0; i < orderedItemIds.Count; i++)
        {
            var itemId = orderedItemIds[i];
            var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);

            if (item is null)
            {
                return new ReorderItemsResult.ItemNotFound(itemId);
            }

            item.SetSortOrder(i);
            await _itemRepository.UpdateAsync(item, cancellationToken);
            updatedItems.Add(item);
        }

        return new ReorderItemsResult.Success(updatedItems);
    }
}

/// <summary>
/// ReorderItems ユースケースの実行結果
/// </summary>
public abstract record ReorderItemsResult
{
    public sealed record Success(IReadOnlyList<Item>? UpdatedItems = null) : ReorderItemsResult;
    public sealed record ItemNotFound(ItemId ItemId) : ReorderItemsResult;
}
