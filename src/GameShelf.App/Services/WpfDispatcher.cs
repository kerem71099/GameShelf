using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Windows.Threading;
using GameShelf.Application.Abstractions;

namespace GameShelf.App.Services;

/// <summary>WPF Dispatcher sarmalayıcısı (ViewModel'ler UI thread'ine böyle döner).</summary>
public sealed class WpfDispatcher : IDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfDispatcher()
        : this(System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
    {
    }

    public WpfDispatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task InvokeAsync(Action action) => _dispatcher.InvokeAsync(action).Task;

    public void Invoke(Action action) => _dispatcher.Invoke(action);
}
