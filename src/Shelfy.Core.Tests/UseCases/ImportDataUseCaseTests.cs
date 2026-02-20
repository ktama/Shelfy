using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.DataTransfer;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class ImportDataUseCaseTests
{
    private readonly FakeShelfRepository _shelfRepo = new();
    private readonly FakeItemRepository _itemRepo = new();

    private ImportDataUseCase CreateUseCase() => new(_shelfRepo, _itemRepo);

    [Fact]
    public async Task ImportData_EmptyData_ReturnsSuccess()
    {
        var useCase = CreateUseCase();
        var data = new ExportData
        {
            ExportedAt = DateTime.UtcNow,
            Shelves = [],
            Items = []
        };

        var result = await useCase.ExecuteAsync(data);

        Assert.IsType<ImportDataResult.Success>(result);
    }

    [Fact]
    public async Task ImportData_WithShelfAndItem_ImportsSuccessfully()
    {
        var shelfId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var useCase = CreateUseCase();

        var data = new ExportData
        {
            ExportedAt = DateTime.UtcNow,
            Shelves =
            [
                new ShelfData
                {
                    Id = shelfId.ToString(),
                    Name = "Imported Shelf",
                    SortOrder = 0,
                    IsPinned = false
                }
            ],
            Items =
            [
                new ItemData
                {
                    Id = itemId.ToString(),
                    ShelfId = shelfId.ToString(),
                    Type = (int)ItemType.File,
                    Target = @"C:\imported.txt",
                    DisplayName = "Imported File",
                    CreatedAt = DateTime.UtcNow.ToString("O")
                }
            ]
        };

        var result = await useCase.ExecuteAsync(data);

        var success = Assert.IsType<ImportDataResult.Success>(result);
        Assert.Equal(1, success.ShelvesImported);
        Assert.Equal(1, success.ItemsImported);

        // リポジトリにデータが存在するか確認
        var shelf = await _shelfRepo.GetByIdAsync(new ShelfId(shelfId));
        Assert.NotNull(shelf);
        Assert.Equal("Imported Shelf", shelf!.Name);

        var item = await _itemRepo.GetByIdAsync(new ItemId(itemId));
        Assert.NotNull(item);
        Assert.Equal("Imported File", item!.DisplayName);
    }

    [Fact]
    public async Task ImportData_ReplaceAll_ClearsExistingData()
    {
        // 既存データを準備
        var existingShelf = new Shelf(new ShelfId(Guid.NewGuid()), "Existing Shelf");
        await _shelfRepo.AddAsync(existingShelf);

        var existingItem = new Item(
            new ItemId(Guid.NewGuid()),
            existingShelf.Id,
            ItemType.File,
            @"C:\existing.txt",
            "Existing File",
            DateTime.UtcNow);
        await _itemRepo.AddAsync(existingItem);

        var newShelfId = Guid.NewGuid();
        var useCase = CreateUseCase();

        var data = new ExportData
        {
            ExportedAt = DateTime.UtcNow,
            Shelves =
            [
                new ShelfData
                {
                    Id = newShelfId.ToString(),
                    Name = "New Shelf",
                    SortOrder = 0,
                    IsPinned = true
                }
            ],
            Items = []
        };

        var result = await useCase.ExecuteAsync(data, replaceAll: true);

        var success = Assert.IsType<ImportDataResult.Success>(result);

        // 既存の Shelf が削除されているか確認
        var oldShelf = await _shelfRepo.GetByIdAsync(existingShelf.Id);
        Assert.Null(oldShelf);

        // 新しい Shelf が追加されているか確認
        var newShelf = await _shelfRepo.GetByIdAsync(new ShelfId(newShelfId));
        Assert.NotNull(newShelf);
        Assert.Equal("New Shelf", newShelf!.Name);
        Assert.True(newShelf.IsPinned);
    }

    [Fact]
    public async Task ImportData_DuplicateShelf_SkipsDuplicate()
    {
        var shelfId = Guid.NewGuid();

        // 既存データを準備
        var existingShelf = new Shelf(new ShelfId(shelfId), "Existing Shelf");
        await _shelfRepo.AddAsync(existingShelf);

        var useCase = CreateUseCase();

        var data = new ExportData
        {
            ExportedAt = DateTime.UtcNow,
            Shelves =
            [
                new ShelfData
                {
                    Id = shelfId.ToString(),
                    Name = "Duplicate Shelf",
                    SortOrder = 0,
                    IsPinned = false
                }
            ],
            Items = []
        };

        var result = await useCase.ExecuteAsync(data);

        var success = Assert.IsType<ImportDataResult.Success>(result);

        // 既存データが変更されていないこと
        var shelf = await _shelfRepo.GetByIdAsync(new ShelfId(shelfId));
        Assert.NotNull(shelf);
        Assert.Equal("Existing Shelf", shelf!.Name);
    }

    [Fact]
    public async Task ImportData_WithParentChild_ImportsInCorrectOrder()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var useCase = CreateUseCase();

        var data = new ExportData
        {
            ExportedAt = DateTime.UtcNow,
            Shelves =
            [
                // 子を先に定義（順序依存でないことを確認）
                new ShelfData
                {
                    Id = childId.ToString(),
                    Name = "Child Shelf",
                    ParentId = parentId.ToString(),
                    SortOrder = 0,
                    IsPinned = false
                },
                new ShelfData
                {
                    Id = parentId.ToString(),
                    Name = "Parent Shelf",
                    SortOrder = 0,
                    IsPinned = false
                }
            ],
            Items = []
        };

        var result = await useCase.ExecuteAsync(data);

        var success = Assert.IsType<ImportDataResult.Success>(result);

        var parent = await _shelfRepo.GetByIdAsync(new ShelfId(parentId));
        Assert.NotNull(parent);

        var child = await _shelfRepo.GetByIdAsync(new ShelfId(childId));
        Assert.NotNull(child);
        Assert.Equal(new ShelfId(parentId), child!.ParentId);
    }
}
