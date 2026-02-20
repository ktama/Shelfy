using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;

namespace Shelfy.Core.UseCases.DataTransfer;

/// <summary>
/// エクスポートデータを表すモデル
/// </summary>
public class ExportData
{
    public string Version { get; init; } = "1.0";
    public DateTime ExportedAt { get; init; }
    public IReadOnlyList<ShelfData> Shelves { get; init; } = [];
    public IReadOnlyList<ItemData> Items { get; init; } = [];
}

/// <summary>
/// Shelf のエクスポート用データ
/// </summary>
public class ShelfData
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ParentId { get; init; }
    public int SortOrder { get; init; }
    public bool IsPinned { get; init; }
}

/// <summary>
/// Item のエクスポート用データ
/// </summary>
public class ItemData
{
    public required string Id { get; init; }
    public required string ShelfId { get; init; }
    public required int Type { get; init; }
    public required string Target { get; init; }
    public required string DisplayName { get; init; }
    public string? Memo { get; init; }
    public int SortOrder { get; init; }
    public required string CreatedAt { get; init; }
    public string? LastAccessedAt { get; init; }
}

/// <summary>
/// 全データをエクスポートする UseCase
/// </summary>
public class ExportDataUseCase
{
    private readonly IShelfRepository _shelfRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IClock _clock;

    public ExportDataUseCase(
        IShelfRepository shelfRepository,
        IItemRepository itemRepository,
        IClock clock)
    {
        _shelfRepository = shelfRepository;
        _itemRepository = itemRepository;
        _clock = clock;
    }

    public async Task<ExportDataResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var shelves = await _shelfRepository.GetAllAsync(cancellationToken);
        var items = await _itemRepository.GetAllAsync(cancellationToken);

        var exportData = new ExportData
        {
            ExportedAt = _clock.UtcNow,
            Shelves = shelves.Select(s => new ShelfData
            {
                Id = s.Id.Value.ToString(),
                Name = s.Name,
                ParentId = s.ParentId?.Value.ToString(),
                SortOrder = s.SortOrder,
                IsPinned = s.IsPinned
            }).ToList(),
            Items = items.Select(i => new ItemData
            {
                Id = i.Id.Value.ToString(),
                ShelfId = i.ShelfId.Value.ToString(),
                Type = (int)i.Type,
                Target = i.Target,
                DisplayName = i.DisplayName,
                Memo = i.Memo,
                SortOrder = i.SortOrder,
                CreatedAt = i.CreatedAt.ToString("O"),
                LastAccessedAt = i.LastAccessedAt?.ToString("O")
            }).ToList()
        };

        return new ExportDataResult.Success(exportData);
    }
}

/// <summary>
/// ExportData ユースケースの実行結果
/// </summary>
public abstract record ExportDataResult
{
    public sealed record Success(ExportData Data) : ExportDataResult;
}
