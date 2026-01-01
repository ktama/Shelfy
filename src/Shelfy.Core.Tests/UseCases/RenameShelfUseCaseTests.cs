using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Shelves;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class RenameShelfUseCaseTests
{
    private readonly FakeShelfRepository _shelfRepository;
    private readonly RenameShelfUseCase _useCase;

    public RenameShelfUseCaseTests()
    {
        _shelfRepository = new FakeShelfRepository();
        _useCase = new RenameShelfUseCase(_shelfRepository);
    }

    [Fact]
    public async Task Execute_WithValidName_RenamesShelf()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Original Name");
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(shelf.Id, "New Name");

        // Assert
        Assert.IsType<RenameShelfResult.Success>(result);
        var success = (RenameShelfResult.Success)result;
        Assert.Equal("New Name", success.Shelf.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_WithInvalidName_ReturnsValidationError(string? invalidName)
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Original Name");
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(shelf.Id, invalidName!);

        // Assert
        Assert.IsType<RenameShelfResult.ValidationError>(result);
    }

    [Fact]
    public async Task Execute_WithNonExistentShelf_ReturnsShelfNotFound()
    {
        // Arrange
        var nonExistentId = ShelfId.New();

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentId, "New Name");

        // Assert
        Assert.IsType<RenameShelfResult.ShelfNotFound>(result);
    }
}
