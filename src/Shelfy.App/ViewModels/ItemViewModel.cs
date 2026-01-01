using CommunityToolkit.Mvvm.ComponentModel;
using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.System;

namespace Shelfy.App.ViewModels;

/// <summary>
/// Item の表示用 ViewModel
/// </summary>
public partial class ItemViewModel : ObservableObject
{
    private readonly IExistenceChecker? _existenceChecker;

    [ObservableProperty]
    private ItemId _id;

    [ObservableProperty]
    private ShelfId _shelfId;

    [ObservableProperty]
    private ItemType _type;

    [ObservableProperty]
    private string _target = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _memo;

    [ObservableProperty]
    private DateTime? _lastAccessedAt;

    [ObservableProperty]
    private bool _exists = true;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _shelfName = string.Empty;

    public string TypeIcon => Type switch
    {
        ItemType.File => "📄",
        ItemType.Folder => "📁",
        ItemType.Url => "🔗",
        _ => "❓"
    };

    public ItemViewModel(Item item, IExistenceChecker? existenceChecker = null)
    {
        _existenceChecker = existenceChecker;
        Update(item);
    }

    public void Update(Item item)
    {
        Id = item.Id;
        ShelfId = item.ShelfId;
        Type = item.Type;
        Target = item.Target;
        DisplayName = item.DisplayName;
        Memo = item.Memo;
        LastAccessedAt = item.LastAccessedAt;

        // 存在確認
        if (_existenceChecker != null)
        {
            Exists = _existenceChecker.Exists(item.Target);
        }
    }

    public void RefreshExistence()
    {
        if (_existenceChecker != null)
        {
            Exists = _existenceChecker.Exists(Target);
        }
    }
}
