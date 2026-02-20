namespace Shelfy.Core.Domain.Entities;

/// <summary>
/// Shelfに属する参照アイテム
/// </summary>
public class Item
{
    public ItemId Id { get; }
    public ShelfId ShelfId { get; private set; }
    public ItemType Type { get; }
    public string Target { get; }
    public string DisplayName { get; private set; }
    public string? Memo { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? LastAccessedAt { get; private set; }

    public Item(
        ItemId id,
        ShelfId shelfId,
        ItemType type,
        string target,
        string displayName,
        DateTime createdAt,
        string? memo = null,
        int sortOrder = 0,
        DateTime? lastAccessedAt = null)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Target cannot be empty.", nameof(target));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));

        Id = id;
        ShelfId = shelfId;
        Type = type;
        Target = target;
        DisplayName = displayName;
        CreatedAt = createdAt;
        Memo = memo;
        SortOrder = sortOrder;
        LastAccessedAt = lastAccessedAt;
    }

    public void Rename(string newDisplayName)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(newDisplayName));

        DisplayName = newDisplayName;
    }

    public void UpdateMemo(string? memo)
    {
        Memo = memo;
    }

    public void MarkAccessed(DateTime accessedAt)
    {
        LastAccessedAt = accessedAt;
    }

    public void SetSortOrder(int order)
    {
        SortOrder = order;
    }

    public void MoveToShelf(ShelfId newShelfId)
    {
        ShelfId = newShelfId;
    }
}
