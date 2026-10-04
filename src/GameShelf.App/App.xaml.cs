using System.IO;
using System.Windows;
using GameShelf.Application;
using GameShelf.Application.Abstractions;
using GameShelf.Application.ViewModels;
using GameShelf.Infrastructure;
using GameShelf.Infrastructure.Data;
using GameShelf.Infrastructure.Emulators;
using GameShelf.Infrastructure.Logging;
using GameShelf.Infrastructure.OS;
using GameShelf.Infrastructure.Settings;
using GameShelf.App.Services;
using GameShelf.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GameShelf.App;

/// <summary>
/// Composition root: DI kurulumu, veritabanı başlatma, tema, global exception handling.
/// </summary>
public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SetupExceptionHandling();

        try
        {
            // 1) Log + ayarlar (DI'den önce: yolları biliyor olmamız gerekir)
            var logger = new FileLoggingService(AppPaths.LogDirectory);
            var preloadedSettings = new JsonSettingsService(AppPaths.SettingsPath, logger);

            var settings = await preloadedSettings.LoadAsync().ConfigureAwait(true);
            AppPaths.EnsureDefaults(settings);
            await preloadedSettings.SaveAsync().ConfigureAwait(true);

            // 2) DI
            var services = new ServiceCollection();
            services.AddSingleton<ILoggingService>(logger);
            services.AddGameShelfInfrastructure(settings, AppPaths.SettingsPath);
            services.AddSingleton<ISettingsService>(preloadedSettings); // önceden yüklenen örnek
            services.AddApplication();
            services.AddSingleton<IDispatcher, WpfDispatcher>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<ThemeService>();

            var provider = services.BuildServiceProvider();
            Services = provider;

            // 3) Veritabanı + plugin'ler
            await provider.GetRequiredService<DbInitializer>().InitializeAsync().ConfigureAwait(true);

            if (provider.GetRequiredService<IEmulatorBackendFactory>() is EmulatorBackendFactory factory)
            {
                factory.LoadPlugins();
            }

            // 4) Tema
            var themeService = provider.GetRequiredService<ThemeService>();
            themeService.Apply(settings.Theme);

            // 5) Ana pencere
            var main = provider.GetRequiredService<MainViewModel>();
            main.ThemeChanged += (_, theme) => themeService.Apply(theme);

            var window = new MainWindow(main)
            {
                Width = settings.Window.Width,
                Height = settings.Window.Height,
                WindowState = settings.Window.IsMaximized ? WindowState.Maximized : WindowState.Normal
            };

            window.Closed += async (_, _) =>
            {
                settings.Window.Width = window.Width;
                settings.Window.Height = window.Height;
                settings.Window.IsMaximized = window.WindowState == WindowState.Maximized;
                await preloadedSettings.SaveAsync();
            };

            MainWindow = window;
            window.Show();

            await main.InitializeAsync().ConfigureAwait(true);

            logger.Info(nameof(App), "GameShelf başlatıldı (offline mod).");
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(AppPaths.LogDirectory, "startup-error.txt");
            Directory.CreateDirectory(AppPaths.LogDirectory);
            File.WriteAllText(logPath, ex.ToString());

            System.Windows.MessageBox.Show(
                $"GameShelf başlatılamadı:\n\n{ex.Message}\n\nAyrıntılar: {logPath}",
                "GameShelf",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    private void SetupExceptionHandling()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            LogAndShow(args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                LogSafe(exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            LogSafe(args.Exception);
        };
    }

    private static void LogAndShow(Exception exception)
    {
        LogSafe(exception);

        System.Windows.MessageBox.Show(
            $"Beklenmeyen bir hata oluştu:\n\n{exception.Message}\n\n" +
            $"Ayrıntılar: {Path.Combine(AppPaths.LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log")}",
            "GameShelf",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    private static void LogSafe(Exception exception)
    {
        try
        {
            if (Services?.GetService<ILoggingService>() is { } logger)
            {
                logger.Error("App", "Yakalanmayan hata.", exception);
            }
            else
            {
                Directory.CreateDirectory(AppPaths.LogDirectory);
                File.AppendAllText(
                    Path.Combine(AppPaths.LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log"),
                    $"{DateTime.Now:O} [ERR] App - {exception}{Environment.NewLine}");
            }
        }
        catch
        {
            // log yazılamıyorsa yapacak bir şey yok
        }
    }
}
