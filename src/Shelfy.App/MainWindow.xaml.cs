using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shelfy.App.ViewModels;

using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Shelfy.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private GlobalHotkey? _globalHotkey;
    private string? _hotkeySetting;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SourceInitialized += MainWindow_SourceInitialized;
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.HideWindowRequested += () => Hide();
        viewModel.ReregisterHotkeyRequested += ReregisterHotkey;
    }

    /// <summary>
    /// 起動時の設定（ホットキー・ウィンドウサイズ）を適用する。
    /// App.xaml.cs OnStartup から呼ばれる。
    /// </summary>
    public void ApplyStartupSettings(string? hotkeySetting, string? windowWidthStr, string? windowHeightStr)
    {
        _hotkeySetting = hotkeySetting;

        if (double.TryParse(windowWidthStr, out var w) && w > 0)
            Width = w;
        if (double.TryParse(windowHeightStr, out var h) && h > 0)
            Height = h;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // ウィンドウハンドルが有効になった後にホットキーを登録
        RegisterGlobalHotkey();
    }

    private void RegisterGlobalHotkey()
    {
        var hotkeyStr = _hotkeySetting ?? "Ctrl+Shift+Space";
        _globalHotkey = new GlobalHotkey(this, hotkeyStr);
        _globalHotkey.HotkeyPressed += OnGlobalHotkeyPressed;

        if (!_globalHotkey.Register())
        {
            // ホットキー登録に失敗した場合はステータスに表示
            if (_viewModel is not null)
            {
                _viewModel.StatusMessage = $"Warning: Could not register global hotkey ({hotkeyStr})";
            }
        }
    }

    /// <summary>
    /// ホットキーを再登録する（設定変更時に呼ばれる）
    /// </summary>
    private void ReregisterHotkey(string newHotkeyString)
    {
        _globalHotkey?.Dispose();
        _hotkeySetting = newHotkeyString;
        RegisterGlobalHotkey();
    }

    private void OnGlobalHotkeyPressed()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
            Activate();
            Focus();
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.LoadAsync();
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // ウィンドウを閉じる代わりに非表示にする（常駐）
        e.Cancel = true;

        // ウィンドウサイズを保存
        if (_viewModel is not null && WindowState == WindowState.Normal)
        {
            _ = SaveWindowSizeSafeAsync(_viewModel, ActualWidth, ActualHeight);
        }

        Hide();
    }

    /// <summary>
    /// ウィンドウサイズ保存の安全な fire-and-forget ラッパー
    /// </summary>
    private static async Task SaveWindowSizeSafeAsync(MainViewModel viewModel, double width, double height)
    {
        try
        {
            await viewModel.SaveWindowSizeAsync(width, height);
        }
        catch
        {
            // ウィンドウサイズ保存失敗は致命的でないため無視
        }
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_viewModel is not null && e.NewValue is ShelfViewModel shelfVM)
        {
            _viewModel.SelectedShelf = shelfVM;
        }
    }

    private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.LaunchItemCommand.CanExecute(null) == true)
        {
            _viewModel.LaunchItemCommand.Execute(null);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else if (!e.Handled)
        {
            e.Effects = DragDropEffects.None;
        }
        if (!e.Handled) e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (_viewModel is null) return;
        if (e.Handled) return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                await _viewModel.AddItemCommand.ExecuteAsync(files);
            }
        }
    }

    #region Item ListView D&D

    private void ItemList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragDropReorder.ListView_PreviewMouseLeftButtonDown(sender, e);
    }

    private void ItemList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        DragDropReorder.ListView_PreviewMouseMove(sender, e);
    }

    private void ItemList_DragOver(object sender, DragEventArgs e)
    {
        DragDropReorder.ListView_DragOver(sender, e);
    }

    private async void ItemList_Drop(object sender, DragEventArgs e)
    {
        var dropResult = DragDropReorder.ListView_Drop(sender, e);
        if (dropResult is null) return;

        var (source, targetIndex) = dropResult.Value;
        if (_viewModel is null || source is not ViewModels.ItemViewModel sourceItem) return;

        var currentIndex = _viewModel.CurrentItems.IndexOf(sourceItem);
        if (currentIndex < 0 || currentIndex == targetIndex) return;

        // ローカルで移動
        _viewModel.CurrentItems.Move(currentIndex, targetIndex);

        // UseCase で永続化
        await _viewModel.ReorderCurrentItemsAsync();
    }

    #endregion

    #region Shelf TreeView D&D

    private void ShelfTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragDropReorder.TreeView_PreviewMouseLeftButtonDown(sender, e);
    }

    private void ShelfTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        DragDropReorder.TreeView_PreviewMouseMove(sender, e);
    }

    private void ShelfTree_DragOver(object sender, DragEventArgs e)
    {
        DragDropReorder.TreeView_DragOver(sender, e);
    }

    private async void ShelfTree_Drop(object sender, DragEventArgs e)
    {
        var dropResult = DragDropReorder.TreeView_Drop(sender, e);
        if (dropResult is null) return;

        var (source, target, position) = dropResult.Value;
        if (_viewModel is null) return;
        if (source is not ViewModels.ShelfViewModel sourceShelf) return;
        if (target is not ViewModels.ShelfViewModel targetShelf) return;

        await _viewModel.ReorderShelfByDropAsync(sourceShelf, targetShelf, position == DragDropReorder.DropPosition.Before);
    }

    #endregion

    /// <summary>
    /// アプリケーションを終了する
    /// </summary>
    public void ExitApplication()
    {
        _globalHotkey?.Dispose();
        (_viewModel as IDisposable)?.Dispose();
        Application.Current.Shutdown();
    }
}
