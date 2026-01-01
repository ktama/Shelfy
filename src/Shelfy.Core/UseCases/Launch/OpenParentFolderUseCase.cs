using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;

namespace Shelfy.Core.UseCases.Launch;

/// <summary>
/// Item の親フォルダを開く UseCase
/// </summary>
public class OpenParentFolderUseCase
{
    private readonly IItemRepository _itemRepository;
    private readonly IItemLauncher _itemLauncher;

    public OpenParentFolderUseCase(
        IItemRepository itemRepository,
        IItemLauncher itemLauncher)
    {
        _itemRepository = itemRepository;
        _itemLauncher = itemLauncher;
    }

    public async Task<OpenParentFolderResult> ExecuteAsync(
        ItemId itemId,
        CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            return new OpenParentFolderResult.ItemNotFound(itemId);
        }

        // URL タイプは親フォルダを開けない
        if (item.Type == ItemType.Url)
        {
            return new OpenParentFolderResult.NotSupported("Cannot open parent folder for URL items.");
        }

        var success = await _itemLauncher.OpenParentFolderAsync(item);
        if (!success)
        {
            return new OpenParentFolderResult.LaunchFailed("Failed to open parent folder.");
        }

        return new OpenParentFolderResult.Success();
    }
}

/// <summary>
/// OpenParentFolder ユースケースの実行結果
/// </summary>
public abstract record OpenParentFolderResult
{
    public sealed record Success : OpenParentFolderResult;
    public sealed record ItemNotFound(ItemId ItemId) : OpenParentFolderResult;
    public sealed record NotSupported(string Message) : OpenParentFolderResult;
    public sealed record LaunchFailed(string Message) : OpenParentFolderResult;
}
