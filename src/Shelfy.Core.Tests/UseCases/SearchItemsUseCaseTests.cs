using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Search;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class SearchItemsUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly FakeShelfRepository _shelfRepository;
    private readonly SearchItemsUseCase _useCase;

    public SearchItemsUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _shelfRepository = new FakeShelfRepository();
        _useCase = new SearchItemsUseCase(_itemRepository, _shelfRepository);
    }

    [Fact]
    public async Task Execute_WithEmptyQuery_ReturnsEmptyResult()
    {
        // Act
        var result = await _useCase.ExecuteAsync("");

        // Assert
        Assert.IsType<SearchResult.Success>(result);
        var success = (SearchResult.Success)result;
        Assert.Empty(success.Items);
    }

    [Fact]
    public async Task Execute_WithMatchingDisplayName_ReturnsItems()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var item = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\test\document.txt",
            "Important Document",
            DateTime.UtcNow);
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync("Important");

        // Assert
        Assert.IsType<SearchResult.Success>(result);
        var success = (SearchResult.Success)result;
        Assert.Single(success.Items);
        Assert.Equal("Important Document", success.Items[0].Item.DisplayName);
        Assert.Equal("Test Shelf", success.Items[0].ShelfName);
    }

    [Fact]
    public async Task Execute_WithMatchingTarget_ReturnsItems()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var item = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\projects\myapp\readme.md",
            "Readme",
            DateTime.UtcNow);
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync("myapp");

        // Assert
        Assert.IsType<SearchResult.Success>(result);
        var success = (SearchResult.Success)result;
        Assert.Single(success.Items);
    }

    [Fact]
    public async Task Execute_WithTypeFilter_FiltersCorrectly()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var fileItem = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\test\file.txt",
            "Test File",
            DateTime.UtcNow);
        await _itemRepository.AddAsync(fileItem);

        var folderItem = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.Folder,
            @"C:\test\folder",
            "Test Folder",
            DateTime.UtcNow);
        await _itemRepository.AddAsync(folderItem);

        // Act
        var result = await _useCase.ExecuteAsync("Test type:file");

        // Assert
        Assert.IsType<SearchResult.Success>(result);
        var success = (SearchResult.Success)result;
        Assert.Single(success.Items);
        Assert.Equal(ItemType.File, success.Items[0].Item.Type);
    }
}
