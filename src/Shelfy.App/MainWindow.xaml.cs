using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shelfy.App.ViewModels;

using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace Shelfy.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private GlobalHotkey? _globalHotkey;

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
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // ウィンドウハンドルが有効になった後にホットキーを登録
        RegisterGlobalHotkey();
    }

    private void RegisterGlobalHotkey()
    {
        _globalHotkey = new GlobalHotkey(this);
        _globalHotkey.HotkeyPressed += OnGlobalHotkeyPressed;

        if (!_globalHotkey.Register())
        {
            // ホットキー登録に失敗した場合はステータスに表示
            if (_viewModel is not null)
            {
                _viewModel.StatusMessage = "Warning: Could not register global hotkey (Ctrl+Shift+Space)";
            }
        }
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
        Hide();
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
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (_viewModel is null) return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                await _viewModel.AddItemCommand.ExecuteAsync(files);
            }
        }
    }

    /// <summary>
    /// アプリケーションを終了する
    /// </summary>
    public void ExitApplication()
    {
        _globalHotkey?.Dispose();
        Application.Current.Shutdown();
    }
}
