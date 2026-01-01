using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.System;

namespace Shelfy.Core.Tests.Helpers;

/// <summary>
/// テスト用の IItemLauncher 実装
/// </summary>
public class FakeItemLauncher : IItemLauncher
{
    public bool ShouldSucceed { get; set; } = true;
    public bool OpenParentFolderShouldSucceed { get; set; } = true;
    public bool OpenParentFolderCalled { get; private set; }
    public List<Item> LaunchedItems { get; } = new();

    public Task<bool> LaunchAsync(Item item)
    {
        LaunchedItems.Add(item);
        return Task.FromResult(ShouldSucceed);
    }

    public Task<bool> OpenParentFolderAsync(Item item)
    {
        OpenParentFolderCalled = true;
        return Task.FromResult(OpenParentFolderShouldSucceed);
    }
}
