using Shelfy.Core.Domain.Entities;
using Xunit;

namespace Shelfy.Core.Tests.Domain;

public class ItemTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesItem()
    {
        // Arrange
        var id = ItemId.New();
        var shelfId = ShelfId.New();
        var type = ItemType.File;
        var target = @"C:\test.txt";
        var displayName = "Test File";

        // Act
        var item = new Item(id, shelfId, type, target, displayName, DateTime.UtcNow);

        // Assert
        Assert.Equal(id, item.Id);
        Assert.Equal(shelfId, item.ShelfId);
        Assert.Equal(type, item.Type);
        Assert.Equal(target, item.Target);
        Assert.Equal(displayName, item.DisplayName);
        Assert.Null(item.Memo);
        Assert.Null(item.LastAccessedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTarget_ThrowsException(string? invalidTarget)
    {
        // Arrange
        var id = ItemId.New();
        var shelfId = ShelfId.New();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Item(id, shelfId, ItemType.File, invalidTarget!, "Display Name", DateTime.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDisplayName_ThrowsException(string? invalidDisplayName)
    {
        // Arrange
        var id = ItemId.New();
        var shelfId = ShelfId.New();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Item(id, shelfId, ItemType.File, @"C:\test.txt", invalidDisplayName!, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_WithMemo_SetsMemo()
    {
        // Arrange & Act
        var item = new Item(
            ItemId.New(),
            ShelfId.New(),
            ItemType.File,
            @"C:\test.txt",
            "Test File",
            DateTime.UtcNow,
            memo: "This is a test memo"
        );

        // Assert
        Assert.Equal("This is a test memo", item.Memo);
    }

    [Fact]
    public void Rename_WithValidName_UpdatesDisplayName()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Original Name", DateTime.UtcNow);
        var newName = "New Name";

        // Act
        item.Rename(newName);

        // Assert
        Assert.Equal(newName, item.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithInvalidName_ThrowsException(string? invalidName)
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Original Name", DateTime.UtcNow);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => item.Rename(invalidName!));
    }

    [Fact]
    public void UpdateMemo_SetsMemo()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test File", DateTime.UtcNow);

        // Act
        item.UpdateMemo("New memo");

        // Assert
        Assert.Equal("New memo", item.Memo);
    }

    [Fact]
    public void UpdateMemo_WithNull_ClearsMemo()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test File", DateTime.UtcNow, memo: "Original memo");

        // Act
        item.UpdateMemo(null);

        // Assert
        Assert.Null(item.Memo);
    }

    [Fact]
    public void MarkAccessed_UpdatesLastAccessedAt()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test File", DateTime.UtcNow);
        var accessedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        item.MarkAccessed(accessedAt);

        // Assert
        Assert.Equal(accessedAt, item.LastAccessedAt);
    }

    [Fact]
    public void MoveToShelf_UpdatesShelfId()
    {
        // Arrange
        var originalShelfId = ShelfId.New();
        var newShelfId = ShelfId.New();
        var item = new Item(ItemId.New(), originalShelfId, ItemType.File, @"C:\test.txt", "Test File", DateTime.UtcNow);

        // Act
        item.MoveToShelf(newShelfId);

        // Assert
        Assert.Equal(newShelfId, item.ShelfId);
    }

    [Theory]
    [InlineData(ItemType.File)]
    [InlineData(ItemType.Folder)]
    [InlineData(ItemType.Url)]
    public void Constructor_SupportsAllItemTypes(ItemType type)
    {
        // Arrange
        var target = type == ItemType.Url ? "https://example.com" : @"C:\test";

        // Act
        var item = new Item(ItemId.New(), ShelfId.New(), type, target, "Test Item", DateTime.UtcNow);

        // Assert
        Assert.Equal(type, item.Type);
    }

    [Fact]
    public void SetSortOrder_UpdatesSortOrder()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test", DateTime.UtcNow);
        Assert.Equal(0, item.SortOrder);

        // Act
        item.SetSortOrder(5);

        // Assert
        Assert.Equal(5, item.SortOrder);
    }

    [Fact]
    public void Constructor_WithSortOrder_SetsSortOrder()
    {
        // Act
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test", DateTime.UtcNow, sortOrder: 3);

        // Assert
        Assert.Equal(3, item.SortOrder);
    }
}
