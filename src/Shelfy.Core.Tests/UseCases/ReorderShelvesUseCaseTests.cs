using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Shelves;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class ReorderShelvesUseCaseTests
{
    private readonly FakeShelfRepository _shelfRepository;
    private readonly ReorderShelvesUseCase _useCase;

    public ReorderShelvesUseCaseTests()
    {
        _shelfRepository = new FakeShelfRepository();
        _useCase = new ReorderShelvesUseCase(_shelfRepository);
    }

    [Fact]
    public async Task Execute_WithValidIds_UpdatesSortOrder()
    {
        // Arrange
        var shelf1 = new Shelf(ShelfId.New(), "Shelf 1");
        var shelf2 = new Shelf(ShelfId.New(), "Shelf 2");
        var shelf3 = new Shelf(ShelfId.New(), "Shelf 3");
        await _shelfRepository.AddAsync(shelf1);
        await _shelfRepository.AddAsync(shelf2);
        await _shelfRepository.AddAsync(shelf3);

        // Act - Reorder: shelf3, shelf1, shelf2
        var result = await _useCase.ExecuteAsync([shelf3.Id, shelf1.Id, shelf2.Id]);

        // Assert
        Assert.IsType<ReorderShelvesResult.Success>(result);
        var success = (ReorderShelvesResult.Success)result;
        Assert.NotNull(success.UpdatedShelves);
        Assert.Equal(3, success.UpdatedShelves.Count);
        
        var updatedShelf3 = await _shelfRepository.GetByIdAsync(shelf3.Id);
        var updatedShelf1 = await _shelfRepository.GetByIdAsync(shelf1.Id);
        var updatedShelf2 = await _shelfRepository.GetByIdAsync(shelf2.Id);
        
        Assert.Equal(0, updatedShelf3!.SortOrder);
        Assert.Equal(1, updatedShelf1!.SortOrder);
        Assert.Equal(2, updatedShelf2!.SortOrder);
    }

    [Fact]
    public async Task Execute_WithEmptyList_ReturnsSuccess()
    {
        // Act
        var result = await _useCase.ExecuteAsync([]);

        // Assert
        Assert.IsType<ReorderShelvesResult.Success>(result);
    }

    [Fact]
    public async Task Execute_WithSingleShelf_UpdatesSortOrder()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Single Shelf");
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync([shelf.Id]);

        // Assert
        Assert.IsType<ReorderShelvesResult.Success>(result);
        var updatedShelf = await _shelfRepository.GetByIdAsync(shelf.Id);
        Assert.Equal(0, updatedShelf!.SortOrder);
    }

    [Fact]
    public async Task Execute_WithNonExistentShelf_ReturnsShelfNotFound()
    {
        // Arrange
        var shelf1 = new Shelf(ShelfId.New(), "Shelf 1");
        await _shelfRepository.AddAsync(shelf1);
        var nonExistentId = ShelfId.New();

        // Act
        var result = await _useCase.ExecuteAsync([shelf1.Id, nonExistentId]);

        // Assert
        Assert.IsType<ReorderShelvesResult.ShelfNotFound>(result);
    }
}
