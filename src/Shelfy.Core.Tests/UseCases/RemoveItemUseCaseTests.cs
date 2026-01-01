using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Items;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class RemoveItemUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly RemoveItemUseCase _useCase;

    public RemoveItemUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _useCase = new RemoveItemUseCase(_itemRepository);
    }

    [Fact]
    public async Task Execute_WithExistingItem_RemovesItem()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test File");
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id);

        // Assert
        Assert.IsType<RemoveItemResult.Success>(result);
        Assert.False(_itemRepository.Contains(item.Id));
    }

    [Fact]
    public async Task Execute_WithNonExistentItem_ReturnsItemNotFound()
    {
        // Arrange
        var nonExistentId = ItemId.New();

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentId);

        // Assert
        Assert.IsType<RemoveItemResult.ItemNotFound>(result);
        var notFound = (RemoveItemResult.ItemNotFound)result;
        Assert.Equal(nonExistentId, notFound.ItemId);
    }
}
