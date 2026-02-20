using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shelfy.App.ViewModels;
using Shelfy.Core.Domain.Entities;
using Wpf.Ui.Controls;

using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ContentDialog = Wpf.Ui.Controls.ContentDialog;
using ContentDialogHost = Wpf.Ui.Controls.ContentDialogHost;
using ContentDialogResult = Wpf.Ui.Controls.ContentDialogResult;
using ContentDialogButton = Wpf.Ui.Controls.ContentDialogButton;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace Shelfy.App;

/// <summary>
/// Fluent Design ContentDialog を使ったダイアログヘルパー
/// </summary>
public static class FluentDialogs
{
    /// <summary>
    /// ContentDialog のホスト（MainWindow 初期化時に設定される）
    /// </summary>
    public static ContentDialogHost? DialogHost { get; set; }

    /// <summary>
    /// 単一行テキスト入力ダイアログ
    /// </summary>
    public static async Task<string?> ShowInputAsync(string title, string prompt, string defaultValue = "")
    {
        if (DialogHost is null) return null;

        var textBox = new TextBox
        {
            Text = defaultValue,
            Margin = new Thickness(0, 8, 0, 0),
            SelectionStart = 0,
            SelectionLength = defaultValue.Length
        };

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(textBox);

        var dialog = new ContentDialog(DialogHost!)
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.Opened += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }

    /// <summary>
    /// 複数行メモ編集ダイアログ
    /// </summary>
    public static async Task<(bool confirmed, string? memo)> ShowMemoAsync(
        string title, string prompt, string? defaultValue = null)
    {
        if (DialogHost is null) return (false, null);

        var textBox = new TextBox
        {
            Text = defaultValue ?? string.Empty,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 120,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(textBox);

        var dialog = new ContentDialog(DialogHost!)
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "OK",
            SecondaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Secondary)
        {
            return (true, null); // Clear
        }
        if (result == ContentDialogResult.Primary)
        {
            var text = string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text;
            return (true, text);
        }

        return (false, null); // Cancel
    }

    /// <summary>
    /// 確認ダイアログ（Yes/No）
    /// </summary>
    public static async Task<bool> ShowConfirmAsync(string message, string title)
    {
        if (DialogHost is null) return false;

        var dialog = new ContentDialog(DialogHost!)
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Yes/No/Cancel 3択ダイアログ
    /// </summary>
    /// <returns>true=Yes, false=No, null=Cancel</returns>
    public static async Task<bool?> ShowYesNoCancelAsync(string message, string title)
    {
        if (DialogHost is null) return null;

        var dialog = new ContentDialog(DialogHost!)
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Yes",
            SecondaryButtonText = "No",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();

        return result switch
        {
            ContentDialogResult.Primary => true,
            ContentDialogResult.Secondary => false,
            _ => null
        };
    }

    /// <summary>
    /// Shelf 選択ダイアログ
    /// </summary>
    public static async Task<(bool confirmed, ShelfId? shelfId, bool isRoot)> ShowShelfPickerAsync(
        string title,
        IEnumerable<ShelfViewModel> rootShelves,
        ShelfId? excludeShelfId = null)
    {
        if (DialogHost is null) return (false, null, false);

        var treeView = new TreeView
        {
            MinHeight = 200,
            MaxHeight = 350,
            Margin = new Thickness(0, 8, 0, 0),
            BorderThickness = new Thickness(0)
        };

        // Create shelf template
        var template = new HierarchicalDataTemplate
        {
            DataType = typeof(ShelfPickerItem),
            ItemsSource = new System.Windows.Data.Binding("Children")
        };

        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var icon = new FrameworkElementFactory(typeof(SymbolIcon));
        icon.SetBinding(SymbolIcon.SymbolProperty, new System.Windows.Data.Binding("Symbol"));
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0));
        panel.AppendChild(icon);

        var nameBlock = new FrameworkElementFactory(typeof(TextBlock));
        nameBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        nameBlock.SetValue(TextBlock.FontSizeProperty, 13.0);
        panel.AppendChild(nameBlock);

        template.VisualTree = panel;
        treeView.ItemTemplate = template;

