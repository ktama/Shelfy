using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.Tests.Helpers;

/// <summary>
/// テスト用のインメモリ IItemRepository 実装
/// </summary>
public class FakeItemRepository : IItemRepository
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
        var items = _items.Values
            .Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Target.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (i.Memo?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        return Task.FromResult<IReadOnlyList<Item>>(items);
    }

    public Task<IReadOnlyList<Item>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        var items = _items.Values
            .Where(i => i.LastAccessedAt.HasValue)
            .OrderByDescending(i => i.LastAccessedAt)
            .Take(count)
            .ToList();
        return Task.FromResult<IReadOnlyList<Item>>(items);
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
        _items.Remove(id);
        return Task.CompletedTask;
    }

    // テスト用ヘルパー
    public void Clear() => _items.Clear();
    public int Count => _items.Count;
    public bool Contains(ItemId id) => _items.ContainsKey(id);
}
