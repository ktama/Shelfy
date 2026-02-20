using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Shelves;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class DeleteShelfUseCaseTests
{
    private readonly FakeShelfRepository _shelfRepository;
    private readonly FakeItemRepository _itemRepository;
    private readonly DeleteShelfUseCase _useCase;

    public DeleteShelfUseCaseTests()
    {
        _shelfRepository = new FakeShelfRepository();
        _itemRepository = new FakeItemRepository();
        _useCase = new DeleteShelfUseCase(_shelfRepository, _itemRepository);
    }

    [Fact]
    public async Task Execute_WithExistingShelf_DeletesShelf()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(shelf.Id);

        // Assert
        Assert.IsType<DeleteShelfResult.Success>(result);
        Assert.False(_shelfRepository.Contains(shelf.Id));
    }

    [Fact]
    public async Task Execute_WithNonExistentShelf_ReturnsShelfNotFound()
    {
        // Arrange
        var nonExistentId = ShelfId.New();

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentId);

        // Assert
        Assert.IsType<DeleteShelfResult.ShelfNotFound>(result);
    }

    [Fact]
    public async Task Execute_DeletesItemsInShelf()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var item1 = new Item(ItemId.New(), shelf.Id, ItemType.File, @"C:\test1.txt", "File 1", DateTime.UtcNow);
        var item2 = new Item(ItemId.New(), shelf.Id, ItemType.File, @"C:\test2.txt", "File 2", DateTime.UtcNow);
        await _itemRepository.AddAsync(item1);
        await _itemRepository.AddAsync(item2);

        // Act
        await _useCase.ExecuteAsync(shelf.Id);

        // Assert
        Assert.Equal(0, _itemRepository.Count);
    }

    [Fact]
    public async Task Execute_DeletesChildShelvesRecursively()
    {
        // Arrange
        var parentShelf = new Shelf(ShelfId.New(), "Parent Shelf");
        await _shelfRepository.AddAsync(parentShelf);

        var childShelf = new Shelf(ShelfId.New(), "Child Shelf", parentShelf.Id);
        await _shelfRepository.AddAsync(childShelf);

        var grandchildShelf = new Shelf(ShelfId.New(), "Grandchild Shelf", childShelf.Id);
        await _shelfRepository.AddAsync(grandchildShelf);

        // Act
        await _useCase.ExecuteAsync(parentShelf.Id);

        // Assert
        Assert.Equal(0, _shelfRepository.Count);
    }

    [Fact]
    public async Task Execute_DeletesItemsInChildShelves()
    {
        // Arrange
        var parentShelf = new Shelf(ShelfId.New(), "Parent Shelf");
        await _shelfRepository.AddAsync(parentShelf);

        var childShelf = new Shelf(ShelfId.New(), "Child Shelf", parentShelf.Id);
        await _shelfRepository.AddAsync(childShelf);

        var parentItem = new Item(ItemId.New(), parentShelf.Id, ItemType.File, @"C:\parent.txt", "Parent File", DateTime.UtcNow);
        var childItem = new Item(ItemId.New(), childShelf.Id, ItemType.File, @"C:\child.txt", "Child File", DateTime.UtcNow);
        await _itemRepository.AddAsync(parentItem);
        await _itemRepository.AddAsync(childItem);

        // Act
        await _useCase.ExecuteAsync(parentShelf.Id);

        // Assert
        Assert.Equal(0, _itemRepository.Count);
    }
}
