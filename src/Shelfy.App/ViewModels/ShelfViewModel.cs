using CommunityToolkit.Mvvm.ComponentModel;
using Shelfy.Core.Domain.Entities;

namespace Shelfy.App.ViewModels;

/// <summary>
/// Shelf の表示用 ViewModel
/// </summary>
public partial class ShelfViewModel : ObservableObject
{
    [ObservableProperty]
    private ShelfId _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private int _sortOrder;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public ShelfId? ParentId { get; private set; }

    public ObservableCollectionEx<ShelfViewModel> Children { get; } = [];

    public ShelfViewModel(Shelf shelf)
    {
        _id = shelf.Id;
        _name = shelf.Name;
        _isPinned = shelf.IsPinned;
        _sortOrder = shelf.SortOrder;
        ParentId = shelf.ParentId;
    }

    public void Update(Shelf shelf)
    {
        Id = shelf.Id;
        Name = shelf.Name;
        IsPinned = shelf.IsPinned;
        SortOrder = shelf.SortOrder;
        ParentId = shelf.ParentId;
    }
}
