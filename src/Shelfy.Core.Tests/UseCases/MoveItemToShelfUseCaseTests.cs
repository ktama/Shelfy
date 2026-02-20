using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Items;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class MoveItemToShelfUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly FakeShelfRepository _shelfRepository;
    private readonly MoveItemToShelfUseCase _useCase;

    public MoveItemToShelfUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _shelfRepository = new FakeShelfRepository();
        _useCase = new MoveItemToShelfUseCase(_itemRepository, _shelfRepository);
    }

    [Fact]
    public async Task Execute_WithValidTargetShelf_MovesItem()
    {
        // Arrange
        var sourceShelf = new Shelf(ShelfId.New(), "Source");
        var targetShelf = new Shelf(ShelfId.New(), "Target");
        await _shelfRepository.AddAsync(sourceShelf);
        await _shelfRepository.AddAsync(targetShelf);

        var item = new Item(
            ItemId.New(),
            sourceShelf.Id,
            ItemType.File,
            @"C:\test.txt",
            "Test File",
            DateTime.UtcNow);
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id, targetShelf.Id);

        // Assert
        Assert.IsType<MoveItemToShelfResult.Success>(result);
        var success = (MoveItemToShelfResult.Success)result;
        Assert.Equal(targetShelf.Id, success.Item.ShelfId);
    }

    [Fact]
    public async Task Execute_MoveToSameShelf_Succeeds()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Same Shelf");
        await _shelfRepository.AddAsync(shelf);

        var item = new Item(
            ItemId.New(),
            shelf.Id,
            ItemType.File,
            @"C:\test.txt",
            "Test File",
            DateTime.UtcNow);
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id, shelf.Id);

        // Assert
        Assert.IsType<MoveItemToShelfResult.Success>(result);
        var success = (MoveItemToShelfResult.Success)result;
        Assert.Equal(shelf.Id, success.Item.ShelfId);
    }

    [Fact]
    public async Task Execute_WithNonExistentItem_ReturnsItemNotFound()
    {
        // Arrange
        var targetShelf = new Shelf(ShelfId.New(), "Target");
        await _shelfRepository.AddAsync(targetShelf);
        var nonExistentItemId = ItemId.New();

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentItemId, targetShelf.Id);

        // Assert
        Assert.IsType<MoveItemToShelfResult.ItemNotFound>(result);
    }

    [Fact]
    public async Task Execute_WithNonExistentTargetShelf_ReturnsShelfNotFound()
    {
        // Arrange
        var sourceShelf = new Shelf(ShelfId.New(), "Source");
        await _shelfRepository.AddAsync(sourceShelf);

        var item = new Item(
            ItemId.New(),
            sourceShelf.Id,
            ItemType.File,
            @"C:\test.txt",
            "Test File",
            DateTime.UtcNow);
        await _itemRepository.AddAsync(item);
        var nonExistentShelfId = ShelfId.New();

        // Act
        var result = await _useCase.ExecuteAsync(item.Id, nonExistentShelfId);

        // Assert
        Assert.IsType<MoveItemToShelfResult.ShelfNotFound>(result);
    }

    [Fact]
    public async Task Execute_WithDuplicateReference_ReturnsDuplicateItem()
    {
        // Arrange
        var sourceShelf = new Shelf(ShelfId.New(), "Source");
        var targetShelf = new Shelf(ShelfId.New(), "Target");
        await _shelfRepository.AddAsync(sourceShelf);
        await _shelfRepository.AddAsync(targetShelf);

        var existingItem = new Item(
            ItemId.New(),
            targetShelf.Id,
            ItemType.File,
            @"C:\test.txt",
            "Existing File",
            DateTime.UtcNow);
        await _itemRepository.AddAsync(existingItem);

        var itemToMove = new Item(
            ItemId.New(),
            sourceShelf.Id,
            ItemType.File,
            @"C:\test.txt",  // Same target as existingItem
            "File to Move",
            DateTime.UtcNow);
        await _itemRepository.AddAsync(itemToMove);

        // Act
        var result = await _useCase.ExecuteAsync(itemToMove.Id, targetShelf.Id);

        // Assert
        Assert.IsType<MoveItemToShelfResult.DuplicateItem>(result);
    }
}
