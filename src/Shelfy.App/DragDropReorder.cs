using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using Point = System.Windows.Point;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace Shelfy.App;

/// <summary>
/// ListView / TreeView のドラッグ＆ドロップによる並び替えサポート
/// </summary>
public static class DragDropReorder
{
    private static readonly string ListViewItemFormat = "ShelfyListViewItem";
    private static readonly string TreeViewItemFormat = "ShelfyTreeViewItem";

    // ListView 用 D&D 状態
    private static Point _listViewDragStartPoint;
    private static bool _listViewIsDragging;

    // TreeView 用 D&D 状態
    private static Point _treeViewDragStartPoint;
    private static bool _treeViewIsDragging;

    #region ListView (Item) D&D

    /// <summary>
    /// ListView のドラッグ開始点を記録
    /// </summary>
    public static void ListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _listViewDragStartPoint = e.GetPosition(null);
        _listViewIsDragging = false;
    }

    /// <summary>
    /// ListView のドラッグ開始を検出・実行
    /// </summary>
    public static void ListView_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _listViewIsDragging) return;

        var position = e.GetPosition(null);
        var diff = _listViewDragStartPoint - position;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is not ListView listView) return;

        // ドラッグ元のアイテムを特定
        var sourceItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
        if (sourceItem?.DataContext is null) return;

        _listViewIsDragging = true;
        var data = new System.Windows.DataObject(ListViewItemFormat, sourceItem.DataContext);
        DragDrop.DoDragDrop(listView, data, DragDropEffects.Move);
        _listViewIsDragging = false;
    }

    /// <summary>
    /// ListView のドラッグオーバーで効果を設定
    /// </summary>
    public static void ListView_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ListViewItemFormat))
        {
            return; // 外部ファイルのドラッグにはここで介入しない
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    /// <summary>
    /// ListView のドロップでアイテムを並び替え
    /// </summary>
    /// <returns>ドロップが処理された場合は (sourceItem, targetIndex) を返す。そうでなければ null。</returns>
    public static (object Source, int TargetIndex)? ListView_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ListViewItemFormat))
            return null;

        if (sender is not ListView listView) return null;

        var sourceData = e.Data.GetData(ListViewItemFormat);
        if (sourceData is null) return null;

        // ドロップ先のアイテムを特定
        var targetElement = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
        if (targetElement?.DataContext is null)
        {
            // リストの空白部分にドロップ → 末尾へ
            return (sourceData, listView.Items.Count - 1);
        }

        var targetIndex = listView.Items.IndexOf(targetElement.DataContext);
        if (targetIndex < 0) return null;

        e.Handled = true;
        return (sourceData, targetIndex);
    }

    #endregion

    #region TreeView (Shelf) D&D

    /// <summary>
    /// TreeView のドラッグ開始点を記録
    /// </summary>
    public static void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _treeViewDragStartPoint = e.GetPosition(null);
        _treeViewIsDragging = false;
    }

    /// <summary>
    /// TreeView のドラッグ開始を検出・実行
    /// </summary>
    public static void TreeView_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _treeViewIsDragging) return;

        var position = e.GetPosition(null);
        var diff = _treeViewDragStartPoint - position;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is not TreeView treeView) return;

        var sourceItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (sourceItem?.DataContext is null) return;

        _treeViewIsDragging = true;
        var data = new System.Windows.DataObject(TreeViewItemFormat, sourceItem.DataContext);
        DragDrop.DoDragDrop(treeView, data, DragDropEffects.Move);
        _treeViewIsDragging = false;
    }

    /// <summary>
    /// TreeView のドラッグオーバーで効果を設定
    /// </summary>
    public static void TreeView_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(TreeViewItemFormat))
            return;

        // ドロップ先を検出
        var targetItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (targetItem is null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var sourceData = e.Data.GetData(TreeViewItemFormat);
        if (sourceData == targetItem.DataContext)
        {
            // 自分自身へのドロップは無効
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    /// <summary>
    /// TreeView のドロップでShelfを並び替え
    /// </summary>
    /// <returns>ドロップが処理された場合は (sourceItem, targetItem, dropPosition) を返す</returns>
    public static (object Source, object Target, DropPosition Position)? TreeView_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(TreeViewItemFormat))
            return null;

        var sourceData = e.Data.GetData(TreeViewItemFormat);
        if (sourceData is null) return null;

        var targetElement = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (targetElement?.DataContext is null) return null;
        if (sourceData == targetElement.DataContext) return null;

        // ドロップ位置を判定（上半分 = Before、下半分 = After）
        var pos = e.GetPosition(targetElement);
        var height = targetElement.ActualHeight;
        var position = pos.Y < height / 2 ? DropPosition.Before : DropPosition.After;

        e.Handled = true;
        return (sourceData, targetElement.DataContext, position);
    }

    #endregion

    #region Helpers

    public enum DropPosition
    {
        Before,
        After
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T result)
                return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    #endregion
}
