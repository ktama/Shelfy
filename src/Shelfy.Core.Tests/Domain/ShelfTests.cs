using Shelfy.Core.Domain.Entities;
using Xunit;

namespace Shelfy.Core.Tests.Domain;

public class ShelfTests
{
    [Fact]
    public void Constructor_WithValidName_CreatesShelf()
    {
        // Arrange
        var id = ShelfId.New();
        var name = "Test Shelf";

        // Act
        var shelf = new Shelf(id, name);

        // Assert
        Assert.Equal(id, shelf.Id);
        Assert.Equal(name, shelf.Name);
        Assert.Null(shelf.ParentId);
        Assert.Equal(0, shelf.SortOrder);
        Assert.False(shelf.IsPinned);
        Assert.Empty(shelf.Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ThrowsException(string? invalidName)
    {
        // Arrange
        var id = ShelfId.New();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Shelf(id, invalidName!));
    }

    [Fact]
    public void Constructor_WithParentId_SetsParent()
    {
        // Arrange
        var id = ShelfId.New();
        var parentId = ShelfId.New();

        // Act
        var shelf = new Shelf(id, "Child Shelf", parentId);

        // Assert
        Assert.Equal(parentId, shelf.ParentId);
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Original Name");
        var newName = "New Name";

        // Act
        shelf.Rename(newName);

        // Assert
        Assert.Equal(newName, shelf.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithInvalidName_ThrowsException(string? invalidName)
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Original Name");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => shelf.Rename(invalidName!));
    }

    [Fact]
    public void MoveTo_UpdatesParentId()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        var newParentId = ShelfId.New();

        // Act
        shelf.MoveTo(newParentId);

        // Assert
        Assert.Equal(newParentId, shelf.ParentId);
    }

    [Fact]
    public void MoveTo_WithNull_RemovesParent()
    {
        // Arrange
        var parentId = ShelfId.New();
        var shelf = new Shelf(ShelfId.New(), "Test Shelf", parentId);

        // Act
        shelf.MoveTo(null);

        // Assert
        Assert.Null(shelf.ParentId);
    }

    [Fact]
    public void Pin_SetsPinnedToTrue()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");

        // Act
        shelf.Pin();

        // Assert
        Assert.True(shelf.IsPinned);
    }

    [Fact]
    public void Unpin_SetsPinnedToFalse()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf", isPinned: true);

        // Act
        shelf.Unpin();

        // Assert
        Assert.False(shelf.IsPinned);
    }

    [Fact]
    public void AddItem_AddsItemToCollection()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        var item = new Item(ItemId.New(), shelf.Id, ItemType.File, @"C:\test.txt", "Test File");

        // Act
        shelf.AddItem(item);

        // Assert
        Assert.Single(shelf.Items);
        Assert.Contains(item, shelf.Items);
    }

    [Fact]
    public void AddItem_WithDuplicateReference_ThrowsException()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        var item1 = new Item(ItemId.New(), shelf.Id, ItemType.File, @"C:\test.txt", "Test File 1");
        var item2 = new Item(ItemId.New(), shelf.Id, ItemType.File, @"C:\test.txt", "Test File 2");
        shelf.AddItem(item1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => shelf.AddItem(item2));
    }

    [Fact]
    public void AddItem_WithSameTargetDifferentType_Succeeds()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        var fileItem = new Item(ItemId.New(), shelf.Id, ItemType.File, @"C:\test", "Test File");
        var folderItem = new Item(ItemId.New(), shelf.Id, ItemType.Folder, @"C:\test", "Test Folder");
        shelf.AddItem(fileItem);

        // Act
        shelf.AddItem(folderItem);

        // Assert
        Assert.Equal(2, shelf.Items.Count);
    }

    [Fact]
    public void RemoveItem_RemovesItemFromCollection()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        var item = new Item(ItemId.New(), shelf.Id, ItemType.File, @"C:\test.txt", "Test File");
        shelf.AddItem(item);

        // Act
        shelf.RemoveItem(item.Id);

        // Assert
        Assert.Empty(shelf.Items);
    }

    [Fact]
    public void RemoveItem_WithNonExistentId_DoesNotThrow()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        var nonExistentId = ItemId.New();

        // Act & Assert (should not throw)
        shelf.RemoveItem(nonExistentId);
    }
}
