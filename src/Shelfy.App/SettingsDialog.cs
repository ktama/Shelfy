using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Label = System.Windows.Controls.Label;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Shelfy.App;

/// <summary>
/// 設定ダイアログ
/// </summary>
public class SettingsDialog : Window
{
    private TextBox _hotkeyDisplay = null!;
    private CheckBox _startMinimized = null!;
    private TextBox _recentItemsCount = null!;
    private TextBox _windowWidth = null!;
    private TextBox _windowHeight = null!;

    // 結果
    public string? HotkeyText => _hotkeyDisplay.Text;
    public bool StartMinimizedValue => _startMinimized.IsChecked == true;
    public int RecentItemsCountValue => int.TryParse(_recentItemsCount.Text, out var v) ? v : 20;
    public double WindowWidthValue => double.TryParse(_windowWidth.Text, out var v) ? v : 800;
    public double WindowHeightValue => double.TryParse(_windowHeight.Text, out var v) ? v : 500;

    public SettingsDialog(
        string? currentHotkey,
        bool currentStartMinimized,
        int currentRecentItemsCount,
        double currentWindowWidth,
        double currentWindowHeight)
    {
        InitializeSettingsDialog(
            currentHotkey,
            currentStartMinimized,
            currentRecentItemsCount,
            currentWindowWidth,
            currentWindowHeight);
    }

    private void InitializeSettingsDialog(
        string? currentHotkey,
        bool currentStartMinimized,
        int currentRecentItemsCount,
        double currentWindowWidth,
        double currentWindowHeight)
    {
        Title = "Settings";
        Width = 420;
        Height = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        var mainGrid = new Grid { Margin = new Thickness(16) };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Settings panel
        var settingsPanel = new StackPanel { Orientation = Orientation.Vertical };

        // Global Hotkey
        AddSettingRow(settingsPanel, "Global Hotkey:", out _hotkeyDisplay);
        _hotkeyDisplay.Text = currentHotkey ?? "Ctrl+Shift+Space";
        _hotkeyDisplay.IsReadOnly = true;
        _hotkeyDisplay.Background = System.Windows.Media.Brushes.WhiteSmoke;
        _hotkeyDisplay.PreviewKeyDown += HotkeyDisplay_PreviewKeyDown;
        _hotkeyDisplay.ToolTip = "Click and press a key combination to set the hotkey";
        _hotkeyDisplay.GotFocus += (s, e) => _hotkeyDisplay.Background = System.Windows.Media.Brushes.LightYellow;
        _hotkeyDisplay.LostFocus += (s, e) => _hotkeyDisplay.Background = System.Windows.Media.Brushes.WhiteSmoke;

        // Start Minimized
        _startMinimized = new CheckBox
        {
            Content = "Start minimized (to system tray)",
            IsChecked = currentStartMinimized,
            Margin = new Thickness(0, 8, 0, 0)
        };
        settingsPanel.Children.Add(_startMinimized);

        // Recent Items Count
        AddSettingRow(settingsPanel, "Recent items count:", out _recentItemsCount);
        _recentItemsCount.Text = currentRecentItemsCount.ToString();

        // Window Size
        var sizePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0)
        };

        sizePanel.Children.Add(new Label { Content = "Window size:", Width = 120 });

        _windowWidth = new TextBox { Width = 60, Text = currentWindowWidth.ToString("0") };
        sizePanel.Children.Add(_windowWidth);

        sizePanel.Children.Add(new Label { Content = "x", Margin = new Thickness(4, 0, 4, 0) });

        _windowHeight = new TextBox { Width = 60, Text = currentWindowHeight.ToString("0") };
        sizePanel.Children.Add(_windowHeight);

        settingsPanel.Children.Add(sizePanel);

        Grid.SetRow(settingsPanel, 0);
        mainGrid.Children.Add(settingsPanel);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        Grid.SetRow(buttonPanel, 1);

        var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button { Content = "Cancel", Width = 75, IsCancel = true };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        mainGrid.Children.Add(buttonPanel);
        Content = mainGrid;
    }

    private static void AddSettingRow(StackPanel parent, string label, out TextBox textBox)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0)
        };
        row.Children.Add(new Label { Content = label, Width = 120 });

        textBox = new TextBox { Width = 200 };
        row.Children.Add(textBox);

        parent.Children.Add(row);
    }

    private void HotkeyDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 修飾キーだけの場合は無視
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var parts = new List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");

        if (parts.Count == 0) return; // 修飾キーなしはホットキーとして不適

        parts.Add(key.ToString());
        _hotkeyDisplay.Text = string.Join("+", parts);
    }

    /// <summary>
    /// 設定ダイアログを表示する
    /// </summary>
    public static SettingsDialog? ShowSettingsDialog(
        Window owner,
        string? currentHotkey,
        bool currentStartMinimized,
        int currentRecentItemsCount,
        double currentWindowWidth,
        double currentWindowHeight)
    {
        var dialog = new SettingsDialog(
            currentHotkey,
            currentStartMinimized,
            currentRecentItemsCount,
            currentWindowWidth,
            currentWindowHeight)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog : null;
    }
}
