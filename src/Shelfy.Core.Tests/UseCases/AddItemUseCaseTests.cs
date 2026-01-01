using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Items;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class AddItemUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly FakeShelfRepository _shelfRepository;
    private readonly FakeClock _clock;
    private readonly AddItemUseCase _useCase;

    public AddItemUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _shelfRepository = new FakeShelfRepository();
        _clock = new FakeClock();
        _useCase = new AddItemUseCase(_itemRepository, _shelfRepository, _clock);
    }

    [Fact]
    public async Task Execute_WithValidInput_AddsItem()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(
            shelf.Id,
            ItemType.File,
            @"C:\test.txt",
            "Test File"
        );

        // Assert
        Assert.IsType<AddItemResult.Success>(result);
        var success = (AddItemResult.Success)result;
        Assert.Equal(shelf.Id, success.Item.ShelfId);
        Assert.Equal(ItemType.File, success.Item.Type);
        Assert.Equal(@"C:\test.txt", success.Item.Target);
        Assert.Equal("Test File", success.Item.DisplayName);
        Assert.Equal(1, _itemRepository.Count);
    }

    [Fact]
    public async Task Execute_WithMemo_SetsItemMemo()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(
            shelf.Id,
            ItemType.File,
            @"C:\test.txt",
            "Test File",
            memo: "This is a memo"
        );

        // Assert
        Assert.IsType<AddItemResult.Success>(result);
        var success = (AddItemResult.Success)result;
        Assert.Equal("This is a memo", success.Item.Memo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_WithInvalidTarget_ReturnsValidationError(string? invalidTarget)
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(
            shelf.Id,
            ItemType.File,
            invalidTarget!,
            "Test File"
        );

        // Assert
        Assert.IsType<AddItemResult.ValidationError>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_WithInvalidDisplayName_ReturnsValidationError(string? invalidDisplayName)
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        // Act
        var result = await _useCase.ExecuteAsync(
            shelf.Id,
            ItemType.File,
            @"C:\test.txt",
            invalidDisplayName!
        );

        // Assert
        Assert.IsType<AddItemResult.ValidationError>(result);
    }

    [Fact]
    public async Task Execute_WithNonExistentShelf_ReturnsShelfNotFound()
    {
        // Arrange
        var nonExistentShelfId = ShelfId.New();

        // Act
        var result = await _useCase.ExecuteAsync(
            nonExistentShelfId,
            ItemType.File,
            @"C:\test.txt",
            "Test File"
        );

        // Assert
        Assert.IsType<AddItemResult.ShelfNotFound>(result);
        var notFound = (AddItemResult.ShelfNotFound)result;
        Assert.Equal(nonExistentShelfId, notFound.ShelfId);
    }

    [Fact]
    public async Task Execute_WithDuplicateReference_ReturnsDuplicateItem()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var existingItem = new Item(ItemId.New(), shelf.Id, ItemType.File, @"C:\test.txt", "Existing File");
        await _itemRepository.AddAsync(existingItem);

        // Act
        var result = await _useCase.ExecuteAsync(
            shelf.Id,
            ItemType.File,
            @"C:\test.txt",
            "Duplicate File"
        );

        // Assert
        Assert.IsType<AddItemResult.DuplicateItem>(result);
    }

    [Fact]
    public async Task Execute_WithSameTargetDifferentType_Succeeds()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var existingItem = new Item(ItemId.New(), shelf.Id, ItemType.File, @"C:\test", "Existing File");
        await _itemRepository.AddAsync(existingItem);

        // Act
        var result = await _useCase.ExecuteAsync(
            shelf.Id,
            ItemType.Folder,
            @"C:\test",
            "Test Folder"
        );

        // Assert
        Assert.IsType<AddItemResult.Success>(result);
        Assert.Equal(2, _itemRepository.Count);
    }

    [Theory]
    [InlineData(ItemType.File)]
    [InlineData(ItemType.Folder)]
    [InlineData(ItemType.Url)]
    public async Task Execute_SupportsAllItemTypes(ItemType type)
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);
        var target = type == ItemType.Url ? "https://example.com" : @"C:\test";

        // Act
        var result = await _useCase.ExecuteAsync(shelf.Id, type, target, "Test Item");

        // Assert
        Assert.IsType<AddItemResult.Success>(result);
        var success = (AddItemResult.Success)result;
        Assert.Equal(type, success.Item.Type);
    }

    [Fact]
    public async Task Execute_SetsCreatedAtFromClock()
    {
        // Arrange
        var shelf = new Shelf(ShelfId.New(), "Test Shelf");
        await _shelfRepository.AddAsync(shelf);
        _clock.UtcNow = new DateTime(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var result = await _useCase.ExecuteAsync(
            shelf.Id,
            ItemType.File,
            @"C:\test.txt",
            "Test File"
        );

        // Assert
        Assert.IsType<AddItemResult.Success>(result);
        var success = (AddItemResult.Success)result;
        Assert.Equal(_clock.UtcNow, success.Item.CreatedAt);
    }
}
