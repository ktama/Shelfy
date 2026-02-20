using System.Collections.Concurrent;
using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Infrastructure.Persistence;

/// <summary>
/// Item のインメモリリポジトリ（開発用）
/// </summary>
public class InMemoryItemRepository : IItemRepository
{
    private readonly ConcurrentDictionary<ItemId, Item> _items = new();

    public Task<Item?> GetByIdAsync(ItemId id, CancellationToken cancellationToken = default)
    {
        _items.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<Item>> GetByShelfIdAsync(ShelfId shelfId, CancellationToken cancellationToken = default)
    {
        var items = _items.Values
            .Where(i => i.ShelfId == shelfId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.DisplayName)
            .ToList();
        return Task.FromResult<IReadOnlyList<Item>>(items);
    }

    public Task<IReadOnlyList<Item>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var lowerQuery = query.ToLowerInvariant();
        var results = _items.Values
            .Where(i =>
                i.DisplayName.ToLowerInvariant().Contains(lowerQuery) ||
                i.Target.ToLowerInvariant().Contains(lowerQuery) ||
                (i.Memo?.ToLowerInvariant().Contains(lowerQuery) ?? false))
            .ToList();
        return Task.FromResult<IReadOnlyList<Item>>(results);
    }

    public Task<IReadOnlyList<Item>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        var results = _items.Values
            .Where(i => i.LastAccessedAt.HasValue)
            .OrderByDescending(i => i.LastAccessedAt)
            .Take(count)
            .ToList();
        return Task.FromResult<IReadOnlyList<Item>>(results);
    }

    public Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Item>>(_items.Values.ToList());
    }

    public Task AddAsync(Item item, CancellationToken cancellationToken = default)
    {
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Item item, CancellationToken cancellationToken = default)
    {
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ItemId id, CancellationToken cancellationToken = default)
    {
        _items.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task DeleteByShelfIdAsync(ShelfId shelfId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _items.Where(kv => kv.Value.ShelfId == shelfId).Select(kv => kv.Key).ToList();
        foreach (var key in keysToRemove)
        {
            _items.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }
}
