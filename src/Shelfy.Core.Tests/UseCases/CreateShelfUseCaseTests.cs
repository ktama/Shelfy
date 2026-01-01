using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Shelves;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class CreateShelfUseCaseTests
{
    private readonly FakeShelfRepository _shelfRepository;
    private readonly CreateShelfUseCase _useCase;

    public CreateShelfUseCaseTests()
    {
        _shelfRepository = new FakeShelfRepository();
        _useCase = new CreateShelfUseCase(_shelfRepository);
    }

    [Fact]
    public async Task Execute_WithValidName_CreatesShelf()
    {
        // Arrange
        var name = "New Shelf";

        // Act
        var result = await _useCase.ExecuteAsync(name);

        // Assert
        Assert.IsType<CreateShelfResult.Success>(result);
        var success = (CreateShelfResult.Success)result;
        Assert.Equal(name, success.Shelf.Name);
        Assert.Null(success.Shelf.ParentId);
        Assert.Equal(1, _shelfRepository.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_WithInvalidName_ReturnsValidationError(string? invalidName)
    {
        // Act
        var result = await _useCase.ExecuteAsync(invalidName!);

        // Assert
        Assert.IsType<CreateShelfResult.ValidationError>(result);
        Assert.Equal(0, _shelfRepository.Count);
    }

    [Fact]
    public async Task Execute_WithValidParentId_CreatesChildShelf()
    {
        // Arrange
        var parentShelf = new Shelf(ShelfId.New(), "Parent Shelf");
        await _shelfRepository.AddAsync(parentShelf);

        // Act
        var result = await _useCase.ExecuteAsync("Child Shelf", parentShelf.Id);

        // Assert
        Assert.IsType<CreateShelfResult.Success>(result);
        var success = (CreateShelfResult.Success)result;
        Assert.Equal(parentShelf.Id, success.Shelf.ParentId);
    }

    [Fact]
    public async Task Execute_WithInvalidParentId_ReturnsParentNotFound()
    {
        // Arrange
        var nonExistentParentId = ShelfId.New();

        // Act
        var result = await _useCase.ExecuteAsync("Child Shelf", nonExistentParentId);

        // Assert
        Assert.IsType<CreateShelfResult.ParentNotFound>(result);
        var notFound = (CreateShelfResult.ParentNotFound)result;
        Assert.Equal(nonExistentParentId, notFound.ParentId);
    }

    [Fact]
    public async Task Execute_MultipleShelves_AssignsIncrementingSortOrder()
    {
        // Arrange & Act
        var result1 = await _useCase.ExecuteAsync("Shelf 1");
        var result2 = await _useCase.ExecuteAsync("Shelf 2");
        var result3 = await _useCase.ExecuteAsync("Shelf 3");

        // Assert
        Assert.IsType<CreateShelfResult.Success>(result1);
        Assert.IsType<CreateShelfResult.Success>(result2);
        Assert.IsType<CreateShelfResult.Success>(result3);

        var shelf1 = ((CreateShelfResult.Success)result1).Shelf;
        var shelf2 = ((CreateShelfResult.Success)result2).Shelf;
        var shelf3 = ((CreateShelfResult.Success)result3).Shelf;

        Assert.Equal(0, shelf1.SortOrder);
        Assert.Equal(1, shelf2.SortOrder);
        Assert.Equal(2, shelf3.SortOrder);
    }
}
