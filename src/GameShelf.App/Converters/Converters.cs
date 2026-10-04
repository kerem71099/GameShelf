using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GameShelf.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

/// <summary>Kapak yoksa yer tutucuyu gösterir.</summary>
public sealed class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible ? string.Empty : value as string ?? string.Empty;
}

/// <summary>Kapak varsa gösterir (boş olmayan metin → Visible).</summary>
public sealed class StringNotNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>Platform adını tema fırçasına çevirir (PS1/PS2/PS3 chip renkleri).</summary>
public sealed class PlatformToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value?.ToString() switch
        {
            "PS1" or "Ps1" => "Brush.PlatformPs1",
            "PS2" or "Ps2" => "Brush.PlatformPs2",
            "PS3" or "Ps3" => "Brush.PlatformPs3",
            "PS4" or "Ps4" => "Brush.PlatformPs4",
            "PS5" or "Ps5" => "Brush.PlatformPs5",
            _ => "Brush.PlatformUnknown"
        };

        return System.Windows.Application.Current.TryFindResource(key) as System.Windows.Media.Brush
               ?? System.Windows.Application.Current.TryFindResource("Brush.TextMuted") as System.Windows.Media.Brush
               ?? System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>Dosya boyutunu (byte) okunur metne çevirir.</summary>
public sealed class BytesToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes <= 0)
        {
            return string.Empty;
        }

        return bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.##} GB",
            >= 1024 * 1024 => $"{bytes / (1024d * 1024):0.#} MB",
            _ => $"{bytes / 1024d:0.#} KB"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