        // Expand all by default
        var containerStyle = new Style(typeof(TreeViewItem));
        containerStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, true));
        treeView.ItemContainerStyle = containerStyle;

        // Build items
        var items = new List<ShelfPickerItem>();
        items.Add(new ShelfPickerItem("(Root)", null, isRoot: true));
        foreach (var shelf in rootShelves)
        {
            var item = BuildShelfPickerItem(shelf, excludeShelfId);
            if (item is not null)
                items.Add(item);
        }
        treeView.ItemsSource = items;

        ShelfViewModel? selectedShelf = null;
        bool isRootSelected = false;
        treeView.SelectedItemChanged += (s, e) =>
        {
            if (e.NewValue is ShelfPickerItem pickerItem)
            {
                selectedShelf = pickerItem.ShelfViewModel;
                isRootSelected = pickerItem.IsRoot;
            }
        };

        var contentPanel = new StackPanel();
        contentPanel.Children.Add(new TextBlock
        {
            Text = "Select destination shelf:",
            TextWrapping = TextWrapping.Wrap
        });
        contentPanel.Children.Add(treeView);

        var dialog = new ContentDialog(DialogHost!)
        {
            Title = title,
            Content = contentPanel,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && (selectedShelf is not null || isRootSelected))
        {
            return (true, selectedShelf?.Id, isRootSelected);
        }

        return (false, null, false);
    }

    private static ShelfPickerItem? BuildShelfPickerItem(ShelfViewModel shelf, ShelfId? excludeShelfId)
    {
        if (excludeShelfId.HasValue && shelf.Id == excludeShelfId.Value)
            return null;

        var item = new ShelfPickerItem(shelf.Name, shelf);
        foreach (var child in shelf.Children)
        {
            var childItem = BuildShelfPickerItem(child, excludeShelfId);
            if (childItem is not null)
                item.Children.Add(childItem);
        }
        return item;
    }

    /// <summary>
    /// 設定ダイアログ
    /// </summary>
    public static async Task<SettingsDialogResult?> ShowSettingsAsync(
        string? currentHotkey,
        bool currentStartMinimized,
        int currentRecentItemsCount,
        double currentWindowWidth,
        double currentWindowHeight)
    {
        if (DialogHost is null) return null;

        var hotkeyTextBox = new TextBox
        {
            Text = currentHotkey ?? "Ctrl+Shift+Space",
            IsReadOnly = true,
            Margin = new Thickness(0, 4, 0, 8),
            ToolTip = "Click and press a key combination"
        };
        hotkeyTextBox.GotFocus += (s, e) =>
        {
            if (System.Windows.Application.Current.TryFindResource("SystemAccentColorPrimaryBrush") is System.Windows.Media.Brush accentBrush)
            {
                var clone = accentBrush.Clone();
                clone.Opacity = 0.15;
                hotkeyTextBox.Background = clone;
            }
        };
        hotkeyTextBox.LostFocus += (s, e) =>
            hotkeyTextBox.ClearValue(System.Windows.Controls.Control.BackgroundProperty);

        hotkeyTextBox.PreviewKeyDown += (s, e) =>
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
                return;

            var modifiers = Keyboard.Modifiers;
            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (parts.Count == 0) return;

            parts.Add(key.ToString());
            hotkeyTextBox.Text = string.Join("+", parts);
        };

        var startMinimizedCheck = new CheckBox
        {
            Content = "Start minimized (to system tray)",
            IsChecked = currentStartMinimized,
            Margin = new Thickness(0, 4, 0, 8)
        };

        var recentCountBox = new TextBox
        {
            Text = currentRecentItemsCount.ToString(),
            Width = 80,
            Margin = new Thickness(0, 4, 0, 8)
        };

        var windowWidthBox = new TextBox
        {
            Text = currentWindowWidth.ToString("0"),
            Width = 80,
            Margin = new Thickness(0, 4, 4, 8)
        };

        var windowHeightBox = new TextBox
        {
            Text = currentWindowHeight.ToString("0"),
            Width = 80,
            Margin = new Thickness(4, 4, 0, 8)
        };

        var sizePanel = new StackPanel { Orientation = Orientation.Horizontal };
        sizePanel.Children.Add(windowWidthBox);
        sizePanel.Children.Add(new TextBlock
        {
            Text = "×",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0)
        });
        sizePanel.Children.Add(windowHeightBox);

        var settingsPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        settingsPanel.Children.Add(new TextBlock { Text = "Global Hotkey", FontWeight = FontWeights.SemiBold });
        settingsPanel.Children.Add(hotkeyTextBox);
        settingsPanel.Children.Add(startMinimizedCheck);
        settingsPanel.Children.Add(new TextBlock { Text = "Recent items count", FontWeight = FontWeights.SemiBold });
        settingsPanel.Children.Add(recentCountBox);
        settingsPanel.Children.Add(new TextBlock { Text = "Window size (Width × Height)", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
        settingsPanel.Children.Add(sizePanel);

        var dialog = new ContentDialog(DialogHost!)
        {
            Title = "Settings",
            Content = settingsPanel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        return new SettingsDialogResult(
            HotkeyText: hotkeyTextBox.Text,
            StartMinimized: startMinimizedCheck.IsChecked == true,
            RecentItemsCount: int.TryParse(recentCountBox.Text, out var rc) ? rc : 20,
            WindowWidth: double.TryParse(windowWidthBox.Text, out var ww) ? ww : 800,
            WindowHeight: double.TryParse(windowHeightBox.Text, out var wh) ? wh : 500
        );
    }
}

/// <summary>
/// 設定ダイアログの結果
/// </summary>
public record SettingsDialogResult(
    string HotkeyText,
    bool StartMinimized,
    int RecentItemsCount,
    double WindowWidth,
    double WindowHeight);

/// <summary>
/// ShelfPicker ダイアログの TreeView アイテム
/// </summary>
public class ShelfPickerItem
{
    public string Name { get; }
    public SymbolRegular Symbol => IsRoot
        ? SymbolRegular.Archive24
        : (ShelfViewModel?.IsPinned == true ? SymbolRegular.Pin24 : SymbolRegular.Folder24);
    public ShelfViewModel? ShelfViewModel { get; }
    public bool IsRoot { get; }
    public List<ShelfPickerItem> Children { get; } = [];

    public ShelfPickerItem(string name, ShelfViewModel? shelfViewModel, bool isRoot = false)
    {
        Name = name;
        ShelfViewModel = shelfViewModel;
        IsRoot = isRoot;
    }
}
