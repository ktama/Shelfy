using Shelfy.Core.Domain.Entities;

namespace Shelfy.Core.Ports.System;

/// <summary>
/// Item を起動する機能のインターフェース
/// </summary>
public interface IItemLauncher
{
    /// <summary>
    /// 指定したItemを起動する
    /// </summary>
    /// <param name="item">起動するItem</param>
    /// <returns>起動が成功したかどうか</returns>
    Task<bool> LaunchAsync(Item item);

    /// <summary>
    /// 親フォルダを開く（File/Folder の場合）
    /// </summary>
    Task<bool> OpenParentFolderAsync(Item item);
}
