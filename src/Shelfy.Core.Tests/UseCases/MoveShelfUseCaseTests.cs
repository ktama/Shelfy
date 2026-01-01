using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Shelves;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class MoveShelfUseCaseTests
{
    private readonly FakeShelfRepository _shelfRepository;
    private readonly MoveShelfUseCase _useCase;

    public MoveShelfUseCaseTests()
    {
        _shelfRepository = new FakeShelfRepository();
        _useCase = new MoveShelfUseCase(_shelfRepository);
    }

    [Fact]
    public async Task Execute_WithValidParent_MovesShelf()
    {
        // Arrange
        var parentShelf = new Shelf(ShelfId.New(), "Parent");
        var childShelf = new Shelf(ShelfId.New(), "Child");
        await _shelfRepository.AddAsync(parentShelf);
        await _shelfRepository.AddAsync(childShelf);

        // Act
        var result = await _useCase.ExecuteAsync(childShelf.Id, parentShelf.Id);

        // Assert
        Assert.IsType<MoveShelfResult.Success>(result);
        var success = (MoveShelfResult.Success)result;
        Assert.Equal(parentShelf.Id, success.Shelf.ParentId);
    }

    [Fact]
    public async Task Execute_MoveToRoot_SetsParentIdToNull()
    {
        // Arrange
        var parentShelf = new Shelf(ShelfId.New(), "Parent");
        var childShelf = new Shelf(ShelfId.New(), "Child", parentShelf.Id);
        await _shelfRepository.AddAsync(parentShelf);
        await _shelfRepository.AddAsync(childShelf);

        // Act
        var result = await _useCase.ExecuteAsync(childShelf.Id, null);

        // Assert
        Assert.IsType<MoveShelfResult.Success>(result);
        var success = (MoveShelfResult.Success)result;
        Assert.Null(success.Shelf.ParentId);
    }

    [Fact]
    public async Task Execute_WithNonExistentShelf_ReturnsShelfNotFound()
    {
        // Arrange
        var nonExistentId = ShelfId.New();

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentId, null);

        // Assert
        Assert.IsType<MoveShelfResult.ShelfNotFound>(result);
    }

    [Fact]
    public async Task Execute_WithNonExistentParent_ReturnsParentNotFound()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Shelf");
        await _shelfRepository.AddAsync(shelf);
        var nonExistentParentId = ShelfId.New();

        // Act
        var result = await _useCase.ExecuteAsync(shelf.Id, nonExistentParentId);

        // Assert
        Assert.IsType<MoveShelfResult.ParentNotFound>(result);
    }

    [Fact]
    public async Task Execute_MoveToSelf_ReturnsInvalidMove()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Shelf");
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(shelf.Id, shelf.Id);

        // Assert
        Assert.IsType<MoveShelfResult.InvalidMove>(result);
        var invalidMove = (MoveShelfResult.InvalidMove)result;
        Assert.Contains("itself", invalidMove.Message);
    }

    [Fact]
    public async Task Execute_MoveToSameParent_Succeeds()
    {
        // Arrange
        var parent = new Shelf(ShelfId.New(), "Parent");
        var child = new Shelf(ShelfId.New(), "Child", parent.Id);
        await _shelfRepository.AddAsync(parent);
        await _shelfRepository.AddAsync(child);

        // Act - Move to same parent (no-op but should succeed)
        var result = await _useCase.ExecuteAsync(child.Id, parent.Id);

        // Assert
        Assert.IsType<MoveShelfResult.Success>(result);
        var success = (MoveShelfResult.Success)result;
        Assert.Equal(parent.Id, success.Shelf.ParentId);
    }

    [Fact]
    public async Task Execute_MoveToDescendant_ReturnsInvalidMove()
    {
        // Arrange
        var grandparent = new Shelf(ShelfId.New(), "Grandparent");
        var parent = new Shelf(ShelfId.New(), "Parent", grandparent.Id);
        var child = new Shelf(ShelfId.New(), "Child", parent.Id);
        await _shelfRepository.AddAsync(grandparent);
        await _shelfRepository.AddAsync(parent);
        await _shelfRepository.AddAsync(child);

        // Act - Try to move grandparent into child (its descendant)
        var result = await _useCase.ExecuteAsync(grandparent.Id, child.Id);

        // Assert
        Assert.IsType<MoveShelfResult.InvalidMove>(result);
    }
}
