using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// Item を Shelf から削除する UseCase
/// </summary>
public class RemoveItemUseCase
{
    private readonly IItemRepository _itemRepository;

    public RemoveItemUseCase(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task<RemoveItemResult> ExecuteAsync(
        ItemId itemId,
        CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            return new RemoveItemResult.ItemNotFound(itemId);
        }

        await _itemRepository.DeleteAsync(itemId, cancellationToken);

        return new RemoveItemResult.Success();
    }
}

/// <summary>
/// RemoveItem ユースケースの実行結果
/// </summary>
public abstract record RemoveItemResult
{
    public sealed record Success : RemoveItemResult;
    public sealed record ItemNotFound(ItemId ItemId) : RemoveItemResult;
}
