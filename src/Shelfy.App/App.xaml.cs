using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

using Application = System.Windows.Application;
using Shelfy.App.ViewModels;
using Shelfy.Core.UseCases.Items;
using Shelfy.Core.UseCases.Launch;
using Shelfy.Core.UseCases.Shelves;
using Shelfy.Infrastructure;

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

        // UseCases
        services.AddTransient<CreateShelfUseCase>();
        services.AddTransient<RenameShelfUseCase>();
        services.AddTransient<DeleteShelfUseCase>();
        services.AddTransient<AddItemUseCase>();
        services.AddTransient<RemoveItemUseCase>();
        services.AddTransient<RenameItemUseCase>();
        services.AddTransient<LaunchItemUseCase>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>(sp => new MainWindow(sp.GetRequiredService<MainViewModel>()));
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Console.Error.WriteLine("Dispatcher exception: " + e.Exception);
        LogException("Dispatcher", e.Exception);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.Error.WriteLine("Domain exception: " + e.ExceptionObject);
        if (e.ExceptionObject is Exception ex)
        {
            LogException("Domain", ex);
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // ShutdownMode を変更（明示的に終了するまでアプリを維持）
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            // タスクトレイアイコンを初期化
            _trayIcon = new TrayIcon(mainWindow);
            _trayIcon.Initialize();

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Startup exception: " + ex);
            LogException("Startup", ex);
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
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

