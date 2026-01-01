using Microsoft.Extensions.DependencyInjection;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;
using Shelfy.Core.UseCases.Items;
using Shelfy.Core.UseCases.Launch;
using Shelfy.Core.UseCases.Search;
using Shelfy.Core.UseCases.Shelves;
using Shelfy.Infrastructure.Persistence;
using Shelfy.Infrastructure.System;

namespace Shelfy.Infrastructure;

/// <summary>
/// DI コンテナへのサービス登録拡張
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Shelfy のすべてのサービスを登録する（SQLite使用）
    /// </summary>
    public static IServiceCollection AddShelfy(this IServiceCollection services, string? databasePath = null)
    {
        // データベースパスが指定されていない場合はデフォルトパスを使用
        var dbPath = databasePath ?? GetDefaultDatabasePath();

        // Ports - System
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IExistenceChecker, FileExistenceChecker>();
        services.AddSingleton<IItemLauncher, Win32ItemLauncher>();
        services.AddSingleton<IHotkeyHoldState, Win32HotkeyHoldState>();

        // Ports - Persistence (SQLite)
        services.AddSingleton(_ => new SqliteConnectionFactory(dbPath));
        services.AddSingleton<IShelfRepository, SqliteShelfRepository>();
        services.AddSingleton<IItemRepository, SqliteItemRepository>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();

        // UseCases - Shelves
        services.AddTransient<CreateShelfUseCase>();
        services.AddTransient<RenameShelfUseCase>();
        services.AddTransient<DeleteShelfUseCase>();
        services.AddTransient<TogglePinShelfUseCase>();
        services.AddTransient<MoveShelfUseCase>();
        services.AddTransient<ReorderShelvesUseCase>();

        // UseCases - Items
        services.AddTransient<AddItemUseCase>();
        services.AddTransient<RemoveItemUseCase>();
        services.AddTransient<RenameItemUseCase>();
        services.AddTransient<GetRecentItemsUseCase>();
        services.AddTransient<GetMissingItemsUseCase>();
        services.AddTransient<UpdateItemMemoUseCase>();
        services.AddTransient<MoveItemToShelfUseCase>();

        // UseCases - Launch
        services.AddTransient<LaunchItemUseCase>();
        services.AddTransient<OpenParentFolderUseCase>();

        // UseCases - Search
        services.AddTransient<SearchItemsUseCase>();

        return services;
    }

    /// <summary>
    /// Shelfy のすべてのサービスを登録する（InMemory使用、テスト・開発用）
    /// </summary>
    public static IServiceCollection AddShelfyInMemory(this IServiceCollection services)
    {
        // Ports - System
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IExistenceChecker, FileExistenceChecker>();
        services.AddSingleton<IItemLauncher, Win32ItemLauncher>();
        services.AddSingleton<IHotkeyHoldState, Win32HotkeyHoldState>();

        // Ports - Persistence (InMemory)
        services.AddSingleton<IShelfRepository, InMemoryShelfRepository>();
        services.AddSingleton<IItemRepository, InMemoryItemRepository>();
        services.AddSingleton<ISettingsRepository, InMemorySettingsRepository>();

        // UseCases - Shelves
        services.AddTransient<CreateShelfUseCase>();
        services.AddTransient<RenameShelfUseCase>();
        services.AddTransient<DeleteShelfUseCase>();
        services.AddTransient<TogglePinShelfUseCase>();
        services.AddTransient<MoveShelfUseCase>();
        services.AddTransient<ReorderShelvesUseCase>();

        // UseCases - Items
        services.AddTransient<AddItemUseCase>();
        services.AddTransient<RemoveItemUseCase>();
        services.AddTransient<RenameItemUseCase>();
        services.AddTransient<GetRecentItemsUseCase>();
        services.AddTransient<GetMissingItemsUseCase>();
        services.AddTransient<UpdateItemMemoUseCase>();
        services.AddTransient<MoveItemToShelfUseCase>();

        // UseCases - Launch
        services.AddTransient<LaunchItemUseCase>();
        services.AddTransient<OpenParentFolderUseCase>();

        // UseCases - Search
        services.AddTransient<SearchItemsUseCase>();

        return services;
    }

    private static string GetDefaultDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var shelfyPath = Path.Combine(appDataPath, "Shelfy");

        // フォルダが存在しない場合は作成
        if (!Directory.Exists(shelfyPath))
        {
            Directory.CreateDirectory(shelfyPath);
        }

        return Path.Combine(shelfyPath, "shelfy.db");
    }
}
