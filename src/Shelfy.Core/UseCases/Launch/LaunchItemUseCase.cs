using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;

namespace Shelfy.Core.UseCases.Launch;

/// <summary>
/// Item を起動する UseCase
/// </summary>
public class LaunchItemUseCase
{
    private readonly IItemRepository _itemRepository;
    private readonly IItemLauncher _launcher;
    private readonly IHotkeyHoldState _hotkeyHoldState;
    private readonly IClock _clock;

    public LaunchItemUseCase(
        IItemRepository itemRepository,
        IItemLauncher launcher,
        IHotkeyHoldState hotkeyHoldState,
        IClock clock)
    {
        _itemRepository = itemRepository;
        _launcher = launcher;
        _hotkeyHoldState = hotkeyHoldState;
        _clock = clock;
    }

    public async Task<LaunchItemResult> ExecuteAsync(ItemId itemId, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);

        if (item is null)
        {
            return new LaunchItemResult.ItemNotFound(itemId);
        }

        var success = await _launcher.LaunchAsync(item);

        if (!success)
        {
            return new LaunchItemResult.LaunchFailed($"Failed to launch: {item.Target}");
        }

        // アクセス日時を更新
        item.MarkAccessed(_clock.UtcNow);
        await _itemRepository.UpdateAsync(item, cancellationToken);

        // ホットキー押下中はウィンドウを維持
        var postAction = _hotkeyHoldState.IsHotkeyHeld
            ? PostLaunchAction.KeepWindow
            : PostLaunchAction.HideWindow;

        return new LaunchItemResult.Success(postAction);
    }
}
