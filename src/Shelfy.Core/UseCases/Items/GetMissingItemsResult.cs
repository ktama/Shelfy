using Shelfy.Core.Domain.Entities;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// 欠損アイテム取得結果
/// </summary>
public abstract record GetMissingItemsResult
{
    private GetMissingItemsResult() { }

    public sealed record Success(IReadOnlyList<MissingItemInfo> Items) : GetMissingItemsResult;
    public sealed record Error(string Message) : GetMissingItemsResult;
}

/// <summary>
/// 欠損アイテム情報（Shelf 名付き）
/// </summary>
public record MissingItemInfo(Item Item, string ShelfName);
