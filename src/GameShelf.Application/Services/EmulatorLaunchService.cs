using System.ComponentModel;
using System.Diagnostics;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Extensions;
using GameShelf.Domain.Interfaces;
using GameShelf.Domain.Models;

namespace GameShelf.Application.Services;

/// <summary>
/// Oyunu doğru emülatörle başlatır: doğrulama → argüman üretimi → Process.Start → kayıt.
/// Emülatörün kendisini dağıtmaz/indirmez; yalnızca kullanıcının verdiği exe'yi çalıştırır.
/// </summary>
public sealed class EmulatorLaunchService : IEmulatorLaunchService
{
    private const string Category = nameof(EmulatorLaunchService);

    private readonly ILibraryRepository _repository;
    private readonly ISettingsService _settings;
    private readonly IEmulatorBackendFactory _backends;
    private readonly IProcessLauncher _processLauncher;
    private readonly ILoggingService _logger;
    private readonly IDialogService? _dialogs;
    private readonly IDispatcher? _dispatcher;

    public EmulatorLaunchService(
        ILibraryRepository repository,
        ISettingsService settings,
        IEmulatorBackendFactory backends,
        IProcessLauncher processLauncher,
        ILoggingService logger,
        IDialogService? dialogs = null,
        IDispatcher? dispatcher = null)
    {
        _repository = repository;
        _settings = settings;
        _backends = backends;
        _processLauncher = processLauncher;
        _logger = logger;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
    }

    /// <summary>Emülatörün "hemen kapandı mı?" diye izlendiği süre.</summary>
    private static readonly TimeSpan StartupWatchWindow = TimeSpan.FromSeconds(20);

    public async Task<LaunchValidation> ValidateAsync(Game game, CancellationToken cancellationToken = default)
    {
        var built = await BuildContextAsync(game, cancellationToken).ConfigureAwait(false);

        if (built is null)
        {
            var backend = _backends.Get(game.PlatformId);
            var issues = new List<LaunchIssue>
            {
                LaunchIssue.Error("emulator_not_configured",
                    backend is null
                        ? $"{game.PlatformId.ToShortName()} için emülatör tanımı yok."
                        : $"{backend.DisplayName} yolu ayarlanmamış. Ayarlar → Emülatörler.")
            };
            return new LaunchValidation(issues);
        }

        var (context, emulatorBackend) = built.Value;
        var all = CommonChecks(context).Concat(emulatorBackend.Validate(context)).ToList();
        return new LaunchValidation(all);
    }

    public async Task<LaunchOutcome> LaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        var built = await BuildContextAsync(game, cancellationToken).ConfigureAwait(false);

        if (built is null)
        {
            var failed = await ValidateAsync(game, cancellationToken).ConfigureAwait(false);
            return LaunchOutcome.Fail(failed.Summary, failed.Issues.ToArray());
        }

        var (context, backend) = built.Value;
        var validation = new LaunchValidation(
            CommonChecks(context).Concat(backend.Validate(context)).ToList());

        if (validation.HasErrors)
        {
            _logger.Warning(Category, $"Başlatma engellendi ({game.Title}): {validation.Summary.ReplaceLineEndings(" | ")}");
            await RecordFailureAsync(game, context, validation.Summary, cancellationToken).ConfigureAwait(false);
            return LaunchOutcome.Fail(validation.Summary, validation.Issues.ToArray());
        }

