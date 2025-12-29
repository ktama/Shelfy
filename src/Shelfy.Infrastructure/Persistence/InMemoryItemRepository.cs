using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Infrastructure.Persistence;

/// <summary>
/// Item のインメモリリポジトリ（開発用）
/// </summary>
public class InMemoryItemRepository : IItemRepository
{
    private readonly Dictionary<ItemId, Item> _items = new();

    public Task<Item?> GetByIdAsync(ItemId id, CancellationToken cancellationToken = default)
    {
        _items.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<Item>> GetByShelfIdAsync(ShelfId shelfId, CancellationToken cancellationToken = default)
    {
        var items = _items.Values
            .Where(i => i.ShelfId == shelfId)
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
        _items.Remove(id);
        return Task.CompletedTask;
    }
}
