using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Search;

/// <summary>
/// アイテム検索 UseCase
/// </summary>
public class SearchItemsUseCase
{
    private readonly IItemRepository _itemRepository;
    private readonly IShelfRepository _shelfRepository;

    public SearchItemsUseCase(
        IItemRepository itemRepository,
        IShelfRepository shelfRepository)
    {
        _itemRepository = itemRepository;
        _shelfRepository = shelfRepository;
    }

    public async Task<SearchResult> ExecuteAsync(
        string queryText,
        CancellationToken cancellationToken = default)
    {
        var query = SearchQuery.Parse(queryText);

        if (string.IsNullOrWhiteSpace(query.FreeText) &&
            string.IsNullOrWhiteSpace(query.ShelfFilter) &&
            string.IsNullOrWhiteSpace(query.TypeFilter) &&
            string.IsNullOrWhiteSpace(query.InShelfFilter))
        {
            return new SearchResult.Success([]);
        }

        // 全 Shelf を取得（名前マッピング用）
        var allShelves = await _shelfRepository.GetAllAsync(cancellationToken);
        var shelfMap = allShelves.ToDictionary(s => s.Id, s => s);

        // ItemRepository で基本検索
        var searchText = query.FreeText;
        var items = new List<Item>(await _itemRepository.SearchAsync(searchText, cancellationToken));
        var itemSet = new HashSet<ItemId>(items.Select(i => i.Id));

        // フリーテキストで Shelf 名にもマッチするアイテムを追加
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var matchingShelfIds = allShelves
                .Where(s => s.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Id)
                .ToList();

            foreach (var shelfId in matchingShelfIds)
            {
                var shelfItems = await _itemRepository.GetByShelfIdAsync(shelfId, cancellationToken);
                foreach (var item in shelfItems)
                {
                    if (itemSet.Add(item.Id))
                    {
                        items.Add(item);
                    }
                }
            }
        }

        // フィルタリング
        var filtered = items.AsEnumerable();

        // type: フィルタ
        if (!string.IsNullOrWhiteSpace(query.TypeFilter))
        {
            var typeFilter = query.TypeFilter.ToLowerInvariant();
            filtered = filtered.Where(i =>
                typeFilter switch
                {
                    "file" => i.Type == ItemType.File,
                    "folder" => i.Type == ItemType.Folder,
                    "url" => i.Type == ItemType.Url,
                    _ => true
                });
        }

        // box: フィルタ（Shelf 名で検索）
        if (!string.IsNullOrWhiteSpace(query.ShelfFilter))
        {
            filtered = filtered.Where(i =>
                shelfMap.TryGetValue(i.ShelfId, out var shelf) &&
                shelf.Name.Contains(query.ShelfFilter, StringComparison.OrdinalIgnoreCase));
        }

        // in: フィルタ（特定 Shelf 内）
        if (!string.IsNullOrWhiteSpace(query.InShelfFilter))
        {
            var matchingShelves = allShelves
                .Where(s => s.Name.Equals(query.InShelfFilter, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Id)
                .ToHashSet();

            filtered = filtered.Where(i => matchingShelves.Contains(i.ShelfId));
        }

        // 検索結果を作成
        var resultItems = filtered
            .Select(i => new SearchResultItem(
                i,
                shelfMap.TryGetValue(i.ShelfId, out var shelf) ? shelf.Name : "Unknown"))
            .ToList();

        return new SearchResult.Success(resultItems);
    }
}
