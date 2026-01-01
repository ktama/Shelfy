using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;
using Shelfy.Core.UseCases.Items;
using Shelfy.Core.UseCases.Launch;
using Shelfy.Core.UseCases.Search;
using Shelfy.Core.UseCases.Shelves;

using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

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
public partial class MainViewModel : ObservableObject
{
    private readonly IShelfRepository _shelfRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IExistenceChecker _existenceChecker;
    private readonly CreateShelfUseCase _createShelfUseCase;
    private readonly RenameShelfUseCase _renameShelfUseCase;
    private readonly DeleteShelfUseCase _deleteShelfUseCase;
    private readonly TogglePinShelfUseCase _togglePinShelfUseCase;
    private readonly AddItemUseCase _addItemUseCase;
    private readonly RemoveItemUseCase _removeItemUseCase;
    private readonly RenameItemUseCase _renameItemUseCase;
    private readonly LaunchItemUseCase _launchItemUseCase;
    private readonly SearchItemsUseCase _searchItemsUseCase;
    private readonly GetRecentItemsUseCase _getRecentItemsUseCase;
    private readonly GetMissingItemsUseCase _getMissingItemsUseCase;

    private System.Timers.Timer? _searchDebounceTimer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateChildShelfCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteShelfCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameShelfCommand))]
    [NotifyCanExecuteChangedFor(nameof(TogglePinShelfCommand))]
    private ShelfViewModel? _selectedShelf;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenParentFolderCommand))]
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

    public MainViewModel(
        IShelfRepository shelfRepository,
        IItemRepository itemRepository,
        IExistenceChecker existenceChecker,
        CreateShelfUseCase createShelfUseCase,
        RenameShelfUseCase renameShelfUseCase,
        DeleteShelfUseCase deleteShelfUseCase,
        TogglePinShelfUseCase togglePinShelfUseCase,
        AddItemUseCase addItemUseCase,
        RemoveItemUseCase removeItemUseCase,
        RenameItemUseCase renameItemUseCase,
        LaunchItemUseCase launchItemUseCase,
        SearchItemsUseCase searchItemsUseCase,
        GetRecentItemsUseCase getRecentItemsUseCase,
        GetMissingItemsUseCase getMissingItemsUseCase)
    {
        _shelfRepository = shelfRepository;
        _itemRepository = itemRepository;
        _existenceChecker = existenceChecker;
        _createShelfUseCase = createShelfUseCase;
        _renameShelfUseCase = renameShelfUseCase;
        _deleteShelfUseCase = deleteShelfUseCase;
        _togglePinShelfUseCase = togglePinShelfUseCase;
        _addItemUseCase = addItemUseCase;
        _removeItemUseCase = removeItemUseCase;
        _renameItemUseCase = renameItemUseCase;
        _launchItemUseCase = launchItemUseCase;
        _searchItemsUseCase = searchItemsUseCase;
        _getRecentItemsUseCase = getRecentItemsUseCase;
        _getMissingItemsUseCase = getMissingItemsUseCase;
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
        var sorted = shelves.OrderByDescending(s => s.IsPinned).ThenBy(s => s.Name).ToList();
        shelves.ReplaceAll(sorted);
        foreach (var shelf in sorted)
        {
            SortShelves(shelf.Children);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // 検索テキスト変更時にデバウンス検索を実行
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Dispose();

        if (string.IsNullOrWhiteSpace(value))
        {
            // 検索テキストが空の場合は通常モードに戻る
            CurrentViewMode = ViewMode.Normal;
            ViewModeTitle = "Items";
            if (SelectedShelf is not null)
            {
                _ = LoadItemsForShelfAsync(SelectedShelf);
            }
            return;
        }

        _searchDebounceTimer = new System.Timers.Timer(300);
        _searchDebounceTimer.Elapsed += async (s, e) =>
        {
            _searchDebounceTimer?.Stop();
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await SearchAsync();
            });
        };
        _searchDebounceTimer.AutoReset = false;
        _searchDebounceTimer.Start();
    }

    partial void OnSelectedShelfChanged(ShelfViewModel? value)
    {
        // 通常モードの場合のみShelf変更時にアイテムを読み込む
        if (CurrentViewMode == ViewMode.Normal)
        {
            _ = LoadItemsForShelfAsync(value);
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
            .OrderBy(i => i.DisplayName)
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

            var result = await _searchItemsUseCase.ExecuteAsync(SearchText);

            CurrentItems.Clear();

            if (result is SearchResult.Success success)
            {
                var itemVMs = success.Items
                    .Select(r => new ItemViewModel(r.Item, _existenceChecker) { ShelfName = r.ShelfName });
                CurrentItems.AddRange(itemVMs);
                StatusMessage = $"Found {success.Items.Count} items";
            }
            else if (result is SearchResult.Error error)
            {
                StatusMessage = $"Search error: {error.Message}";
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

            var result = await _getRecentItemsUseCase.ExecuteAsync();

            CurrentItems.Clear();

            if (result is GetRecentItemsResult.Success success)
            {
                var itemVMs = success.Items
                    .Select(r => new ItemViewModel(r.Item, _existenceChecker) { ShelfName = r.ShelfName });
                CurrentItems.AddRange(itemVMs);
                StatusMessage = $"Showing {success.Items.Count} recent items";
            }
            else if (result is GetRecentItemsResult.Error error)
            {
                StatusMessage = $"Error: {error.Message}";
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

            var result = await _getMissingItemsUseCase.ExecuteAsync();

            CurrentItems.Clear();

            if (result is GetMissingItemsResult.Success success)
            {
                var itemVMs = success.Items
                    .Select(r => new ItemViewModel(r.Item, _existenceChecker) { ShelfName = r.ShelfName });
                CurrentItems.AddRange(itemVMs);
                StatusMessage = $"Found {success.Items.Count} missing items";
            }
            else if (result is GetMissingItemsResult.Error error)
            {
                StatusMessage = $"Error: {error.Message}";
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
        var name = await PromptForNameAsync("New Shelf", "Enter shelf name:");
        if (string.IsNullOrWhiteSpace(name)) return;

        var result = await _createShelfUseCase.ExecuteAsync(name);

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

        var name = await PromptForNameAsync("New Child Shelf", "Enter child shelf name:");
        if (string.IsNullOrWhiteSpace(name)) return;

        var result = await _createShelfUseCase.ExecuteAsync(name, SelectedShelf.Id);

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

        var result = await _deleteShelfUseCase.ExecuteAsync(SelectedShelf.Id);

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

        var newName = await PromptForNameAsync("Rename Shelf", "Enter new name:", SelectedShelf.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == SelectedShelf.Name) return;

        var result = await _renameShelfUseCase.ExecuteAsync(SelectedShelf.Id, newName);

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

        var result = await _togglePinShelfUseCase.ExecuteAsync(SelectedShelf.Id);

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

    #endregion

    #region Item Commands

    private bool CanLaunchItem() => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(CanLaunchItem))]
    private async Task LaunchItemAsync()
    {
        if (SelectedItem is null) return;

        var result = await _launchItemUseCase.ExecuteAsync(SelectedItem.Id);

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

        var result = await _removeItemUseCase.ExecuteAsync(SelectedItem.Id);

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

        var newName = await PromptForNameAsync("Rename Item", "Enter new display name:", SelectedItem.DisplayName);
        if (string.IsNullOrWhiteSpace(newName) || newName == SelectedItem.DisplayName) return;

        var result = await _renameItemUseCase.ExecuteAsync(SelectedItem.Id, newName);

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
    private void OpenParentFolder()
    {
        if (SelectedItem is null) return;

        var path = SelectedItem.Target;
        var parentFolder = SelectedItem.Type == ItemType.Folder
            ? path
            : System.IO.Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(parentFolder))
        {
            System.Diagnostics.Process.Start("explorer.exe", parentFolder);
            StatusMessage = $"Opened folder: {parentFolder}";
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

            var result = await _addItemUseCase.ExecuteAsync(
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

    #endregion

    #region Helper Methods

    /// <summary>
    /// ユーザーに名前入力を求める
    /// </summary>
    private Task<string?> PromptForNameAsync(string title, string prompt, string defaultValue = "")
    {
        var owner = Application.Current.MainWindow;
        var result = InputDialog.ShowDialog(owner, title, prompt, defaultValue);
        return Task.FromResult(result);
    }

    #endregion
}
