using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// 欠損（存在しない参照先）アイテムを取得する UseCase
/// </summary>
public class GetMissingItemsUseCase
{
    private readonly IItemRepository _itemRepository;
    private readonly IShelfRepository _shelfRepository;
    private readonly IExistenceChecker _existenceChecker;

    public GetMissingItemsUseCase(
        IItemRepository itemRepository,
        IShelfRepository shelfRepository,
        IExistenceChecker existenceChecker)
    {
        _itemRepository = itemRepository;
        _shelfRepository = shelfRepository;
        _existenceChecker = existenceChecker;
    }

    public async Task<GetMissingItemsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // 全アイテムを取得
        var allItems = await _itemRepository.GetAllAsync(cancellationToken);

        // Shelf 名をマッピング
        var allShelves = await _shelfRepository.GetAllAsync(cancellationToken);
        var shelfMap = allShelves.ToDictionary(s => s.Id, s => s.Name);

        // 存在しないアイテムをフィルタ
        var missingItems = allItems
            .Where(item => !_existenceChecker.Exists(item.Target))
            .Select(item => new MissingItemInfo(
                item,
                shelfMap.TryGetValue(item.ShelfId, out var name) ? name : "Unknown"))
            .ToList();

        return new GetMissingItemsResult.Success(missingItems);
    }
}
