using System.Windows;
using System.Windows.Controls;
using Shelfy.App.ViewModels;
using Shelfy.Core.Domain.Entities;

using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace Shelfy.App;

/// <summary>
/// Shelf を選択するためのダイアログ
/// </summary>
public class ShelfPickerDialog : Window
{
    private TreeView _treeView = null!;
    private ShelfViewModel? _selectedShelf;

    /// <summary>
    /// 選択された Shelf の ID（ルートへ移動の場合は null）
    /// </summary>
    public ShelfId? SelectedShelfId => _selectedShelf?.Id;

    /// <summary>
    /// 選択された Shelf の名前
    /// </summary>
    public string? SelectedShelfName => _selectedShelf?.Name;

    /// <summary>
    /// ルートへ移動が選択されたかどうか
    /// </summary>
    public bool IsRootSelected { get; private set; }

    public ShelfPickerDialog(
        string title,
        IEnumerable<ShelfViewModel> rootShelves,
        ShelfId? excludeShelfId = null)
    {
        InitializeShelfPickerDialog(title, rootShelves, excludeShelfId);
    }

    private void InitializeShelfPickerDialog(
        string title,
        IEnumerable<ShelfViewModel> rootShelves,
        ShelfId? excludeShelfId)
    {
        Title = title;
        Width = 350;
        Height = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = false;

        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var promptText = new TextBlock
        {
            Text = "Select destination shelf:",
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(promptText, 0);
        grid.Children.Add(promptText);

        // TreeView for shelf selection
        _treeView = new TreeView { Margin = new Thickness(0, 0, 0, 12) };

        // Create shelf template
        var template = new HierarchicalDataTemplate
        {
            DataType = typeof(ShelfPickerItem),
            ItemsSource = new System.Windows.Data.Binding("Children")
        };

        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var icon = new FrameworkElementFactory(typeof(TextBlock));
        icon.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Icon"));
        icon.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 4, 0));
        panel.AppendChild(icon);

        var nameBlock = new FrameworkElementFactory(typeof(TextBlock));
        nameBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        nameBlock.SetValue(TextBlock.FontSizeProperty, 13.0);
        panel.AppendChild(nameBlock);

        template.VisualTree = panel;
        _treeView.ItemTemplate = template;

        // Style to expand all items
        var itemContainerStyle = new Style(typeof(TreeViewItem));
        itemContainerStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, true));
        _treeView.ItemContainerStyle = itemContainerStyle;

        _treeView.SelectedItemChanged += (s, e) =>
        {
            if (e.NewValue is ShelfPickerItem item)
            {
                _selectedShelf = item.ShelfViewModel;
                IsRootSelected = item.IsRoot;
            }
        };

        // Build tree items (with "Root" option)
        var items = new List<ShelfPickerItem>();
        items.Add(new ShelfPickerItem("📦 (Root)", null, isRoot: true));
        foreach (var shelf in rootShelves)
        {
            var item = BuildShelfPickerItem(shelf, excludeShelfId);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        _treeView.ItemsSource = items;
        Grid.SetRow(_treeView, 1);
        grid.Children.Add(_treeView);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 2);

        var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        okButton.Click += (s, e) =>
        {
            if (_selectedShelf is not null || IsRootSelected)
            {
                DialogResult = true;
                Close();
            }
        };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button { Content = "Cancel", Width = 75, IsCancel = true };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }

    private static ShelfPickerItem? BuildShelfPickerItem(ShelfViewModel shelf, ShelfId? excludeShelfId)
    {
        if (excludeShelfId.HasValue && shelf.Id == excludeShelfId.Value)
        {
            return null;
        }

        var item = new ShelfPickerItem(shelf.Name, shelf);
        foreach (var child in shelf.Children)
        {
            var childItem = BuildShelfPickerItem(child, excludeShelfId);
            if (childItem is not null)
            {
                item.Children.Add(childItem);
            }
        }

        return item;
    }

    /// <summary>
    /// Shelf 選択ダイアログを表示する
    /// </summary>
    public static (bool confirmed, ShelfId? shelfId, bool isRoot) ShowPickerDialog(
        Window owner,
        string title,
        IEnumerable<ShelfViewModel> rootShelves,
        ShelfId? excludeShelfId = null)
    {
        var dialog = new ShelfPickerDialog(title, rootShelves, excludeShelfId)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true)
        {
            return (true, dialog.SelectedShelfId, dialog.IsRootSelected);
        }

        return (false, null, false);
    }
}

/// <summary>
/// ShelfPickerDialog のツリーに表示するアイテム
/// </summary>
public class ShelfPickerItem
{
    public string Name { get; }
    public string Icon => IsRoot ? "📦" : (ShelfViewModel?.IsPinned == true ? "📌" : "📁");
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