        foreach (var warning in validation.Issues.Where(i => i.Severity == LaunchIssueSeverity.Warning))
        {
            _logger.Warning(Category, $"{game.Title}: {warning.Message}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = context.Emulator.ExecutablePath ?? string.Empty,
            Arguments = backend.BuildArguments(context),
            WorkingDirectory = ResolveWorkingDirectory(context.Emulator),
            UseShellExecute = false
        };

        try
        {
            var process = _processLauncher.Start(startInfo);

            var entry = new LaunchHistoryEntry
            {
                GameId = game.Id,
                PlatformId = game.PlatformId,
                EmulatorPath = startInfo.FileName,
                Arguments = startInfo.Arguments,
                StartedAt = DateTimeOffset.Now
            };

            await _repository.AddLaunchHistoryAsync(entry, cancellationToken).ConfigureAwait(false);

            game.LastPlayedAt = entry.StartedAt;
            await _repository.UpsertGameAsync(game, cancellationToken).ConfigureAwait(false);

            _logger.Info(Category, $"Başlatıldı: {startInfo.FileName} {startInfo.Arguments}");

            // Süre takibi arka planda: çıkışta PlayTimeMinutes güncellenir.
            _ = TrackAsync(process, entry, game.Id);

            // Emülatör birkaç saniye içinde kapanırsa kendi hata mesajını yakala.
            _ = WatchStartupAsync(process, startInfo);

            return LaunchOutcome.Ok(process.Id, startInfo.FileName, startInfo.Arguments,
                validation.Issues.Where(i => i.Severity == LaunchIssueSeverity.Warning).ToArray());
        }
        catch (Win32Exception ex)
        {
            _logger.Error(Category, $"Emülatör başlatılamadı: {startInfo.FileName}", ex);
            await RecordFailureAsync(game, context, ex.Message, cancellationToken).ConfigureAwait(false);
            return LaunchOutcome.Fail($"Emülatör başlatılamadı: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error(Category, "Beklenmeyen başlatma hatası.", ex);
            await RecordFailureAsync(game, context, ex.Message, cancellationToken).ConfigureAwait(false);
            return LaunchOutcome.Fail($"Beklenmeyen hata: {ex.Message}");
        }
    }

    public async Task<string?> BuildCommandPreviewAsync(Game game, CancellationToken cancellationToken = default)
    {
        var built = await BuildContextAsync(game, cancellationToken).ConfigureAwait(false);
        if (built is null)
        {
            return null;
        }

        var (context, backend) = built.Value;
        return $"\"{context.Emulator.ExecutablePath}\" {backend.BuildArguments(context)}";
    }

    // -------------------------------------------------------------- yardımcılar

    private async Task<(EmulatorLaunchContext Context, IEmulatorBackend Backend)?> BuildContextAsync(
        Game game,
        CancellationToken cancellationToken)
    {
        var backend = _backends.Get(game.PlatformId);
        if (backend is null)
        {
            return null;
        }

        var config = await _repository.GetEmulatorConfigAsync(game.PlatformId, cancellationToken).ConfigureAwait(false);
        if (config is null || string.IsNullOrWhiteSpace(config.ExecutablePath))
        {
            return null;
        }

        var platforms = await _repository.GetPlatformsAsync(cancellationToken).ConfigureAwait(false);
        var platform = platforms.FirstOrDefault(p => p.Id == game.PlatformId);

        var gameOverride = await _repository.GetGameOverrideAsync(game.Id, cancellationToken).ConfigureAwait(false);

        _settings.Current.BiosPaths.TryGetValue(game.PlatformId.ToString(), out var biosPath);

        var context = new EmulatorLaunchContext
        {
            Game = game,
            Emulator = config,
            GameOverride = gameOverride,
            BiosPath = string.IsNullOrWhiteSpace(biosPath) ? null : biosPath,
            Fullscreen = gameOverride?.Fullscreen ?? config.DefaultFullscreen,
            ExtraArguments = gameOverride?.ExtraArguments ?? config.ExtraArguments,
            PlatformSupported = platform?.IsSupported ?? game.PlatformId.IsLaunchable()
        };

        return (context, backend);
    }

    private static IEnumerable<LaunchIssue> CommonChecks(EmulatorLaunchContext context)
    {
        if (!context.PlatformSupported)
        {
            yield return LaunchIssue.Error("platform_unsupported",
                $"{context.Game.PlatformId.ToShortName()} bu sürümde desteklenmiyor (emülasyon sağlanmaz).");
        }

        if (string.IsNullOrWhiteSpace(context.Emulator.ExecutablePath))
        {
            yield return LaunchIssue.Error("emulator_not_configured", "Emülatör yolu ayarlanmamış.");
        }
        else if (!File.Exists(context.Emulator.ExecutablePath))
        {
            yield return LaunchIssue.Error("emulator_not_found",
                $"Emülatör bulunamadı: {context.Emulator.ExecutablePath}");
        }

        var target = context.Game.FilePath;

        if (string.IsNullOrWhiteSpace(target))
        {
            yield return LaunchIssue.Error("file_missing", "Oyun yolu boş.");
        }
        else if (Directory.Exists(target))
        {
            if (!SupportsDirectories(context))
            {
                yield return LaunchIssue.Error("directory_target_not_supported",
                    "Bu emülatör klasör hedefi kabul etmiyor.");
            }
        }
        else if (!File.Exists(target))
        {
            yield return LaunchIssue.Error("file_missing", $"Oyun dosyası bulunamadı: {target}");
        }
    }

    private static bool SupportsDirectories(EmulatorLaunchContext context) =>
        context.Game.PlatformId == PlatformId.Ps3;

    private static string ResolveWorkingDirectory(EmulatorConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.WorkingDirectory) && Directory.Exists(config.WorkingDirectory))
        {
            return config.WorkingDirectory;
        }

