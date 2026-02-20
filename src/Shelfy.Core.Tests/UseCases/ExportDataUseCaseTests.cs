using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Tests.Helpers;
using Shelfy.Core.UseCases.DataTransfer;
using Xunit;

namespace Shelfy.Core.Tests.UseCases;

public class ExportDataUseCaseTests
{
    private readonly FakeShelfRepository _shelfRepo = new();
    private readonly FakeItemRepository _itemRepo = new();
    private readonly FakeClock _clock = new();

    private ExportDataUseCase CreateUseCase() => new(_shelfRepo, _itemRepo, _clock);

    [Fact]
    public async Task ExportData_EmptyDatabase_ReturnsEmptyData()
    {
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync();

        var success = Assert.IsType<ExportDataResult.Success>(result);
        Assert.Empty(success.Data.Shelves);
        Assert.Empty(success.Data.Items);
        Assert.Equal("1.0", success.Data.Version);
    }

    [Fact]
    public async Task ExportData_WithShelvesAndItems_ReturnsAllData()
    {
        var shelf = new Shelf(new ShelfId(Guid.NewGuid()), "Test Shelf");
        await _shelfRepo.AddAsync(shelf);

        var item = new Item(
            new ItemId(Guid.NewGuid()),
            shelf.Id,
            ItemType.File,
            @"C:\test.txt",
            "Test File",
            DateTime.UtcNow,
            "A memo");
        await _itemRepo.AddAsync(item);

        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync();

        var success = Assert.IsType<ExportDataResult.Success>(result);
        Assert.Single(success.Data.Shelves);
        Assert.Single(success.Data.Items);
        Assert.Equal(shelf.Id.Value.ToString(), success.Data.Shelves[0].Id);
        Assert.Equal(item.Id.Value.ToString(), success.Data.Items[0].Id);
        Assert.Equal("A memo", success.Data.Items[0].Memo);
    }
}
