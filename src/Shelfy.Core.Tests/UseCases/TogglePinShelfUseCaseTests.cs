using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Shelves;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class TogglePinShelfUseCaseTests
{
    private readonly FakeShelfRepository _shelfRepository;
    private readonly TogglePinShelfUseCase _useCase;

    public TogglePinShelfUseCaseTests()
    {
        _shelfRepository = new FakeShelfRepository();
        _useCase = new TogglePinShelfUseCase(_shelfRepository);
    }

    [Fact]
    public async Task Execute_WhenShelfNotPinned_PinsShelf()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf", isPinned: false);
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(shelfId);

        // Assert
        Assert.IsType<TogglePinShelfResult.Success>(result);
        var success = (TogglePinShelfResult.Success)result;
        Assert.True(success.Shelf.IsPinned);
    }

    [Fact]
    public async Task Execute_WhenShelfPinned_UnpinsShelf()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf", isPinned: true);
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(shelfId);

        // Assert
        Assert.IsType<TogglePinShelfResult.Success>(result);
        var success = (TogglePinShelfResult.Success)result;
        Assert.False(success.Shelf.IsPinned);
    }

    [Fact]
    public async Task Execute_WhenShelfNotFound_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = new ShelfId(Guid.NewGuid());

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentId);

        // Assert
        Assert.IsType<TogglePinShelfResult.NotFound>(result);
    }

    [Fact]
    public async Task Execute_UpdatesRepository()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf", isPinned: false);
        await _shelfRepository.AddAsync(shelf);

        // Act
        await _useCase.ExecuteAsync(shelfId);

        // Assert
        var updatedShelf = await _shelfRepository.GetByIdAsync(shelfId);
        Assert.NotNull(updatedShelf);
        Assert.True(updatedShelf.IsPinned);
    }
}
