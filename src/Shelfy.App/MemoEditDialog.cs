using System.Windows;
using System.Windows.Controls;

using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Shelfy.App;

/// <summary>
/// メモ編集用ダイアログ（複数行テキストボックス）
/// </summary>
public class MemoEditDialog : Window
{
    public string MemoText => MemoTextBox.Text;

    private TextBlock PromptText { get; set; } = null!;
    private TextBox MemoTextBox { get; set; } = null!;

    public MemoEditDialog(string title, string prompt, string? defaultValue = null)
    {
        InitializeMemoEditDialog();
        Title = title;
        PromptText.Text = prompt;
        MemoTextBox.Text = defaultValue ?? string.Empty;
    }

    private void InitializeMemoEditDialog()
    {
        Width = 400;
        Height = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = false;

        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        PromptText = new TextBlock { Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(PromptText, 0);
        grid.Children.Add(PromptText);

        MemoTextBox = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(MemoTextBox, 1);
        grid.Children.Add(MemoTextBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 2);

        var clearButton = new Button { Content = "Clear", Width = 75, Margin = new Thickness(0, 0, 8, 0) };
        clearButton.Click += (s, e) => { MemoTextBox.Text = string.Empty; };
        buttonPanel.Children.Add(clearButton);

        var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button { Content = "Cancel", Width = 75, IsCancel = true };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }

    /// <summary>
    /// メモ編集ダイアログを表示する
    /// </summary>
    /// <returns>OK の場合はメモテキスト（空文字はnullに変換）、キャンセルの場合はnullを返す特殊型</returns>
    public static (bool confirmed, string? memo) ShowMemoDialog(Window owner, string title, string prompt, string? defaultValue = null)
    {
        var dialog = new MemoEditDialog(title, prompt, defaultValue)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true)
        {
            var text = string.IsNullOrWhiteSpace(dialog.MemoText) ? null : dialog.MemoText;
            return (true, text);
        }

        return (false, null);
    }
}
