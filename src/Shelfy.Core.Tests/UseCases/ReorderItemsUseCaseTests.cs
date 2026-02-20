using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Items;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class ReorderItemsUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly ReorderItemsUseCase _useCase;

    public ReorderItemsUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _useCase = new ReorderItemsUseCase(_itemRepository);
    }

    [Fact]
    public async Task Execute_WithValidIds_UpdatesSortOrder()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var item1 = new Item(new ItemId(Guid.NewGuid()), shelfId, ItemType.File, "C:\\file1.txt", "File 1", DateTime.UtcNow);
        var item2 = new Item(new ItemId(Guid.NewGuid()), shelfId, ItemType.File, "C:\\file2.txt", "File 2", DateTime.UtcNow);
        var item3 = new Item(new ItemId(Guid.NewGuid()), shelfId, ItemType.File, "C:\\file3.txt", "File 3", DateTime.UtcNow);
        await _itemRepository.AddAsync(item1);
        await _itemRepository.AddAsync(item2);
        await _itemRepository.AddAsync(item3);

        // Act - Reorder: item3, item1, item2
        var result = await _useCase.ExecuteAsync([item3.Id, item1.Id, item2.Id]);

        // Assert
        Assert.IsType<ReorderItemsResult.Success>(result);
        var success = (ReorderItemsResult.Success)result;
        Assert.NotNull(success.UpdatedItems);
        Assert.Equal(3, success.UpdatedItems.Count);

        var updatedItem3 = await _itemRepository.GetByIdAsync(item3.Id);
        var updatedItem1 = await _itemRepository.GetByIdAsync(item1.Id);
        var updatedItem2 = await _itemRepository.GetByIdAsync(item2.Id);

        Assert.Equal(0, updatedItem3!.SortOrder);
        Assert.Equal(1, updatedItem1!.SortOrder);
        Assert.Equal(2, updatedItem2!.SortOrder);
    }

    [Fact]
    public async Task Execute_WithEmptyList_ReturnsSuccess()
    {
        // Act
        var result = await _useCase.ExecuteAsync([]);

        // Assert
        Assert.IsType<ReorderItemsResult.Success>(result);
    }

    [Fact]
    public async Task Execute_WithNonExistentId_ReturnsItemNotFound()
    {
        // Arrange
        var nonExistentId = new ItemId(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync([nonExistentId]);

        // Assert
        var notFound = Assert.IsType<ReorderItemsResult.ItemNotFound>(result);
        Assert.Equal(nonExistentId, notFound.ItemId);
    }

    [Fact]
    public async Task Execute_SortOrderIsPersistedAndAffectsGetByShelfId()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var item1 = new Item(new ItemId(Guid.NewGuid()), shelfId, ItemType.File, "C:\\a.txt", "Alpha", DateTime.UtcNow);
        var item2 = new Item(new ItemId(Guid.NewGuid()), shelfId, ItemType.File, "C:\\b.txt", "Beta", DateTime.UtcNow);
        var item3 = new Item(new ItemId(Guid.NewGuid()), shelfId, ItemType.File, "C:\\c.txt", "Charlie", DateTime.UtcNow);
        await _itemRepository.AddAsync(item1);
        await _itemRepository.AddAsync(item2);
        await _itemRepository.AddAsync(item3);

        // Act - Reorder: Charlie, Alpha, Beta
        await _useCase.ExecuteAsync([item3.Id, item1.Id, item2.Id]);

        // Assert - GetByShelfId should return sorted by SortOrder
        var items = await _itemRepository.GetByShelfIdAsync(shelfId);
        Assert.Equal(3, items.Count);
        Assert.Equal("Charlie", items[0].DisplayName);
        Assert.Equal("Alpha", items[1].DisplayName);
        Assert.Equal("Beta", items[2].DisplayName);
    }
}
