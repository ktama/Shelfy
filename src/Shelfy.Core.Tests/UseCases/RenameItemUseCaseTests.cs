using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Items;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class RenameItemUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly RenameItemUseCase _useCase;

    public RenameItemUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _useCase = new RenameItemUseCase(_itemRepository);
    }

    [Fact]
    public async Task Execute_WithValidName_RenamesItem()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Original Name", DateTime.UtcNow);
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id, "New Name");

        // Assert
        Assert.IsType<RenameItemResult.Success>(result);
        var success = (RenameItemResult.Success)result;
        Assert.Equal("New Name", success.Item.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_WithInvalidName_ReturnsValidationError(string? invalidName)
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Original Name", DateTime.UtcNow);
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id, invalidName!);

        // Assert
        Assert.IsType<RenameItemResult.ValidationError>(result);
    }

    [Fact]
    public async Task Execute_WithNonExistentItem_ReturnsItemNotFound()
    {
        // Arrange
        var nonExistentId = ItemId.New();

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentId, "New Name");

        // Assert
        Assert.IsType<RenameItemResult.ItemNotFound>(result);
    }
}
