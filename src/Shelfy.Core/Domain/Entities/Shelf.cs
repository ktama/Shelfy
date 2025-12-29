namespace Shelfy.Core.Domain.Entities;

/// <summary>
/// アイテム参照を格納する論理的な「棚」
/// </summary>
public class Shelf
{
    public ShelfId Id { get; }
    public string Name { get; private set; }
    public ShelfId? ParentId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsPinned { get; private set; }

    private readonly List<Item> _items = [];

    public IReadOnlyList<Item> Items => _items.AsReadOnly();

    public Shelf(ShelfId id, string name, ShelfId? parentId = null, int sortOrder = 0, bool isPinned = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Shelf name cannot be empty.", nameof(name));

        Id = id;
        Name = name;
        ParentId = parentId;
        SortOrder = sortOrder;
        IsPinned = isPinned;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Shelf name cannot be empty.", nameof(newName));

        Name = newName;
    }

    public void MoveTo(ShelfId? newParentId)
    {
        ParentId = newParentId;
    }

    public void SetSortOrder(int order)
    {
        SortOrder = order;
    }

    public void Pin() => IsPinned = true;
    public void Unpin() => IsPinned = false;

    public void AddItem(Item item)
    {
        if (_items.Any(i => i.Type == item.Type && i.Target == item.Target))
            throw new InvalidOperationException("Duplicate item reference in the same shelf.");

        _items.Add(item);
    }

    public void RemoveItem(ItemId itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
            _items.Remove(item);
    }
}
