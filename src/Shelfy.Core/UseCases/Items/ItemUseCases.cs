using Shelfy.Core.UseCases.Launch;
using Shelfy.Core.UseCases.Search;

namespace Shelfy.Core.UseCases.Items;

/// <summary>
/// Item 関連 UseCase のファサード
/// </summary>
public class ItemUseCases(
    AddItemUseCase add,
    RemoveItemUseCase remove,
    RenameItemUseCase rename,
    UpdateItemMemoUseCase updateMemo,
    MoveItemToShelfUseCase moveToShelf,
    ReorderItemsUseCase reorder,
    LaunchItemUseCase launch,
    OpenParentFolderUseCase openParentFolder,
    SearchItemsUseCase search,
    GetRecentItemsUseCase getRecent,
    GetMissingItemsUseCase getMissing)
{
    public AddItemUseCase Add { get; } = add;
    public RemoveItemUseCase Remove { get; } = remove;
    public RenameItemUseCase Rename { get; } = rename;
    public UpdateItemMemoUseCase UpdateMemo { get; } = updateMemo;
    public MoveItemToShelfUseCase MoveToShelf { get; } = moveToShelf;
    public ReorderItemsUseCase Reorder { get; } = reorder;
    public LaunchItemUseCase Launch { get; } = launch;
    public OpenParentFolderUseCase OpenParentFolder { get; } = openParentFolder;
    public SearchItemsUseCase Search { get; } = search;
    public GetRecentItemsUseCase GetRecent { get; } = getRecent;
    public GetMissingItemsUseCase GetMissing { get; } = getMissing;
}
