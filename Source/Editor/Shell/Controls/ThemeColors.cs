using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls;

/// <summary>
/// The theme's colours for controls that draw themselves, found by key, and mixed as the design images mix them (CSS
/// <c>color-mix</c> in Oklab), so a drawn control matches its mockup without a colour of its own (rule R13).
/// </summary>
public static class ThemeColors
{
    public static Color Find(StyledElement element, string key)
    {
        if (element.TryFindResource(key, element.ActualThemeVariant, out object? value))
        {
            if (value is Color color)
            {
                return color;
            }

            if (value is ISolidColorBrush brush)
            {
                return brush.Color;
            }
        }

        return FindInApplication(key);
    }

    public static IBrush Brush(StyledElement element, string key)
    {
        if (element.TryFindResource(key, element.ActualThemeVariant, out object? value) && value is IBrush brush)
        {
            return brush;
        }

        Color color = Find(element, key);
        return new SolidColorBrush(color);
    }

    /// <summary>A colour from the application's theme, for code without a control at hand.</summary>
    public static Color FindInApplication(string key)
    {
        if (Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out object? found))
        {
            if (found is Color color)
            {
                return color;
            }

            if (found is ISolidColorBrush brush)
            {
                return brush.Color;
            }
        }

        return Colors.Gray;
    }

    /// <summary>A share of one colour over another, mixed in Oklab as CSS <c>color-mix(in oklab, a share, b)</c> does.</summary>
    public static Color Mix(Color a, Color b, double share)
    {
        (double L, double A, double B, double Alpha) first = ToOklab(a);
        (double L, double A, double B, double Alpha) second = ToOklab(b);
        double rest = 1 - share;
        double alpha = (first.Alpha * share) + (second.Alpha * rest);
        if (alpha <= 0)
        {
            return Colors.Transparent;
        }

        double l = ((first.L * first.Alpha * share) + (second.L * second.Alpha * rest)) / alpha;
        double aa = ((first.A * first.Alpha * share) + (second.A * second.Alpha * rest)) / alpha;
        double bb = ((first.B * first.Alpha * share) + (second.B * second.Alpha * rest)) / alpha;
        return FromOklab(l, aa, bb, alpha);
    }

    /// <summary>The colour at a share of its opacity, as <c>color-mix(in oklab, c share, transparent)</c>.</summary>
    public static Color Fade(Color color, double share)
    {
        byte alpha = (byte)Math.Round(color.A * Math.Clamp(share, 0, 1));
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static (double L, double A, double B, double Alpha) ToOklab(Color color)
    {
        double r = ToLinear(color.R / 255.0);
        double g = ToLinear(color.G / 255.0);
        double b = ToLinear(color.B / 255.0);
        double l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        double m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        double s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
        double lightness = (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s);
        double green = (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s);
        double blue = (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s);
        return (lightness, green, blue, color.A / 255.0);
    }

    private static Color FromOklab(double lightness, double green, double blue, double alpha)
    {
        double l = lightness + (0.3963377774 * green) + (0.2158037573 * blue);
        double m = lightness - (0.1055613458 * green) - (0.0638541728 * blue);
        double s = lightness - (0.0894841775 * green) - (1.2914855480 * blue);
        l = l * l * l;
        m = m * m * m;
        s = s * s * s;
        double r = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        double g = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        double b = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);
        return Color.FromArgb(ToByte(alpha), ToByte(ToGamma(r)), ToByte(ToGamma(g)), ToByte(ToGamma(b)));
    }

    private static double ToLinear(double channel)
    {
        if (channel <= 0.04045)
        {
            return channel / 12.92;
        }

        return Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static double ToGamma(double channel)
    {
        double clamped = Math.Clamp(channel, 0, 1);
        if (clamped <= 0.0031308)
        {
            return clamped * 12.92;
        }

        return (1.055 * Math.Pow(clamped, 1 / 2.4)) - 0.055;
    }

    private static byte ToByte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
