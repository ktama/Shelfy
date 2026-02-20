namespace Shelfy.Core.UseCases.Search;

/// <summary>
/// 検索クエリを解析した結果
/// </summary>
public record SearchQuery
{
    /// <summary>
    /// 自由検索テキスト
    /// </summary>
    public string FreeText { get; init; } = string.Empty;

    /// <summary>
    /// Shelf 名でフィルタ（box: プレフィックス）
    /// </summary>
    public string? ShelfFilter { get; init; }

    /// <summary>
    /// ItemType でフィルタ（type: プレフィックス）
    /// </summary>
    public string? TypeFilter { get; init; }

    /// <summary>
    /// 特定の Shelf 内で検索（in: プレフィックス）
    /// </summary>
    public string? InShelfFilter { get; init; }

    /// <summary>
    /// クエリ文字列を解析する
    /// </summary>
    public static SearchQuery Parse(string? queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return new SearchQuery();

        var freeTextParts = new List<string>();
        string? shelfFilter = null;
        string? typeFilter = null;
        string? inShelfFilter = null;

        var tokens = queryText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (token.StartsWith("box:", StringComparison.OrdinalIgnoreCase))
            {
                var value = token[4..].Trim();
                if (value.Length > 0) shelfFilter = value;
            }
            else if (token.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            {
                var value = token[5..].Trim();
                if (value.Length > 0) typeFilter = value;
            }
            else if (token.StartsWith("in:", StringComparison.OrdinalIgnoreCase))
            {
                var value = token[3..].Trim();
                if (value.Length > 0) inShelfFilter = value;
            }
            else
            {
                freeTextParts.Add(token);
            }
        }

        return new SearchQuery
        {
            FreeText = string.Join(" ", freeTextParts),
            ShelfFilter = shelfFilter,
            TypeFilter = typeFilter,
            InShelfFilter = inShelfFilter
        };
    }
}
