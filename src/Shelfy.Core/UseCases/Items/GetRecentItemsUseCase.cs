using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// 最近使ったアイテムを取得する UseCase
/// </summary>
public class GetRecentItemsUseCase
{
    private readonly IItemRepository _itemRepository;
    private readonly IShelfRepository _shelfRepository;
    private const int DefaultCount = 20;

    public GetRecentItemsUseCase(
        IItemRepository itemRepository,
        IShelfRepository shelfRepository)
    {
        _itemRepository = itemRepository;
        _shelfRepository = shelfRepository;
    }

    public async Task<GetRecentItemsResult> ExecuteAsync(
        int? count = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var takeCount = count ?? DefaultCount;

            // 最近アクセスしたアイテムを取得
            var recentItems = await _itemRepository.GetRecentAsync(takeCount, cancellationToken);

            // Shelf 名をマッピング
            var allShelves = await _shelfRepository.GetAllAsync(cancellationToken);
            var shelfMap = allShelves.ToDictionary(s => s.Id, s => s.Name);

            var resultItems = recentItems
                .Select(item => new RecentItemInfo(
                    item,
                    shelfMap.TryGetValue(item.ShelfId, out var name) ? name : "Unknown"))
                .ToList();

            return new GetRecentItemsResult.Success(resultItems);
        }
        catch (Exception ex)
        {
            return new GetRecentItemsResult.Error(ex.Message);
        }
    }
}
