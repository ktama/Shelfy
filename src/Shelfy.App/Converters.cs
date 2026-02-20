using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Shelfy.Core.Domain.Entities;
using Wpf.Ui.Controls;

namespace Shelfy.App;

/// <summary>
/// ItemType を SymbolRegular アイコンに変換するコンバーター
/// </summary>
public class ItemTypeToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ItemType type)
        {
            return type switch
            {
                ItemType.File => SymbolRegular.Document24,
                ItemType.Folder => SymbolRegular.FolderOpen24,
                ItemType.Url => SymbolRegular.Globe24,
                _ => SymbolRegular.Question24,
            };
        }
        return SymbolRegular.Document24;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// bool (IsPinned) を Shelf アイコンの SymbolRegular に変換するコンバーター
/// </summary>
public class BoolToPinSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true
            ? SymbolRegular.Pin24
            : SymbolRegular.Folder24;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 文字列が空でない場合に Visibility.Visible を返すコンバーター
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// bool の逆を Visibility に変換するコンバーター（false = Visible）
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
