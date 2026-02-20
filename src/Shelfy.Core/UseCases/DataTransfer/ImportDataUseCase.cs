using System.Globalization;
using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.DataTransfer;

/// <summary>
/// データをインポートする UseCase
/// </summary>
public class ImportDataUseCase
{
    private readonly IShelfRepository _shelfRepository;
    private readonly IItemRepository _itemRepository;

    public ImportDataUseCase(
        IShelfRepository shelfRepository,
        IItemRepository itemRepository)
    {
        _shelfRepository = shelfRepository;
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// データをインポートする（マージモード：既存データに追加）
    /// </summary>
    public async Task<ImportDataResult> ExecuteAsync(
        ExportData data,
        bool replaceAll = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (replaceAll)
            {
                // 全削除してからインポート（InMemory等CASCADE非対応のリポジトリにも対応）
                var existingItems = await _itemRepository.GetAllAsync(cancellationToken);
                foreach (var item in existingItems)
                {
                    await _itemRepository.DeleteAsync(item.Id, cancellationToken);
                }

                var existingShelves = await _shelfRepository.GetAllAsync(cancellationToken);
                foreach (var shelf in existingShelves)
                {
                    await _shelfRepository.DeleteAsync(shelf.Id, cancellationToken);
                }
            }

            var shelvesImported = 0;
            var itemsImported = 0;

            // Shelf のインポート（親子関係があるため、親から順にインポート）
            var shelfDataMap = data.Shelves.ToDictionary(s => s.Id);
            var importedShelfIds = new HashSet<string>();

            // 再帰的に親から順にインポート
            foreach (var shelfData in data.Shelves)
            {
                var imported = await ImportShelfRecursiveAsync(shelfData, shelfDataMap, importedShelfIds, cancellationToken);
                if (imported) shelvesImported++;
            }

            // Item のインポート
            foreach (var itemData in data.Items)
            {
                // インポート先の Shelf が存在するか確認
                var shelfId = new ShelfId(Guid.Parse(itemData.ShelfId));
                var shelf = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);
                if (shelf is null) continue;

                // ItemType の範囲バリデーション
                if (!Enum.IsDefined(typeof(ItemType), itemData.Type))
                    continue;

                var itemId = new ItemId(Guid.Parse(itemData.Id));

                // 既存チェック
                var existing = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
                if (existing is not null) continue;

                var item = new Item(
                    itemId,
                    shelfId,
                    (ItemType)itemData.Type,
                    itemData.Target,
                    itemData.DisplayName,
                    DateTime.Parse(itemData.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    itemData.Memo,
                    itemData.SortOrder,
                    itemData.LastAccessedAt is not null ? DateTime.Parse(itemData.LastAccessedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null);

                await _itemRepository.AddAsync(item, cancellationToken);
                itemsImported++;
            }

            return new ImportDataResult.Success(shelvesImported, itemsImported);
        }
        catch (Exception ex)
        {
            return new ImportDataResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// Shelf を再帰的にインポートする。実際にインポートした場合は true を返す。
    /// </summary>
    private async Task<bool> ImportShelfRecursiveAsync(
        ShelfData shelfData,
        Dictionary<string, ShelfData> shelfDataMap,
        HashSet<string> importedShelfIds,
        CancellationToken cancellationToken)
    {
        if (importedShelfIds.Contains(shelfData.Id)) return false;

        // 親がまだインポートされていない場合は先に親をインポート
        if (shelfData.ParentId is not null &&
            !importedShelfIds.Contains(shelfData.ParentId) &&
            shelfDataMap.TryGetValue(shelfData.ParentId, out var parentData))
        {
            await ImportShelfRecursiveAsync(parentData, shelfDataMap, importedShelfIds, cancellationToken);
        }

        var shelfId = new ShelfId(Guid.Parse(shelfData.Id));

        // 既存チェック
        var existing = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);
        if (existing is not null)
        {
            importedShelfIds.Add(shelfData.Id);
            return false;
        }

        var parentId = shelfData.ParentId is not null
            ? new ShelfId(Guid.Parse(shelfData.ParentId))
            : (ShelfId?)null;

        var shelf = new Shelf(shelfId, shelfData.Name, parentId, shelfData.SortOrder, shelfData.IsPinned);
        await _shelfRepository.AddAsync(shelf, cancellationToken);
        importedShelfIds.Add(shelfData.Id);
        return true;
    }
}

/// <summary>
/// ImportData ユースケースの実行結果
/// </summary>
public abstract record ImportDataResult
{
    public sealed record Success(int ShelvesImported, int ItemsImported) : ImportDataResult;
    public sealed record Error(string Message) : ImportDataResult;
}
