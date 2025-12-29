using Shelfy.Core.Domain.Entities;

namespace Shelfy.Core.Ports.Persistence;

/// <summary>
/// Shelf の永続化インターフェース
/// </summary>
public interface IShelfRepository
{
    Task<Shelf?> GetByIdAsync(ShelfId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Shelf>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Shelf>> GetChildrenAsync(ShelfId? parentId, CancellationToken cancellationToken = default);
    Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default);
    Task UpdateAsync(Shelf shelf, CancellationToken cancellationToken = default);
    Task DeleteAsync(ShelfId id, CancellationToken cancellationToken = default);
}
