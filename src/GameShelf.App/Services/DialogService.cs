using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using GameShelf.Application.Abstractions;

namespace GameShelf.App.Services;

/// <summary>
/// Dosya/klasör dialogları. ViewModel'ler bu arayüzü kullandığı için test edilebilir kalır.
/// (Klasör dialogu için System.Windows.Forms.FolderBrowserDialog kullanılır.)
/// </summary>
public sealed class DialogService : IDialogService
{
    public string? OpenFile(string title, string filter, string? initialDirectory = null)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory!;
        }

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.FileName : null;
    }

    public string? OpenFolder(string description, string? initialDirectory = null)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory!;
        }

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    public bool Confirm(string title, string message)
        => System.Windows.MessageBox.Show(
               message, title,
               System.Windows.MessageBoxButton.YesNo,
               System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;

    public void ShowMessage(string title, string message)
        => System.Windows.MessageBox.Show(
            message, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
}
