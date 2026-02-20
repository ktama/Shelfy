using Shelfy.Core.Domain.Entities;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// 最近使ったアイテム取得結果
/// </summary>
public abstract record GetRecentItemsResult
{
    private GetRecentItemsResult() { }

    public sealed record Success(IReadOnlyList<RecentItemInfo> Items) : GetRecentItemsResult;
}

/// <summary>
/// 最近使ったアイテム情報（Shelf 名付き）
/// </summary>
public record RecentItemInfo(Item Item, string ShelfName);
