using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.Shelves;

/// <summary>
/// Shelf のピン留め状態を切り替える UseCase
/// </summary>
public class TogglePinShelfUseCase
{
    private readonly IShelfRepository _shelfRepository;

    public TogglePinShelfUseCase(IShelfRepository shelfRepository)
    {
        _shelfRepository = shelfRepository;
    }

    public async Task<TogglePinShelfResult> ExecuteAsync(
        ShelfId shelfId,
        CancellationToken cancellationToken = default)
    {
        var shelf = await _shelfRepository.GetByIdAsync(shelfId, cancellationToken);

        if (shelf is null)
        {
            return new TogglePinShelfResult.NotFound(shelfId);
        }

        // ピン状態を切り替え
        if (shelf.IsPinned)
        {
            shelf.Unpin();
        }
        else
        {
            shelf.Pin();
        }

        await _shelfRepository.UpdateAsync(shelf, cancellationToken);

        return new TogglePinShelfResult.Success(shelf);
    }
}
