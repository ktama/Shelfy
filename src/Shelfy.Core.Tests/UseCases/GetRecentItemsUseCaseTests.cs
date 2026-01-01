using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Items;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class GetRecentItemsUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly FakeShelfRepository _shelfRepository;
    private readonly GetRecentItemsUseCase _useCase;

    public GetRecentItemsUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _shelfRepository = new FakeShelfRepository();
        _useCase = new GetRecentItemsUseCase(_itemRepository, _shelfRepository);
    }

    [Fact]
    public async Task Execute_WithNoItems_ReturnsEmptyResult()
    {
        // Act
        var result = await _useCase.ExecuteAsync();

        // Assert
        Assert.IsType<GetRecentItemsResult.Success>(result);
        var success = (GetRecentItemsResult.Success)result;
        Assert.Empty(success.Items);
    }

    [Fact]
    public async Task Execute_ReturnsItemsOrderedByLastAccessedAt()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var oldItem = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\old.txt",
            "Old File",
            lastAccessedAt: DateTime.UtcNow.AddDays(-5));
        await _itemRepository.AddAsync(oldItem);

        var recentItem = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\recent.txt",
            "Recent File",
            lastAccessedAt: DateTime.UtcNow.AddHours(-1));
        await _itemRepository.AddAsync(recentItem);

        // Act
        var result = await _useCase.ExecuteAsync();

        // Assert
        Assert.IsType<GetRecentItemsResult.Success>(result);
        var success = (GetRecentItemsResult.Success)result;
        Assert.Equal(2, success.Items.Count);
        Assert.Equal("Recent File", success.Items[0].Item.DisplayName);
        Assert.Equal("Old File", success.Items[1].Item.DisplayName);
    }

    [Fact]
    public async Task Execute_RespectsCountParameter()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        for (int i = 0; i < 5; i++)
        {
            var item = new Item(
                new ItemId(Guid.NewGuid()),
                shelfId,
                ItemType.File,
                $@"C:\file{i}.txt",
                $"File {i}",
                lastAccessedAt: DateTime.UtcNow.AddHours(-i));
            await _itemRepository.AddAsync(item);
        }

        // Act
        var result = await _useCase.ExecuteAsync(count: 3);

        // Assert
        Assert.IsType<GetRecentItemsResult.Success>(result);
        var success = (GetRecentItemsResult.Success)result;
        Assert.Equal(3, success.Items.Count);
    }

    [Fact]
    public async Task Execute_ExcludesItemsWithoutLastAccessedAt()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var accessedItem = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\accessed.txt",
            "Accessed File",
            lastAccessedAt: DateTime.UtcNow);
        await _itemRepository.AddAsync(accessedItem);

        var neverAccessedItem = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\never.txt",
            "Never Accessed File");
        await _itemRepository.AddAsync(neverAccessedItem);

        // Act
        var result = await _useCase.ExecuteAsync();

        // Assert
        Assert.IsType<GetRecentItemsResult.Success>(result);
        var success = (GetRecentItemsResult.Success)result;
        Assert.Single(success.Items);
        Assert.Equal("Accessed File", success.Items[0].Item.DisplayName);
    }
}
