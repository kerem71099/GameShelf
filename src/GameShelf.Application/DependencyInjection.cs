using GameShelf.Application.Abstractions;
using GameShelf.Application.Services;
using GameShelf.Application.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GameShelf.Application;

public static class DependencyInjection
{
    /// <summary>Application katmanı servisleri + ViewModel'ler.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IGameScannerService, GameScannerService>();
        services.AddSingleton<IEmulatorLaunchService, EmulatorLaunchService>();
        services.AddSingleton<ICoverImageService, CoverImageService>();
        services.AddSingleton<LibraryMaintenanceService>();
        services.AddSingleton<BiosCheckService>();
        services.AddSingleton<EmulatorSetupService>();

        // ViewModel'ler: her navigasyonda yeniden üretilmesi ucuz ve güvenli.
        services.AddTransient<MainViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<GameDetailsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ToolsViewModel>();

        return services;
    }
}
