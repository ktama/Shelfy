using System.Windows;
using System.Windows.Controls;

using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Shelfy.App;

/// <summary>
/// 入力ダイアログウィンドウ
/// </summary>
public partial class InputDialog : Window
{
    public string InputText => InputTextBox.Text;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeInputDialog();
        Title = title;
        PromptText.Text = prompt;
        InputTextBox.Text = defaultValue;
        InputTextBox.SelectAll();
    }

    private void InitializeInputDialog()
    {
        Width = 350;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        PromptText = new TextBlock { Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(PromptText, 0);
        grid.Children.Add(PromptText);

        InputTextBox = new TextBox { Margin = new Thickness(0, 0, 0, 16) };
        Grid.SetRow(InputTextBox, 1);
        grid.Children.Add(InputTextBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetRow(buttonPanel, 2);

        var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button { Content = "Cancel", Width = 75, IsCancel = true };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }

    private TextBlock PromptText { get; set; } = null!;
    private TextBox InputTextBox { get; set; } = null!;

    /// <summary>
    /// 入力ダイアログを表示してユーザー入力を取得する
    /// </summary>
    public static string? ShowDialog(Window owner, string title, string prompt, string defaultValue = "")
    {
        var dialog = new InputDialog(title, prompt, defaultValue)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }
}
