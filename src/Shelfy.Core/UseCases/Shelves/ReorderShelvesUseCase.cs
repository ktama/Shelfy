using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Shelves;

/// <summary>
/// Shelf の並び順を変更する UseCase
/// </summary>
public class ReorderShelvesUseCase
{
    private readonly IShelfRepository _shelfRepository;

    public ReorderShelvesUseCase(IShelfRepository shelfRepository)
    {
        _shelfRepository = shelfRepository;
    }

    /// <summary>
    /// 指定された順序で Shelf の SortOrder を更新する
    /// </summary>
    /// <param name="orderedShelfIds">新しい順序での ShelfId リスト</param>
    public async Task<ReorderShelvesResult> ExecuteAsync(
        IReadOnlyList<ShelfId> orderedShelfIds,
        CancellationToken cancellationToken = default)
    {
        if (orderedShelfIds.Count == 0)
        {
            return new ReorderShelvesResult.Success();
        }

        var updatedShelves = new List<Shelf>();

        for (var i = 0; i < orderedShelfIds.Count; i++)
        {
            var shelfId = orderedShelfIds[i];
            var shelf = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);
            
            if (shelf is null)
            {
                return new ReorderShelvesResult.ShelfNotFound(shelfId);
            }

            shelf.SetSortOrder(i);
            await _shelfRepository.UpdateAsync(shelf, cancellationToken);
            updatedShelves.Add(shelf);
        }

        return new ReorderShelvesResult.Success(updatedShelves);
    }
}

/// <summary>
/// ReorderShelves ユースケースの実行結果
/// </summary>
public abstract record ReorderShelvesResult
{
    public sealed record Success(IReadOnlyList<Shelf>? UpdatedShelves = null) : ReorderShelvesResult;
    public sealed record ShelfNotFound(ShelfId ShelfId) : ReorderShelvesResult;
}
