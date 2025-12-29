using Microsoft.Extensions.DependencyInjection;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.Ports.System;
using Shelfy.Core.UseCases.Launch;
using Shelfy.Infrastructure.Persistence;
using Shelfy.Infrastructure.System;

namespace Shelfy.Infrastructure;

/// <summary>
/// DI コンテナへのサービス登録拡張
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Shelfy のすべてのサービスを登録する
    /// </summary>
    public static IServiceCollection AddShelfy(this IServiceCollection services)
    {
        // Ports - System
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IExistenceChecker, FileExistenceChecker>();
        services.AddSingleton<IItemLauncher, Win32ItemLauncher>();
        services.AddSingleton<IHotkeyHoldState, Win32HotkeyHoldState>();

        // Ports - Persistence (InMemory for now)
        services.AddSingleton<IShelfRepository, InMemoryShelfRepository>();
        services.AddSingleton<IItemRepository, InMemoryItemRepository>();

        // UseCases
        services.AddTransient<LaunchItemUseCase>();

        return services;
    }
}
