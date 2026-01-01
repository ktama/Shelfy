using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.System;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Items;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class GetMissingItemsUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly FakeShelfRepository _shelfRepository;
    private readonly FakeExistenceChecker _existenceChecker;
    private readonly GetMissingItemsUseCase _useCase;

    public GetMissingItemsUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _shelfRepository = new FakeShelfRepository();
        _existenceChecker = new FakeExistenceChecker();
        _useCase = new GetMissingItemsUseCase(_itemRepository, _shelfRepository, _existenceChecker);
    }

    [Fact]
    public async Task Execute_WithNoItems_ReturnsEmptyResult()
    {
        // Act
        var result = await _useCase.ExecuteAsync();

        // Assert
        Assert.IsType<GetMissingItemsResult.Success>(result);
        var success = (GetMissingItemsResult.Success)result;
        Assert.Empty(success.Items);
    }

    [Fact]
    public async Task Execute_WithAllExistingItems_ReturnsEmptyResult()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var item = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\existing.txt",
            "Existing File");
        await _itemRepository.AddAsync(item);
        _existenceChecker.SetExists(@"C:\existing.txt", true);

        // Act
        var result = await _useCase.ExecuteAsync();

        // Assert
        Assert.IsType<GetMissingItemsResult.Success>(result);
        var success = (GetMissingItemsResult.Success)result;
        Assert.Empty(success.Items);
    }

    [Fact]
    public async Task Execute_WithMissingItems_ReturnsMissingItems()
    {
        // Arrange
        var shelfId = new ShelfId(Guid.NewGuid());
        var shelf = new Shelf(shelfId, "Test Shelf");
        await _shelfRepository.AddAsync(shelf);

        var existingItem = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\existing.txt",
            "Existing File");
        await _itemRepository.AddAsync(existingItem);
        _existenceChecker.SetExists(@"C:\existing.txt", true);

        var missingItem = new Item(
            new ItemId(Guid.NewGuid()),
            shelfId,
            ItemType.File,
            @"C:\missing.txt",
            "Missing File");
        await _itemRepository.AddAsync(missingItem);
        _existenceChecker.SetExists(@"C:\missing.txt", false);

        // Act
        var result = await _useCase.ExecuteAsync();

        // Assert
        Assert.IsType<GetMissingItemsResult.Success>(result);
        var success = (GetMissingItemsResult.Success)result;
        Assert.Single(success.Items);
        Assert.Equal("Missing File", success.Items[0].Item.DisplayName);
        Assert.Equal("Test Shelf", success.Items[0].ShelfName);
    }
}

/// <summary>
/// テスト用の IExistenceChecker 実装
/// </summary>
public class FakeExistenceChecker : IExistenceChecker
{
    private readonly Dictionary<string, bool> _existenceMap = new();

    public bool Exists(string target)
    {
        return _existenceMap.TryGetValue(target, out var exists) && exists;
    }

    public void SetExists(string target, bool exists)
    {
        _existenceMap[target] = exists;
    }
}
