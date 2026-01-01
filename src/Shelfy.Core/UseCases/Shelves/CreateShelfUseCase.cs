using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Shelves;

/// <summary>
/// Shelf を作成する UseCase
/// </summary>
public class CreateShelfUseCase
{
    private readonly IShelfRepository _shelfRepository;

    public CreateShelfUseCase(IShelfRepository shelfRepository)
    {
        _shelfRepository = shelfRepository;
    }

    public async Task<CreateShelfResult> ExecuteAsync(
        string name,
        ShelfId? parentId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new CreateShelfResult.ValidationError("Shelf name cannot be empty.");
        }

        // 親Shelfの存在確認
        if (parentId.HasValue)
        {
            var parent = await _shelfRepository.GetByIdAsync(parentId.Value, cancellationToken);
            if (parent is null)
            {
                return new CreateShelfResult.ParentNotFound(parentId.Value);
            }
        }

        // 同一階層の子Shelfを取得してソート順を決定
        var siblings = await _shelfRepository.GetChildrenAsync(parentId, cancellationToken);
        var nextSortOrder = siblings.Count > 0 ? siblings.Max(s => s.SortOrder) + 1 : 0;

        var shelf = new Shelf(
            id: ShelfId.New(),
            name: name,
            parentId: parentId,
            sortOrder: nextSortOrder
        );

        await _shelfRepository.AddAsync(shelf, cancellationToken);

        return new CreateShelfResult.Success(shelf);
    }
}

/// <summary>
/// CreateShelf ユースケースの実行結果
/// </summary>
public abstract record CreateShelfResult
{
    public sealed record Success(Shelf Shelf) : CreateShelfResult;
    public sealed record ValidationError(string Message) : CreateShelfResult;
    public sealed record ParentNotFound(ShelfId ParentId) : CreateShelfResult;
}
