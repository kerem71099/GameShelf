using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Infrastructure.Data;
using GameShelf.Infrastructure.Emulators;
using GameShelf.Infrastructure.Logging;
using GameShelf.Infrastructure.OS;
using GameShelf.Infrastructure.Platform;
using GameShelf.Infrastructure.Plugins;
using GameShelf.Infrastructure.Repositories;
using GameShelf.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GameShelf.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure kayıtları. <paramref name="settingsPath"/> ayar dosyasının tam yoludur.
    /// </summary>
    public static IServiceCollection AddGameShelfInfrastructure(
        this IServiceCollection services,
        AppSettings settings,
        string settingsPath)
    {
        services.AddSingleton(settings);

        services.AddSingleton<ISqliteConnectionFactory>(_ => new SqliteConnectionFactory(settings.DatabasePath));
        services.AddSingleton<DbInitializer>();
        services.AddSingleton<ILibraryRepository, SqliteLibraryRepository>();

        services.AddSingleton<ISettingsService>(provider =>
            new JsonSettingsService(settingsPath, provider.GetRequiredService<ILoggingService>()));

        services.AddSingleton<IPlatformDetector, PlatformDetector>();
        services.AddSingleton<IFileHasher, PartialFileHasher>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IPluginLoader, PluginLoader>();

        // Built-in emülatör adapter'ları (kullanıcı exe yolunu verir; binary dağıtmayız)
        services.AddSingleton<Domain.Interfaces.IEmulatorBackend, DuckStationBackend>();
        services.AddSingleton<Domain.Interfaces.IEmulatorBackend, Pcsx2Backend>();
        services.AddSingleton<Domain.Interfaces.IEmulatorBackend, Rpcs3Backend>();

        services.AddSingleton<IEmulatorBackendFactory>(provider => new EmulatorBackendFactory(
            provider.GetServices<Domain.Interfaces.IEmulatorBackend>(),
            provider.GetRequiredService<ILoggingService>(),
            provider.GetService<IPluginLoader>(),
            provider.GetService<ISettingsService>()));

        return services;
    }
}
