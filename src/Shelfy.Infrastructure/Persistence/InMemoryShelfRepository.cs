using System.Collections.Concurrent;
using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Infrastructure.Persistence;

/// <summary>
/// Shelf のインメモリリポジトリ（開発用）
/// </summary>
public class InMemoryShelfRepository : IShelfRepository
{
    private readonly ConcurrentDictionary<ShelfId, Shelf> _shelves = new();

    public Task<Shelf?> GetByIdAsync(ShelfId id, CancellationToken cancellationToken = default)
    {
        _shelves.TryGetValue(id, out var shelf);
        return Task.FromResult(shelf);
    }

    public Task<IReadOnlyList<Shelf>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Shelf>>(_shelves.Values.ToList());
    }

    public Task<IReadOnlyList<Shelf>> GetChildrenAsync(ShelfId? parentId, CancellationToken cancellationToken = default)
    {
        var children = _shelves.Values
            .Where(s => s.ParentId == parentId)
            .OrderBy(s => s.SortOrder)
            .ToList();
        return Task.FromResult<IReadOnlyList<Shelf>>(children);
    }

    public Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default)
    {
        _shelves[shelf.Id] = shelf;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Shelf shelf, CancellationToken cancellationToken = default)
    {
        _shelves[shelf.Id] = shelf;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ShelfId id, CancellationToken cancellationToken = default)
    {
        _shelves.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
