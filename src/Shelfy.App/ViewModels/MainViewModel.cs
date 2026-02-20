using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;
using Shelfy.Core.UseCases.DataTransfer;
using Shelfy.Core.UseCases.Items;
using Shelfy.Core.UseCases.Launch;
using Shelfy.Core.UseCases.Search;
using Shelfy.Core.UseCases.Shelves;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Window = System.Windows.Window;

namespace Shelfy.App.ViewModels;

/// <summary>
/// 表示モード
/// </summary>
public enum ViewMode
{
    Normal,      // 通常（Shelf のアイテム表示）
    Search,      // 検索結果表示
    Recent,      // 最近使った表示
    Missing      // 欠損アイテム表示
}

/// <summary>
/// メインウィンドウの ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IShelfRepository _shelfRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IExistenceChecker _existenceChecker;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ShelfUseCases _shelfUseCases;
    private readonly ItemUseCases _itemUseCases;
    private readonly DataTransferUseCases _dataTransferUseCases;

    private CancellationTokenSource? _searchDebounceCts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateChildShelfCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteShelfCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameShelfCommand))]
    [NotifyCanExecuteChangedFor(nameof(TogglePinShelfCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveShelfCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveShelfUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveShelfDownCommand))]
    private ShelfViewModel? _selectedShelf;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditMemoCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveItemToShelfCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenParentFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveItemUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveItemDownCommand))]
    private ItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ViewMode _currentViewMode = ViewMode.Normal;

    [ObservableProperty]
    private string _viewModeTitle = "Items";

    /// <summary>
    /// ルートレベルの Shelf 一覧
    /// </summary>
    public ObservableCollectionEx<ShelfViewModel> RootShelves { get; } = [];

    /// <summary>
    /// 選択中 Shelf の Item 一覧（または検索結果など）
    /// </summary>
    public ObservableCollectionEx<ItemViewModel> CurrentItems { get; } = [];

    /// <summary>
    /// ウィンドウを非表示にする要求イベント
    /// </summary>
    public event Action? HideWindowRequested;

    /// <summary>
    /// ホットキーの再登録要求イベント（新しいホットキー文字列を渡す）
    /// </summary>
    public event Action<string>? ReregisterHotkeyRequested;

    public MainViewModel(
        IShelfRepository shelfRepository,
        IItemRepository itemRepository,
        IExistenceChecker existenceChecker,
        ISettingsRepository settingsRepository,
        ShelfUseCases shelfUseCases,
        ItemUseCases itemUseCases,
        DataTransferUseCases dataTransferUseCases)
    {
        _shelfRepository = shelfRepository;
        _itemRepository = itemRepository;
        _existenceChecker = existenceChecker;
        _settingsRepository = settingsRepository;
        _shelfUseCases = shelfUseCases;
        _itemUseCases = itemUseCases;
        _dataTransferUseCases = dataTransferUseCases;
    }

    /// <summary>
    /// 初期データをロードする
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading...";

            RootShelves.Clear();
            CurrentItems.Clear();

            var allShelves = await _shelfRepository.GetAllAsync(cancellationToken);
            var shelfVMs = allShelves
                .ToDictionary(s => s.Id, s => new ShelfViewModel(s));

            // ツリー構造を構築
            foreach (var shelf in allShelves)
            {
                var shelfVM = shelfVMs[shelf.Id];

                if (shelf.ParentId.HasValue && shelfVMs.TryGetValue(shelf.ParentId.Value, out var parentVM))
                {
                    parentVM.Children.Add(shelfVM);
                }
                else
                {
                    RootShelves.Add(shelfVM);
                }
            }

            // ソート
            SortShelves(RootShelves);

            StatusMessage = $"Loaded {allShelves.Count} shelves.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SortShelves(ObservableCollectionEx<ShelfViewModel> shelves)
    {
        var sorted = shelves.OrderByDescending(s => s.IsPinned).ThenBy(s => s.SortOrder).ThenBy(s => s.Name).ToList();
        shelves.ReplaceAll(sorted);
        foreach (var shelf in sorted)
        {
            SortShelves(shelf.Children);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // 検索テキスト変更時にデバウンス検索を実行
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            // 検索テキストが空の場合は通常モードに戻る
            CurrentViewMode = ViewMode.Normal;
            ViewModeTitle = "Items";
            if (SelectedShelf is not null)
            {
                _ = LoadItemsForShelfSafeAsync(SelectedShelf);
            }
            return;
        }

        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        _ = DebounceSearchAsync(cts.Token);
    }

    private async Task DebounceSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(300, cancellationToken);
            await SearchAsync();
        }
        catch (OperationCanceledException)
        {
            // デバウンスキャンセルは正常
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search error: {ex.Message}";
        }
    }

    partial void OnSelectedShelfChanged(ShelfViewModel? value)
    {
        // 通常モードの場合のみShelf変更時にアイテムを読み込む
        if (CurrentViewMode == ViewMode.Normal)
        {
            _ = LoadItemsForShelfSafeAsync(value);
        }
    }

    /// <summary>
    /// 例外を握りつぶさない安全な fire-and-forget ラッパー
    /// </summary>
    private async Task LoadItemsForShelfSafeAsync(ShelfViewModel? shelfVM)
    {
        try
        {
            await LoadItemsForShelfAsync(shelfVM);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading items: {ex.Message}";
        }
    }

    private async Task LoadItemsForShelfAsync(ShelfViewModel? shelfVM)
    {
        CurrentViewMode = ViewMode.Normal;
        ViewModeTitle = shelfVM?.Name ?? "Items";
        CurrentItems.Clear();

        if (shelfVM is null) return;

        var items = await _itemRepository.GetByShelfIdAsync(shelfVM.Id);
        var itemVMs = items
            .Select(i => new ItemViewModel(i, _existenceChecker));

        CurrentItems.AddRange(itemVMs);
    }

    #region Search & View Mode Commands

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        try
        {
            IsLoading = true;
            CurrentViewMode = ViewMode.Search;
            ViewModeTitle = $"Search: {SearchText}";

            var result = await _itemUseCases.Search.ExecuteAsync(SearchText);

            CurrentItems.Clear();

            if (result is SearchResult.Success success)
            {
                var itemVMs = success.Items
                    .Select(r => new ItemViewModel(r.Item, _existenceChecker) { ShelfName = r.ShelfName });
                CurrentItems.AddRange(itemVMs);
                StatusMessage = $"Found {success.Items.Count} items";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ShowRecentAsync()
    {
        try
        {
            IsLoading = true;
            CurrentViewMode = ViewMode.Recent;
            ViewModeTitle = "Recent Items";
            SearchText = string.Empty;

            // 設定から表示件数を取得
            var recentCountStr = await _settingsRepository.GetAsync(SettingKeys.RecentItemsCount);
            int? recentCount = int.TryParse(recentCountStr, out var rc) ? rc : null;

            var result = await _itemUseCases.GetRecent.ExecuteAsync(recentCount);

            CurrentItems.Clear();

            if (result is GetRecentItemsResult.Success success)
            {
                var itemVMs = success.Items
                    .Select(r => new ItemViewModel(r.Item, _existenceChecker) { ShelfName = r.ShelfName });
                CurrentItems.AddRange(itemVMs);
                StatusMessage = $"Showing {success.Items.Count} recent items";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ShowMissingAsync()
    {
        try
        {
            IsLoading = true;
            CurrentViewMode = ViewMode.Missing;
            ViewModeTitle = "Missing Items";
            SearchText = string.Empty;

            var result = await _itemUseCases.GetMissing.ExecuteAsync();

            CurrentItems.Clear();

            if (result is GetMissingItemsResult.Success success)
            {
                var itemVMs = success.Items
                    .Select(r => new ItemViewModel(r.Item, _existenceChecker) { ShelfName = r.ShelfName });
                CurrentItems.AddRange(itemVMs);
                StatusMessage = $"Found {success.Items.Count} missing items";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (CurrentViewMode == ViewMode.Search && !string.IsNullOrWhiteSpace(SearchText))
        {
            await SearchAsync();
        }
        else if (CurrentViewMode == ViewMode.Recent)
        {
            await ShowRecentAsync();
        }
        else if (CurrentViewMode == ViewMode.Missing)
        {
            await ShowMissingAsync();
        }
        else if (SelectedShelf is not null)
        {
            await LoadItemsForShelfAsync(SelectedShelf);
        }

        // 存在確認を再実行
        foreach (var item in CurrentItems)
        {
            item.RefreshExistence();
        }

        StatusMessage = "Refreshed";
    }

    #endregion

    #region Shelf Commands

    [RelayCommand]
    private async Task CreateShelfAsync()
    {
        var name = PromptForName("New Shelf", "Enter shelf name:");
        if (string.IsNullOrWhiteSpace(name)) return;

        var result = await _shelfUseCases.Create.ExecuteAsync(name);

        if (result is CreateShelfResult.Success success)
        {
            var shelfVM = new ShelfViewModel(success.Shelf);
            RootShelves.Add(shelfVM);
            SelectedShelf = shelfVM;
            StatusMessage = $"Created shelf: {name}";
        }
        else
        {
            StatusMessage = $"Failed to create shelf: {result}";
        }
    }

    private bool CanCreateChildShelf() => SelectedShelf is not null;

    [RelayCommand(CanExecute = nameof(CanCreateChildShelf))]
    private async Task CreateChildShelfAsync()
    {
        if (SelectedShelf is null) return;

        var name = PromptForName("New Child Shelf", "Enter child shelf name:");
        if (string.IsNullOrWhiteSpace(name)) return;

        var result = await _shelfUseCases.Create.ExecuteAsync(name, SelectedShelf.Id);

        if (result is CreateShelfResult.Success success)
        {
            var shelfVM = new ShelfViewModel(success.Shelf);
            SelectedShelf.Children.Add(shelfVM);
            SelectedShelf.IsExpanded = true;
            StatusMessage = $"Created child shelf: {name}";
        }
        else
        {
            StatusMessage = $"Failed to create child shelf: {result}";
        }
    }

    private bool CanDeleteShelf() => SelectedShelf is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteShelf))]
    private async Task DeleteShelfAsync()
    {
        if (SelectedShelf is null) return;

        var confirm = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedShelf.Name}' and all its contents?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var result = await _shelfUseCases.Delete.ExecuteAsync(SelectedShelf.Id);

        if (result is DeleteShelfResult.Success)
        {
            var shelfToRemove = SelectedShelf;
            SelectedShelf = null;

            // 親から削除
            if (TryFindParentCollection(shelfToRemove, out var parentCollection))
            {
                parentCollection.Remove(shelfToRemove);
            }
            else
            {
                RootShelves.Remove(shelfToRemove);
            }

            StatusMessage = $"Deleted shelf: {shelfToRemove.Name}";
        }
        else
        {
            StatusMessage = $"Failed to delete shelf: {result}";
        }
    }

    private bool TryFindParentCollection(ShelfViewModel shelf, out ObservableCollectionEx<ShelfViewModel> parentCollection)
    {
        parentCollection = null!;
        return TryFindParentCollectionRecursive(shelf, RootShelves, out parentCollection);
    }

    private bool TryFindParentCollectionRecursive(
        ShelfViewModel target,
        ObservableCollectionEx<ShelfViewModel> shelves,
        out ObservableCollectionEx<ShelfViewModel> parentCollection)
    {
        foreach (var shelf in shelves)
        {
            if (shelf.Children.Contains(target))
            {
                parentCollection = shelf.Children;
                return true;
            }

            if (TryFindParentCollectionRecursive(target, shelf.Children, out parentCollection))
            {
                return true;
            }
        }

        parentCollection = null!;
        return false;
    }

    private bool CanRenameShelf() => SelectedShelf is not null;

    [RelayCommand(CanExecute = nameof(CanRenameShelf))]
    private async Task RenameShelfAsync()
    {
        if (SelectedShelf is null) return;

        var newName = PromptForName("Rename Shelf", "Enter new name:", SelectedShelf.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == SelectedShelf.Name) return;

        var result = await _shelfUseCases.Rename.ExecuteAsync(SelectedShelf.Id, newName);

        if (result is RenameShelfResult.Success success)
        {
            SelectedShelf.Update(success.Shelf);
            StatusMessage = $"Renamed shelf to: {newName}";
        }
        else
        {
            StatusMessage = $"Failed to rename shelf: {result}";
        }
    }

    private bool CanTogglePinShelf() => SelectedShelf is not null;

    [RelayCommand(CanExecute = nameof(CanTogglePinShelf))]
    private async Task TogglePinShelfAsync()
    {
        if (SelectedShelf is null) return;

        var result = await _shelfUseCases.TogglePin.ExecuteAsync(SelectedShelf.Id);

        if (result is TogglePinShelfResult.Success success)
        {
            SelectedShelf.Update(success.Shelf);

            // 再ソート（ピン留め状態が変わったため）
            if (TryFindParentCollection(SelectedShelf, out var parentCollection))
            {
                SortShelves(parentCollection);
            }
            else
            {
                SortShelves(RootShelves);
            }

            var action = success.Shelf.IsPinned ? "Pinned" : "Unpinned";
            StatusMessage = $"{action}: {success.Shelf.Name}";
        }
        else
        {
            StatusMessage = $"Failed to toggle pin: {result}";
        }
    }

    private bool CanMoveShelf() => SelectedShelf is not null;

    [RelayCommand(CanExecute = nameof(CanMoveShelf))]
    private async Task MoveShelfAsync()
    {
        if (SelectedShelf is null) return;

        var owner = Application.Current.MainWindow;
        var (confirmed, targetShelfId, isRoot) = ShelfPickerDialog.ShowPickerDialog(
            owner,
            "Move Shelf",
            RootShelves,
            SelectedShelf.Id);

        if (!confirmed) return;

        ShelfId? newParentId = isRoot ? null : targetShelfId;

        var result = await _shelfUseCases.Move.ExecuteAsync(SelectedShelf.Id, newParentId);

        if (result is MoveShelfResult.Success success)
        {
            // ツリーから削除して新しい場所に追加
            var shelfToMove = SelectedShelf;
            RemoveShelfFromTree(shelfToMove);

            if (newParentId is null)
            {
                RootShelves.Add(shelfToMove);
                SortShelves(RootShelves);
            }
            else
            {
                var parentVM = FindShelfViewModel(newParentId.Value, RootShelves);
                if (parentVM is not null)
                {
                    parentVM.Children.Add(shelfToMove);
                    parentVM.IsExpanded = true;
                    SortShelves(parentVM.Children);
                }
            }

            StatusMessage = $"Moved shelf: {success.Shelf.Name}";
        }
        else if (result is MoveShelfResult.InvalidMove invalid)
        {
            StatusMessage = invalid.Message;
        }
        else
        {
            StatusMessage = $"Failed to move shelf: {result}";
        }
    }

    private bool CanMoveShelfUp() => SelectedShelf is not null;

    [RelayCommand(CanExecute = nameof(CanMoveShelfUp))]
    private async Task MoveShelfUpAsync()
    {
        if (SelectedShelf is null) return;
        await ReorderShelfAsync(SelectedShelf, -1);
    }

    private bool CanMoveShelfDown() => SelectedShelf is not null;

    [RelayCommand(CanExecute = nameof(CanMoveShelfDown))]
    private async Task MoveShelfDownAsync()
    {
        if (SelectedShelf is null) return;
        await ReorderShelfAsync(SelectedShelf, +1);
    }

    private async Task ReorderShelfAsync(ShelfViewModel shelf, int direction)
    {
        var siblings = TryFindParentCollection(shelf, out var parentCollection)
            ? parentCollection
            : RootShelves;

        var currentIndex = siblings.IndexOf(shelf);
        var newIndex = currentIndex + direction;

        if (newIndex < 0 || newIndex >= siblings.Count) return;

        // ローカルで入れ替え
        siblings.Move(currentIndex, newIndex);

        // 新しい順序をUseCaseに送信
        var orderedIds = siblings.Select(s => s.Id).ToList();
        var result = await _shelfUseCases.Reorder.ExecuteAsync(orderedIds);

        if (result is ReorderShelvesResult.Success)
        {
            StatusMessage = $"Reordered: {shelf.Name}";
        }
        else
        {
            // 失敗時は元に戻す
            siblings.Move(newIndex, currentIndex);
            StatusMessage = $"Failed to reorder: {result}";
        }
    }

    private void RemoveShelfFromTree(ShelfViewModel shelf)
    {
        if (TryFindParentCollection(shelf, out var parentCollection))
        {
            parentCollection.Remove(shelf);
        }
        else
        {
            RootShelves.Remove(shelf);
        }
    }

    private ShelfViewModel? FindShelfViewModel(ShelfId id, IEnumerable<ShelfViewModel> shelves)
    {
        foreach (var shelf in shelves)
        {
            if (shelf.Id == id) return shelf;

            var found = FindShelfViewModel(id, shelf.Children);
            if (found is not null) return found;
        }
        return null;
    }

    #endregion

    #region Item Commands

    private bool CanLaunchItem() => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(CanLaunchItem))]
    private async Task LaunchItemAsync()
    {
        if (SelectedItem is null) return;

        var result = await _itemUseCases.Launch.ExecuteAsync(SelectedItem.Id);

        if (result is LaunchItemResult.Success success)
        {
            StatusMessage = $"Launched: {SelectedItem.DisplayName}";

            if (success.PostAction == PostLaunchAction.HideWindow)
            {
                HideWindowRequested?.Invoke();
            }
        }
        else
        {
            StatusMessage = $"Failed to launch: {result}";
        }
    }

    private bool CanRemoveItem() => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveItem))]
    private async Task RemoveItemAsync()
    {
        if (SelectedItem is null) return;

        var confirm = MessageBox.Show(
            $"Remove '{SelectedItem.DisplayName}' from this shelf?",
            "Confirm Remove",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var result = await _itemUseCases.Remove.ExecuteAsync(SelectedItem.Id);

        if (result is RemoveItemResult.Success)
        {
            var itemToRemove = SelectedItem;
            SelectedItem = null;
            CurrentItems.Remove(itemToRemove);
            StatusMessage = $"Removed: {itemToRemove.DisplayName}";
        }
        else
        {
            StatusMessage = $"Failed to remove item: {result}";
        }
    }

    private bool CanRenameItem() => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(CanRenameItem))]
    private async Task RenameItemAsync()
    {
        if (SelectedItem is null) return;

        var newName = PromptForName("Rename Item", "Enter new display name:", SelectedItem.DisplayName);
        if (string.IsNullOrWhiteSpace(newName) || newName == SelectedItem.DisplayName) return;

        var result = await _itemUseCases.Rename.ExecuteAsync(SelectedItem.Id, newName);

        if (result is RenameItemResult.Success success)
        {
            SelectedItem.Update(success.Item);
            StatusMessage = $"Renamed to: {newName}";
        }
        else
        {
            StatusMessage = $"Failed to rename item: {result}";
        }
    }

    private bool CanOpenParentFolder() =>
        SelectedItem is not null &&
        SelectedItem.Type is ItemType.File or ItemType.Folder;

    [RelayCommand(CanExecute = nameof(CanOpenParentFolder))]
    private async Task OpenParentFolderAsync()
    {
        if (SelectedItem is null) return;

        var result = await _itemUseCases.OpenParentFolder.ExecuteAsync(SelectedItem.Id);

        if (result is OpenParentFolderResult.Success)
        {
            StatusMessage = $"Opened parent folder: {SelectedItem.DisplayName}";
        }
        else if (result is OpenParentFolderResult.NotSupported notSupported)
        {
            StatusMessage = notSupported.Message;
        }
        else
        {
            StatusMessage = $"Failed to open parent folder: {result}";
        }
    }

    private bool CanEditMemo() => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(CanEditMemo))]
    private async Task EditMemoAsync()
    {
        if (SelectedItem is null) return;

        var owner = Application.Current.MainWindow;
        var (confirmed, memo) = MemoEditDialog.ShowMemoDialog(
            owner,
            "Edit Memo",
            $"Memo for: {SelectedItem.DisplayName}",
            SelectedItem.Memo);

        if (!confirmed) return;

        var result = await _itemUseCases.UpdateMemo.ExecuteAsync(SelectedItem.Id, memo);

        if (result is UpdateItemMemoResult.Success success)
        {
            SelectedItem.Update(success.Item);
            StatusMessage = memo is null ? "Memo cleared" : "Memo updated";
        }
        else
        {
            StatusMessage = $"Failed to update memo: {result}";
        }
    }

    private bool CanMoveItemToShelf() => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(CanMoveItemToShelf))]
    private async Task MoveItemToShelfAsync()
    {
        if (SelectedItem is null) return;

        var owner = Application.Current.MainWindow;
        var (confirmed, targetShelfId, _) = ShelfPickerDialog.ShowPickerDialog(
            owner,
            "Move Item to Shelf",
            RootShelves);

        if (!confirmed || targetShelfId is null) return;

        var result = await _itemUseCases.MoveToShelf.ExecuteAsync(SelectedItem.Id, targetShelfId.Value);

        if (result is MoveItemToShelfResult.Success success)
        {
            var itemToRemove = SelectedItem;
            SelectedItem = null;
            CurrentItems.Remove(itemToRemove);
            StatusMessage = $"Moved '{success.Item.DisplayName}' to another shelf";
        }
        else if (result is MoveItemToShelfResult.DuplicateItem dup)
        {
            StatusMessage = dup.Message;
        }
        else
        {
            StatusMessage = $"Failed to move item: {result}";
        }
    }

    private bool CanMoveItemUp() => SelectedItem is not null && CurrentViewMode == ViewMode.Normal;

    [RelayCommand(CanExecute = nameof(CanMoveItemUp))]
    private async Task MoveItemUpAsync()
    {
        if (SelectedItem is null) return;
        await ReorderItemAsync(SelectedItem, -1);
    }

    private bool CanMoveItemDown() => SelectedItem is not null && CurrentViewMode == ViewMode.Normal;

    [RelayCommand(CanExecute = nameof(CanMoveItemDown))]
    private async Task MoveItemDownAsync()
    {
        if (SelectedItem is null) return;
        await ReorderItemAsync(SelectedItem, +1);
    }

    private async Task ReorderItemAsync(ItemViewModel item, int direction)
    {
        var currentIndex = CurrentItems.IndexOf(item);
        var newIndex = currentIndex + direction;

        if (newIndex < 0 || newIndex >= CurrentItems.Count) return;

        // ローカルで入れ替え
        CurrentItems.Move(currentIndex, newIndex);

        // 新しい順序を UseCase に送信
        var orderedIds = CurrentItems.Select(i => i.Id).ToList();
        var result = await _itemUseCases.Reorder.ExecuteAsync(orderedIds);

        if (result is ReorderItemsResult.Success)
        {
            StatusMessage = $"Reordered: {item.DisplayName}";
        }
        else
        {
            // 失敗時は元に戻す
            CurrentItems.Move(newIndex, currentIndex);
            StatusMessage = $"Failed to reorder: {result}";
        }
    }

    [RelayCommand]
    private async Task AddItemAsync(string[]? filePaths)
    {
        if (SelectedShelf is null || filePaths is null || filePaths.Length == 0) return;

        foreach (var path in filePaths)
        {
            var type = DetermineItemType(path);
            var displayName = System.IO.Path.GetFileName(path);

            var result = await _itemUseCases.Add.ExecuteAsync(
                SelectedShelf.Id,
                type,
                path,
                displayName);

            if (result is AddItemResult.Success success)
            {
                var itemVM = new ItemViewModel(success.Item, _existenceChecker);
                CurrentItems.Add(itemVM);
                StatusMessage = $"Added: {displayName}";
            }
            else
            {
                StatusMessage = $"Failed to add {displayName}: {result}";
            }
        }
    }

    private ItemType DetermineItemType(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            return ItemType.Url;
        }

        if (System.IO.Directory.Exists(path))
        {
            return ItemType.Folder;
        }

        return ItemType.File;
    }

    [RelayCommand]
    private async Task AddUrlAsync()
    {
        if (SelectedShelf is null)
        {
            StatusMessage = "Please select a shelf first.";
            return;
        }

        var owner = Application.Current.MainWindow;
        var url = InputDialog.ShowDialog(owner, "Add URL", "Enter URL (e.g. https://example.com):");
        if (string.IsNullOrWhiteSpace(url)) return;

        // URL バリデーション
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            StatusMessage = "Invalid URL. Please enter a valid http/https URL.";
            return;
        }

        // 表示名を入力（デフォルトはホスト名）
        var defaultName = uri.Host;
        var displayName = InputDialog.ShowDialog(owner, "Display Name", "Enter display name:", defaultName);
        if (string.IsNullOrWhiteSpace(displayName)) displayName = defaultName;

        var result = await _itemUseCases.Add.ExecuteAsync(
            SelectedShelf.Id,
            ItemType.Url,
            url,
            displayName);

        if (result is AddItemResult.Success success)
        {
            var itemVM = new ItemViewModel(success.Item, _existenceChecker);
            CurrentItems.Add(itemVM);
            StatusMessage = $"Added URL: {displayName}";
        }
        else if (result is AddItemResult.DuplicateItem dup)
        {
            StatusMessage = dup.Message;
        }
        else
        {
            StatusMessage = $"Failed to add URL: {result}";
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// ユーザーに名前入力を求める
    /// </summary>
    private string? PromptForName(string title, string prompt, string defaultValue = "")
    {
        var owner = Application.Current.MainWindow;
        return InputDialog.ShowDialog(owner, title, prompt, defaultValue);
    }

    #endregion

    #region D&D Reorder Methods

    /// <summary>
    /// 現在表示中のアイテムリストの並び順を永続化する（D&D後に呼び出す）
    /// </summary>
    public async Task ReorderCurrentItemsAsync()
    {
        var orderedIds = CurrentItems.Select(i => i.Id).ToList();
        var result = await _itemUseCases.Reorder.ExecuteAsync(orderedIds);

        if (result is ReorderItemsResult.Success)
        {
            StatusMessage = "Items reordered";
        }
        else
        {
            StatusMessage = $"Failed to reorder items: {result}";
        }
    }

    /// <summary>
    /// Shelf を D&D で並び替える
    /// </summary>
    public async Task ReorderShelfByDropAsync(ShelfViewModel source, ShelfViewModel target, bool insertBefore)
    {
        // 同じ兄弟コレクション内のみ並び替えをサポート
        var sourceSiblings = TryFindParentCollection(source, out var sourceParent)
            ? sourceParent
            : RootShelves;

        var targetSiblings = TryFindParentCollection(target, out var targetParent)
            ? targetParent
            : RootShelves;

        if (sourceSiblings != targetSiblings)
        {
            StatusMessage = "Cannot reorder between different parent shelves. Use 'Move to...' instead.";
            return;
        }

        var sourceIndex = sourceSiblings.IndexOf(source);
        var targetIndex = sourceSiblings.IndexOf(target);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex) return;

        // 挿入位置を計算
        var newIndex = insertBefore ? targetIndex : targetIndex;
        if (sourceIndex < targetIndex && insertBefore)
            newIndex--;
        else if (sourceIndex > targetIndex && !insertBefore)
            newIndex++;

        if (newIndex < 0) newIndex = 0;
        if (newIndex >= sourceSiblings.Count) newIndex = sourceSiblings.Count - 1;
        if (newIndex == sourceIndex) return;

        // ローカルで移動
        sourceSiblings.Move(sourceIndex, newIndex);

        // UseCase で永続化
        var orderedIds = sourceSiblings.Select(s => s.Id).ToList();
        var result = await _shelfUseCases.Reorder.ExecuteAsync(orderedIds);

        if (result is ReorderShelvesResult.Success)
        {
            StatusMessage = $"Reordered: {source.Name}";
        }
        else
        {
            // 失敗時は元に戻す
            sourceSiblings.Move(newIndex, sourceIndex);
            StatusMessage = $"Failed to reorder: {result}";
        }
    }

    #endregion

    #region Export / Import / Settings Commands

    [RelayCommand]
    private async Task ExportDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Exporting...";

            var result = await _dataTransferUseCases.Export.ExecuteAsync();

            if (result is ExportDataResult.Success success)
            {
                var json = _dataTransferUseCases.Serializer.Serialize(success.Data);

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Shelfy Data",
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    FileName = $"shelfy_export_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog() == true)
                {
                    await System.IO.File.WriteAllTextAsync(dialog.FileName, json);
                    StatusMessage = $"Exported to: {dialog.FileName}";
                }
                else
                {
                    StatusMessage = "Export cancelled";
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ImportDataAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Shelfy Data",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true) return;

            var replaceConfirm = MessageBox.Show(
                "Do you want to replace all existing data?\n\n" +
                "Yes = Replace all data\n" +
                "No = Merge with existing data\n" +
                "Cancel = Abort import",
                "Import Mode",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (replaceConfirm == MessageBoxResult.Cancel) return;

            var replaceAll = replaceConfirm == MessageBoxResult.Yes;

            IsLoading = true;
            StatusMessage = "Importing...";

            var json = await System.IO.File.ReadAllTextAsync(dialog.FileName);
            var data = _dataTransferUseCases.Serializer.Deserialize(json);

            if (data is null)
            {
                StatusMessage = "Import error: Invalid data format";
                return;
            }

            var result = await _dataTransferUseCases.Import.ExecuteAsync(data, replaceAll);

            if (result is ImportDataResult.Success success)
            {
                StatusMessage = $"Imported {success.ShelvesImported} shelves and {success.ItemsImported} items";

                // ツリーをリロード
                RootShelves.Clear();
                CurrentItems.Clear();
                await LoadAsync();
            }
            else if (result is ImportDataResult.Error error)
            {
                StatusMessage = $"Import error: {error.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var owner = Application.Current.MainWindow;

        // 現在の設定を読み込み
        var currentHotkey = await _settingsRepository.GetAsync(SettingKeys.GlobalHotkey);
        var startMinimizedStr = await _settingsRepository.GetAsync(SettingKeys.StartMinimized);
        var recentCountStr = await _settingsRepository.GetAsync(SettingKeys.RecentItemsCount);
        var windowWidthStr = await _settingsRepository.GetAsync(SettingKeys.WindowWidth);
        var windowHeightStr = await _settingsRepository.GetAsync(SettingKeys.WindowHeight);

        var startMinimized = startMinimizedStr == "true";
        var recentCount = int.TryParse(recentCountStr, out var rc) ? rc : 20;
        var windowWidth = double.TryParse(windowWidthStr, out var ww) ? ww : 800;
        var windowHeight = double.TryParse(windowHeightStr, out var wh) ? wh : 500;

        var dialog = SettingsDialog.ShowSettingsDialog(
            owner,
            currentHotkey ?? "Ctrl+Shift+Space",
            startMinimized,
            recentCount,
            windowWidth,
            windowHeight);

        if (dialog is null) return;

        // 設定を保存
        await _settingsRepository.SetAsync(SettingKeys.GlobalHotkey, dialog.HotkeyText ?? "Ctrl+Shift+Space");
        await _settingsRepository.SetAsync(SettingKeys.StartMinimized, dialog.StartMinimizedValue.ToString().ToLower());
        await _settingsRepository.SetAsync(SettingKeys.RecentItemsCount, dialog.RecentItemsCountValue.ToString());
        await _settingsRepository.SetAsync(SettingKeys.WindowWidth, dialog.WindowWidthValue.ToString("0"));
        await _settingsRepository.SetAsync(SettingKeys.WindowHeight, dialog.WindowHeightValue.ToString("0"));

        // ウィンドウサイズを即時反映
        if (owner is not null)
        {
            owner.Width = dialog.WindowWidthValue;
            owner.Height = dialog.WindowHeightValue;
        }

        // ホットキーを即時反映
        var newHotkey = dialog.HotkeyText ?? "Ctrl+Shift+Space";
        ReregisterHotkeyRequested?.Invoke(newHotkey);

        StatusMessage = "Settings saved.";
    }

    /// <summary>
    /// ウィンドウサイズを保存する（ウィンドウ非表示時に呼び出される）
    /// </summary>
    public async Task SaveWindowSizeAsync(double width, double height)
    {
        if (width > 0 && height > 0)
        {
            await _settingsRepository.SetAsync(SettingKeys.WindowWidth, width.ToString("0"));
            await _settingsRepository.SetAsync(SettingKeys.WindowHeight, height.ToString("0"));
        }
    }

    [RelayCommand]
    private void Hide()
    {
        HideWindowRequested?.Invoke();
    }

    #endregion

    public void Dispose()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;
        GC.SuppressFinalize(this);
    }
}