        return Path.GetDirectoryName(config.ExecutablePath) ?? string.Empty;
    }

    private async Task RecordFailureAsync(
        Game game,
        EmulatorLaunchContext context,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _repository.AddLaunchHistoryAsync(new LaunchHistoryEntry
            {
                GameId = game.Id,
                PlatformId = game.PlatformId,
                EmulatorPath = context.Emulator.ExecutablePath,
                StartedAt = DateTimeOffset.Now,
                EndedAt = DateTimeOffset.Now,
                Success = false,
                Message = message
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(Category, "Başarısız launch kaydı yazılamadı.", ex);
        }
    }

    /// <summary>
    /// Emülatör başladıktan kısa süre sonra kapanırsa konsola yazdığı mesajı yakalar:
    /// log'a yazar ve (bilgi amaçlı) gösterir. Böylece "aynı hata" yerine emülatörün
    /// kendi mesajını görürüz. Emülatörün çalışmasını hiçbir şekilde değiştirmez.
    /// </summary>
    private async Task WatchStartupAsync(ILaunchedProcess process, ProcessStartInfo startInfo)
    {
        try
        {
            using var cts = new CancellationTokenSource(StartupWatchWindow);
            var exitCode = await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var output = (process.StartupOutput ?? string.Empty).Trim();

            if (exitCode == 0 && output.Length == 0)
            {
                return; // temiz kapandı: söylenecek bir şey yok
            }

            var head = output.Length > 1200 ? output[^1200..] : output;

            _logger.Warning(Category,
                $"Emülatör kısa sürede kapandı (kod {exitCode}): {startInfo.FileName}{Environment.NewLine}{head}");

            if (exitCode == 0 || _dialogs is null)
            {
                return;
            }

            var message = output.Length == 0
                ? $"Emülatör başladıktan kısa süre sonra kapandı (çıkış kodu {exitCode}).\n\n" +
                  $"{startInfo.FileName} {startInfo.Arguments}"
                : $"Emülatör kapandı (çıkış kodu {exitCode}). Emülatörün kendi mesajı:\n\n{head}";

            if (_dispatcher is not null)
            {
                await _dispatcher.InvokeAsync(() => _dialogs.ShowMessage("Emülatör kapandı", message))
                    .ConfigureAwait(false);
            }
            else
            {
                _dialogs.ShowMessage("Emülatör kapandı", message);
            }
        }
        catch (OperationCanceledException)
        {
            // pencere boyunca ayakta kaldı: sorun yok, izlemeyi bırak
        }
        catch (Exception ex)
        {
            _logger.Error(Category, "Başlatma izlemesi başarısız.", ex);
        }
    }

    private async Task TrackAsync(ILaunchedProcess process, LaunchHistoryEntry entry, Guid gameId)
    {
        try
        {
            var exitCode = await process.WaitForExitAsync().ConfigureAwait(false);
            var ended = DateTimeOffset.Now;
            var duration = ended - entry.StartedAt;

            entry.EndedAt = ended;
            entry.DurationSeconds = (int)duration.TotalSeconds;
            entry.ExitCode = exitCode;

            await _repository.UpdateLaunchHistoryAsync(entry).ConfigureAwait(false);

            var minutes = (int)Math.Round(duration.TotalMinutes);
            if (minutes > 0)
            {
                await _repository.AddPlayTimeAsync(gameId, minutes).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(Category, "Oyun süresi kaydedilemedi.", ex);
        }
    }
}
