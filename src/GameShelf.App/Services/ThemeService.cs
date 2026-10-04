using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Windows;

namespace GameShelf.App.Services;

/// <summary>
/// Tema değiştirici: tema sözlüğünü (Theme.Dark / Theme.Light) çalışma zamanında değiştirir.
/// Tüm fırçalar DynamicResource ile bağlı olduğu için UI kendini yeniler.
/// </summary>
public sealed class ThemeService
{
    private ResourceDictionary? _current;

    public string Current { get; private set; } = "Dark";

    public void Apply(string theme)
    {
        var name = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        Current = name;

        var resources = System.Windows.Application.Current.Resources;
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Themes/Theme.{name}.xaml", UriKind.Relative)
        };

        if (_current is not null)
        {
            resources.MergedDictionaries.Remove(_current);
        }

        resources.MergedDictionaries.Add(dictionary);
        _current = dictionary;
    }
}
