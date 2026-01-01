using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// Item のメモを更新する UseCase
/// </summary>
public class UpdateItemMemoUseCase
{
    private readonly IItemRepository _itemRepository;

    public UpdateItemMemoUseCase(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task<UpdateItemMemoResult> ExecuteAsync(
        ItemId itemId,
        string? memo,
        CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            return new UpdateItemMemoResult.ItemNotFound(itemId);
        }

        item.UpdateMemo(memo);
        await _itemRepository.UpdateAsync(item, cancellationToken);

        return new UpdateItemMemoResult.Success(item);
    }
}

/// <summary>
/// UpdateItemMemo ユースケースの実行結果
/// </summary>
public abstract record UpdateItemMemoResult
{
    public sealed record Success(Item Item) : UpdateItemMemoResult;
    public sealed record ItemNotFound(ItemId ItemId) : UpdateItemMemoResult;
}
