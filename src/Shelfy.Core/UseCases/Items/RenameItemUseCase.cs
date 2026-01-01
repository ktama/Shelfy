using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// Item の表示名を変更する UseCase
/// </summary>
public class RenameItemUseCase
{
    private readonly IItemRepository _itemRepository;

    public RenameItemUseCase(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task<RenameItemResult> ExecuteAsync(
        ItemId itemId,
        string newDisplayName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
        {
            return new RenameItemResult.ValidationError("Display name cannot be empty.");
        }

        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            return new RenameItemResult.ItemNotFound(itemId);
        }

        item.Rename(newDisplayName);
        await _itemRepository.UpdateAsync(item, cancellationToken);

        return new RenameItemResult.Success(item);
    }
}

/// <summary>
/// RenameItem ユースケースの実行結果
/// </summary>
public abstract record RenameItemResult
{
    public sealed record Success(Item Item) : RenameItemResult;
    public sealed record ValidationError(string Message) : RenameItemResult;
    public sealed record ItemNotFound(ItemId ItemId) : RenameItemResult;
}
