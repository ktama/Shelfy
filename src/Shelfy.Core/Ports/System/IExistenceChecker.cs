namespace Shelfy.Core.Ports.System;

/// <summary>
/// ファイル/フォルダ/URLの存在確認インターフェース
/// </summary>
public interface IExistenceChecker
{
    /// <summary>
    /// 指定したパスまたはURLが存在するかどうかを確認する
    /// </summary>
    bool Exists(string target);
}
