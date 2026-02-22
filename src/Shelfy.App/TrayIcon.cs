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
            Icon = LoadAppIcon(),
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

    private static Icon LoadAppIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/shelfy.ico", UriKind.Absolute);
        var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        GC.SuppressFinalize(this);
    }
}
