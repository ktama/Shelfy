using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

using Application = System.Windows.Application;
using Shelfy.App.ViewModels;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;
using Shelfy.Infrastructure;
using Wpf.Ui.Appearance;

namespace Shelfy.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;
    private TrayIcon? _trayIcon;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // データベースパスを設定
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var shelfyDataPath = Path.Combine(appDataPath, "Shelfy");
        Directory.CreateDirectory(shelfyDataPath);
        var databasePath = Path.Combine(shelfyDataPath, "shelfy.db");

        // Shelfy のサービスを登録
        services.AddShelfy(databasePath);

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>(sp => new MainWindow(sp.GetRequiredService<MainViewModel>()));
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var logger = _serviceProvider.GetService<IAppLogger>();
        logger?.Error("Dispatcher unhandled exception", e.Exception);
        LogException("Dispatcher", e.Exception);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var logger = _serviceProvider.GetService<IAppLogger>();
        if (e.ExceptionObject is Exception ex)
        {
            logger?.Error("Domain unhandled exception", ex);
            LogException("Domain", ex);
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // ShutdownMode を変更（明示的に終了するまでアプリを維持）
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            var logger = _serviceProvider.GetRequiredService<IAppLogger>();
            var settingsRepo = _serviceProvider.GetRequiredService<ISettingsRepository>();

            logger.Info("Shelfy started");

            // 起動時に設定を一括読み込み
            var allSettings = await settingsRepo.GetAllAsync();
            allSettings.TryGetValue(SettingKeys.GlobalHotkey, out var hotkeySetting);
            allSettings.TryGetValue(SettingKeys.StartMinimized, out var startMinStr);
            allSettings.TryGetValue(SettingKeys.WindowWidth, out var windowWidthStr);
            allSettings.TryGetValue(SettingKeys.WindowHeight, out var windowHeightStr);

            // ウィンドウサイズとホットキー設定を適用
            mainWindow.ApplyStartupSettings(hotkeySetting, windowWidthStr, windowHeightStr);

            // HotkeyHoldState に実際のホットキー修飾キーを設定
            var hotkeyHoldState = _serviceProvider.GetRequiredService<IHotkeyHoldState>();
            hotkeyHoldState.ConfigureFromHotkeyString(hotkeySetting ?? "Ctrl+Shift+Space");

            // タスクトレイアイコンを初期化
            _trayIcon = new TrayIcon(mainWindow);
            _trayIcon.Initialize();

            // システムテーマの自動追従を有効化
            var systemTheme = ApplicationThemeManager.GetSystemTheme();
            ApplicationThemeManager.Apply(
                systemTheme == SystemTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
            SystemThemeWatcher.Watch(mainWindow);

            // Start minimized が有効でない場合のみウィンドウを表示
            if (startMinStr != "true")
            {
                mainWindow.Show();
            }
            else
            {
                logger.Info("Starting minimized to system tray");
            }
        }
        catch (Exception ex)
        {
            var logger = _serviceProvider.GetService<IAppLogger>();
            logger?.Error("Startup exception", ex);
            LogException("Startup", ex);
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var logger = _serviceProvider.GetService<IAppLogger>();
        logger?.Info("Shelfy exiting");

        _trayIcon?.Dispose();
        _serviceProvider.Dispose();
        base.OnExit(e);
    }

    private void LogException(string prefix, Exception ex)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shelfy");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, "Shelfy.log");
            var entry = $"{DateTime.UtcNow:O} [{prefix}] {ex}{Environment.NewLine}";
            File.AppendAllText(logFile, entry);
        }
        catch
        {
            // ログ書き込みに失敗してもアプリを終了させたくない
        }
    }
}

