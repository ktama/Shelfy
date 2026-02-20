namespace Shelfy.Core.UseCases.Shelves;

/// <summary>
/// Shelf 関連 UseCase のファサード
/// </summary>
public class ShelfUseCases(
    CreateShelfUseCase create,
    RenameShelfUseCase rename,
    DeleteShelfUseCase delete,
    TogglePinShelfUseCase togglePin,
    MoveShelfUseCase move,
    ReorderShelvesUseCase reorder)
{
    public CreateShelfUseCase Create { get; } = create;
    public RenameShelfUseCase Rename { get; } = rename;
    public DeleteShelfUseCase Delete { get; } = delete;
    public TogglePinShelfUseCase TogglePin { get; } = togglePin;
    public MoveShelfUseCase Move { get; } = move;
    public ReorderShelvesUseCase Reorder { get; } = reorder;
}
