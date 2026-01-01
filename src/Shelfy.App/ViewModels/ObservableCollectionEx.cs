using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Shelfy.App.ViewModels;

/// <summary>
/// 拡張 ObservableCollection（AddRange サポート）
/// </summary>
public class ObservableCollectionEx<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public void AddRange(IEnumerable<T> items)
    {
        _suppressNotification = true;

        foreach (var item in items)
        {
            Add(item);
        }

        _suppressNotification = false;
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void ReplaceAll(IEnumerable<T> items)
    {
        _suppressNotification = true;

        Clear();
        foreach (var item in items)
        {
            Add(item);
        }

        _suppressNotification = false;
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }
}
