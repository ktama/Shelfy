using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Launch;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class OpenParentFolderUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly FakeItemLauncher _itemLauncher;
    private readonly OpenParentFolderUseCase _useCase;

    public OpenParentFolderUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _itemLauncher = new FakeItemLauncher();
        _useCase = new OpenParentFolderUseCase(_itemRepository, _itemLauncher);
    }

    [Fact]
    public async Task Execute_WithFileItem_OpensParentFolder()
    {
        // Arrange
        var shelfId = ShelfId.New();
        var item = new Item(
            ItemId.New(),
            shelfId,
            ItemType.File,
            @"C:\folder\test.txt",
            "Test File");
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id);

        // Assert
        Assert.IsType<OpenParentFolderResult.Success>(result);
        Assert.True(_itemLauncher.OpenParentFolderCalled);
    }

    [Fact]
    public async Task Execute_WithFolderItem_OpensParentFolder()
    {
        // Arrange
        var shelfId = ShelfId.New();
        var item = new Item(
            ItemId.New(),
            shelfId,
            ItemType.Folder,
            @"C:\folder\subfolder",
            "Test Folder");
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id);

        // Assert
        Assert.IsType<OpenParentFolderResult.Success>(result);
    }

    [Fact]
    public async Task Execute_WithUrlItem_ReturnsNotSupported()
    {
        // Arrange
        var shelfId = ShelfId.New();
        var item = new Item(
            ItemId.New(),
            shelfId,
            ItemType.Url,
            "https://example.com",
            "Example");
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id);

        // Assert
        Assert.IsType<OpenParentFolderResult.NotSupported>(result);
    }

    [Fact]
    public async Task Execute_WithNonExistentItem_ReturnsItemNotFound()
    {
        // Arrange
        var nonExistentId = ItemId.New();

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentId);

        // Assert
        Assert.IsType<OpenParentFolderResult.ItemNotFound>(result);
    }

    [Fact]
    public async Task Execute_WhenLauncherFails_ReturnsLaunchFailed()
    {
        // Arrange
        var shelfId = ShelfId.New();
        var item = new Item(
            ItemId.New(),
            shelfId,
            ItemType.File,
            @"C:\folder\test.txt",
            "Test File");
        await _itemRepository.AddAsync(item);
        _itemLauncher.OpenParentFolderShouldSucceed = false;

        // Act
        var result = await _useCase.ExecuteAsync(item.Id);

        // Assert
        Assert.IsType<OpenParentFolderResult.LaunchFailed>(result);
    }
}
