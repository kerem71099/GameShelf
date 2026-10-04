using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Threading;
using GameShelf.Application.ViewModels;

namespace GameShelf.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        _viewModel.FocusSearchRequested += OnFocusSearchRequested;
    }

    /// <summary>Ctrl+F: önce kütüphaneye dön, sonra arama kutusuna odaklan.</summary>
    private void OnFocusSearchRequested(object? sender, EventArgs e)
    {
        if (ContentHost.Content is LibraryView libraryView)
        {
            libraryView.FocusSearch();
            return;
        }

        _viewModel.ShowLibraryCommand.Execute(null);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ContentHost.Content is LibraryView view)
            {
                view.FocusSearch();
            }
        }), DispatcherPriority.Input);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.FocusSearchRequested -= OnFocusSearchRequested;
        base.OnClosed(e);
    }
}
