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
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public ShelfId? ParentId { get; }

    public ObservableCollectionEx<ShelfViewModel> Children { get; } = [];
    public ObservableCollectionEx<ItemViewModel> Items { get; } = [];

    public ShelfViewModel(Shelf shelf)
    {
        _id = shelf.Id;
        _name = shelf.Name;
        _isPinned = shelf.IsPinned;
        ParentId = shelf.ParentId;
    }

    public void Update(Shelf shelf)
    {
        Id = shelf.Id;
        Name = shelf.Name;
        IsPinned = shelf.IsPinned;
    }
}
