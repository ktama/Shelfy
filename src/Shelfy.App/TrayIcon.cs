using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace Shelfy.App;

/// <summary>
/// タスクトレイアイコンを管理するクラス
/// </summary>
public class TrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly MainWindow _mainWindow;

    public TrayIcon(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application, // デフォルトアイコン
            Text = "Shelfy - Press Ctrl+Shift+Space to show",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Show Shelfy", null, (s, e) => ShowWindow());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowWindow();
    }

    private void ShowWindow()
    {
        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    private void ExitApplication()
    {
        Dispose();
        _mainWindow.ExitApplication();
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        GC.SuppressFinalize(this);
    }
}
