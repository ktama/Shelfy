using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.Launch;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class LaunchItemUseCaseTests
{
    private readonly FakeItemRepository _itemRepository;
    private readonly FakeItemLauncher _launcher;
    private readonly FakeHotkeyHoldState _hotkeyHoldState;
    private readonly FakeClock _clock;
    private readonly LaunchItemUseCase _useCase;

    public LaunchItemUseCaseTests()
    {
        _itemRepository = new FakeItemRepository();
        _launcher = new FakeItemLauncher();
        _hotkeyHoldState = new FakeHotkeyHoldState();
        _clock = new FakeClock();
        _useCase = new LaunchItemUseCase(_itemRepository, _launcher, _hotkeyHoldState, _clock);
    }

    [Fact]
    public async Task Execute_WithValidItem_LaunchesSuccessfully()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test File");
        await _itemRepository.AddAsync(item);

        // Act
        var result = await _useCase.ExecuteAsync(item.Id);

        // Assert
        Assert.IsType<LaunchItemResult.Success>(result);
        Assert.Single(_launcher.LaunchedItems);
        Assert.Equal(item.Id, _launcher.LaunchedItems[0].Id);
    }

    [Fact]
    public async Task Execute_WithNonExistentItem_ReturnsItemNotFound()
    {
        // Arrange
        var nonExistentId = ItemId.New();

        // Act
        var result = await _useCase.ExecuteAsync(nonExistentId);

        // Assert
        Assert.IsType<LaunchItemResult.ItemNotFound>(result);
        var notFound = (LaunchItemResult.ItemNotFound)result;
        Assert.Equal(nonExistentId, notFound.ItemId);
    }

    [Fact]
    public async Task Execute_WhenLaunchFails_ReturnsLaunchFailed()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test File");
        await _itemRepository.AddAsync(item);
        _launcher.ShouldSucceed = false;

        // Act
        var result = await _useCase.ExecuteAsync(item.Id);

        // Assert
        Assert.IsType<LaunchItemResult.LaunchFailed>(result);
    }

    [Fact]
    public async Task Execute_UpdatesLastAccessedAt()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test File");
        await _itemRepository.AddAsync(item);
        _clock.UtcNow = new DateTime(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        await _useCase.ExecuteAsync(item.Id);

        // Assert
        var updatedItem = await _itemRepository.GetByIdAsync(item.Id);
        Assert.Equal(_clock.UtcNow, updatedItem!.LastAccessedAt);
    }

    [Fact]
    public async Task Execute_WhenHotkeyNotHeld_ReturnsHideWindow()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test File");
        await _itemRepository.AddAsync(item);
        _hotkeyHoldState.IsHotkeyHeld = false;

        // Act
        var result = await _useCase.ExecuteAsync(item.Id);

        // Assert
        Assert.IsType<LaunchItemResult.Success>(result);
        var success = (LaunchItemResult.Success)result;
        Assert.Equal(PostLaunchAction.HideWindow, success.PostAction);
    }

    [Fact]
    public async Task Execute_WhenHotkeyHeld_ReturnsKeepWindow()
    {
        // Arrange
        var item = new Item(ItemId.New(), ShelfId.New(), ItemType.File, @"C:\test.txt", "Test File");
        await _itemRepository.AddAsync(item);
        _hotkeyHoldState.IsHotkeyHeld = true;

        // Act
        var result = await _useCase.ExecuteAsync(item.Id);

        // Assert
        Assert.IsType<LaunchItemResult.Success>(result);
        var success = (LaunchItemResult.Success)result;
        Assert.Equal(PostLaunchAction.KeepWindow, success.PostAction);
    }
}
