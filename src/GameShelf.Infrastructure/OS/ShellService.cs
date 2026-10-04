using System.ComponentModel;
using System.Diagnostics;
using GameShelf.Application.Abstractions;

namespace GameShelf.Infrastructure.OS;

/// <summary>Explorer'da gösterme / klasör açma.</summary>
public sealed class ShellService : IShellService
{
    public void RevealInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // Kullanıcı için kritik değil; sessizce yoksay.
        }
    }

    public void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // yoksay
        }
    }
}
