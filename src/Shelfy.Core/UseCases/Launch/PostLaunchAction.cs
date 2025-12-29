namespace Shelfy.Core.UseCases.Launch;

/// <summary>
/// 起動後のアクション
/// </summary>
public enum PostLaunchAction
{
    /// <summary>
    /// ウィンドウを非表示にする
    /// </summary>
    HideWindow,

    /// <summary>
    /// ウィンドウを維持する（ホットキー押下中）
    /// </summary>
    KeepWindow
}
