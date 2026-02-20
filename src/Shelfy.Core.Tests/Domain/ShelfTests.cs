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
}
