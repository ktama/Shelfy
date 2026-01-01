namespace Shelfy.Core.Ports.Persistence;

/// <summary>
/// アプリケーション設定の永続化インターフェース
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// 設定値を取得する
    /// </summary>
    /// <param name="key">設定キー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>設定値。存在しない場合は null</returns>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定値を保存する
    /// </summary>
    /// <param name="key">設定キー</param>
    /// <param name="value">設定値</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定値を削除する
    /// </summary>
    /// <param name="key">設定キー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// すべての設定を取得する
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>キーと値のペアのリスト</returns>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 設定キーの定義
/// </summary>
public static class SettingKeys
{
    /// <summary>
    /// グローバルホットキー（例: "Ctrl+Space"）
    /// </summary>
    public const string GlobalHotkey = "GlobalHotkey";

    /// <summary>
    /// ウィンドウの幅
    /// </summary>
    public const string WindowWidth = "WindowWidth";

    /// <summary>
    /// ウィンドウの高さ
    /// </summary>
    public const string WindowHeight = "WindowHeight";

    /// <summary>
    /// 起動時に非表示で開始するかどうか
    /// </summary>
    public const string StartMinimized = "StartMinimized";

    /// <summary>
    /// 最近使った項目の表示数
    /// </summary>
    public const string RecentItemsCount = "RecentItemsCount";
}
