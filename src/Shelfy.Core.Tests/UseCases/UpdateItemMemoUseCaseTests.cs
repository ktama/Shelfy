using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Items;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class UpdateItemMemoUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly UpdateItemMemoUseCase _useCase;

    public UpdateItemMemoUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _useCase = new UpdateItemMemoUseCase(_itemRepository);
    }

    [Fact]
    public async Task Execute_WithValidMemo_UpdatesMemo()
    {
        // Arrange
        var shelfId = ShelfId.New();
        var item = new Item(
            ItemId.New(),
            shelfId,
            ItemType.File,
            @"C:\test.txt",
            "Test File", DateTime.UtcNow, "Old memo");
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id, "New memo");

        // Assert
        Assert.IsType<UpdateItemMemoResult.Success>(result);
        var success = (UpdateItemMemoResult.Success)result;
        Assert.Equal("New memo", success.Item.Memo);
    }

    [Fact]
    public async Task Execute_WithNullMemo_ClearsMemo()
    {
        // Arrange
        var shelfId = ShelfId.New();
        var item = new Item(
            ItemId.New(),
            shelfId,
            ItemType.File,
            @"C:\test.txt",
            "Test File",
            DateTime.UtcNow,
            "Existing memo");
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id, null);

        // Assert
        Assert.IsType<UpdateItemMemoResult.Success>(result);
        var success = (UpdateItemMemoResult.Success)result;
        Assert.Null(success.Item.Memo);
    }

    [Fact]
    public async Task Execute_WithEmptyMemo_SetsEmptyMemo()
    {
        // Arrange
        var shelfId = ShelfId.New();
        var item = new Item(
            ItemId.New(),
            shelfId,
            ItemType.File,
            @"C:\test.txt",
            "Test File",
            DateTime.UtcNow,
            "Existing memo");
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id, "");

        // Assert
        Assert.IsType<UpdateItemMemoResult.Success>(result);
        var success = (UpdateItemMemoResult.Success)result;
        Assert.Equal("", success.Item.Memo);
    }

    [Fact]
    public async Task Execute_WithNonExistentItem_ReturnsItemNotFound()
    {
        // Arrange
        var nonExistentId = ItemId.New();

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentId, "Some memo");

        // Assert
        Assert.IsType<UpdateItemMemoResult.ItemNotFound>(result);
    }
}
