using CommunityToolkit.Mvvm.ComponentModel;
using GameShelf.Application.Abstractions;

namespace GameShelf.Application.ViewModels;

/// <summary>
/// Tüm ViewModel'lerin tabanı: busy durumu, durum mesajı ve ortak hata yakalama.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    protected readonly ILoggingService Logger;
    protected readonly IDispatcher Dispatcher;

    protected ViewModelBase(ILoggingService logger, IDispatcher dispatcher)
    {
        Logger = logger;
        Dispatcher = dispatcher;
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Uzun işleri tek yerde try/catch ile çalıştırır; UI thread'ini bloke etmez.</summary>
    protected async Task RunBusyAsync(
        Func<CancellationToken, Task> work,
        string? busyMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = busyMessage;

        try
        {
            await work(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "İptal edildi.";
        }
        catch (Exception ex)
        {
            Logger.Error(GetType().Name, "İşlem başarısız.", ex);
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
