using System.Globalization;

using Avalonia.Controls;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls.Mix;

/// <summary>Draws and reads the decibel scales the meters, gain bars and faders share.</summary>
public static class DecibelScale
{
    public const double Floor = -60;

    public static double ToPosition(double decibels, double lowest, double highest)
    {
        double clamped = Math.Clamp(decibels, lowest, highest);
        return (clamped - lowest) / (highest - lowest);
    }

    public static double FromLevel(double level)
    {
        if (level <= 0.0001)
        {
            return Floor;
        }

        return 20 * Math.Log10(level);
    }

    public static IBrush Brush(Control control, string key, IBrush fallback)
    {
        if (control.TryFindResource(key, control.ActualThemeVariant, out object? value) && value is IBrush brush)
        {
            return brush;
        }

        return fallback;
    }

    public static FormattedText Text(string text, double size, IBrush brush)
    {
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);
    }

    /// <summary>A scale mark as written beside a fader: "+6", "0", "-12".</summary>
    public static string Mark(double decibels)
    {
        string number = decibels.ToString("0", CultureInfo.InvariantCulture);
        if (decibels > 0)
        {
            return "+" + number;
        }

        return number;
    }
}
