using Shelfy.Core.Domain.Entities;

namespace Shelfy.Core.Ports.Persistence;

/// <summary>
/// Item の永続化インターフェース
/// </summary>
public interface IItemRepository
{
    Task<Item?> GetByIdAsync(ItemId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetByShelfIdAsync(ShelfId shelfId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Item item, CancellationToken cancellationToken = default);
    Task UpdateAsync(Item item, CancellationToken cancellationToken = default);
    Task DeleteAsync(ItemId id, CancellationToken cancellationToken = default);
    Task DeleteByShelfIdAsync(ShelfId shelfId, CancellationToken cancellationToken = default);
}
