using System.Globalization;
using System.Numerics;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlickerVoid.Editor.Inspector;

/// <summary>Converts a colour field's sRGB channels, 0 to 1, to and from the colour picker's colour.</summary>
public sealed class SrgbColorConverter : IValueConverter
{
    public static SrgbColorConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Vector3 srgb)
        {
            return null;
        }

        return Color.FromRgb(ToByte(srgb.X), ToByte(srgb.Y), ToByte(srgb.Z));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Color color)
        {
            return null;
        }

        return new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
    }

    private static byte ToByte(float channel)
    {
        float clamped = Math.Clamp(channel, 0, 1);
        float scaled = MathF.Round(clamped * 255);
        return (byte)scaled;
    }
}
