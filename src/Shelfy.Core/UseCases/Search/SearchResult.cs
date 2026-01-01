using Shelfy.Core.Domain.Entities;

namespace Shelfy.Core.UseCases.Search;

/// <summary>
/// 検索結果
/// </summary>
public abstract record SearchResult
{
    private SearchResult() { }

    public sealed record Success(IReadOnlyList<SearchResultItem> Items) : SearchResult;
    public sealed record Error(string Message) : SearchResult;
}

/// <summary>
/// 検索結果のアイテム（Shelf 情報付き）
/// </summary>
public record SearchResultItem(Item Item, string ShelfName);
