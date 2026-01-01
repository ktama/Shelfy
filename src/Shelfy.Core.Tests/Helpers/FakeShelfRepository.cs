using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.Tests.Helpers;

/// <summary>
/// テスト用のインメモリ IShelfRepository 実装
/// </summary>
public class FakeShelfRepository : IShelfRepository
{
    private readonly Dictionary<ShelfId, Shelf> _shelves = new();

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
        _shelves.Remove(id);
        return Task.CompletedTask;
    }

    // テスト用ヘルパー
    public void Clear() => _shelves.Clear();
    public int Count => _shelves.Count;
    public bool Contains(ShelfId id) => _shelves.ContainsKey(id);
}
