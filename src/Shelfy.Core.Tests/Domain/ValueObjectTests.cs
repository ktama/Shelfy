using Shelfy.Core.Domain.Entities;
using Xunit;

namespace Shelfy.Core.Tests.Domain;

public class ValueObjectTests
{
    [Fact]
    public void ShelfId_New_CreatesUniqueId()
    {
        // Act
        var id1 = ShelfId.New();
        var id2 = ShelfId.New();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ShelfId_WithSameValue_AreEqual()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        var id1 = new ShelfId(value);
        var id2 = new ShelfId(value);

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void ShelfId_ToString_ReturnsGuidString()
    {
        // Arrange
        var value = Guid.NewGuid();
        var id = new ShelfId(value);

        // Act
        var result = id.ToString();

        // Assert
        Assert.Equal(value.ToString(), result);
    }

    [Fact]
    public void ItemId_New_CreatesUniqueId()
    {
        // Act
        var id1 = ItemId.New();
        var id2 = ItemId.New();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ItemId_WithSameValue_AreEqual()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        var id1 = new ItemId(value);
        var id2 = new ItemId(value);

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void ItemId_ToString_ReturnsGuidString()
    {
        // Arrange
        var value = Guid.NewGuid();
        var id = new ItemId(value);

        // Act
        var result = id.ToString();

        // Assert
        Assert.Equal(value.ToString(), result);
    }
}
