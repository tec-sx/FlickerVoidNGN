using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// A linear RGB colour property, marked with <c>[Color]</c>, picked as the sRGB colour it looks like on screen:
/// <see cref="Color"/> holds the sRGB channels from 0 to 1, in 8-bit steps as a colour picker shows them.
/// </summary>
public sealed partial class ColorFieldViewModel(IEditableValue property) : FieldViewModel(property)
{
    [ObservableProperty]
    public partial Vector3 Color { get; set; }

    protected override void Show(object? value)
    {
        Vector3 linear = (Vector3)value!;
        float red = ToSrgbByte(linear.X) / 255f;
        float green = ToSrgbByte(linear.Y) / 255f;
        float blue = ToSrgbByte(linear.Z) / 255f;
        Color = new Vector3(red, green, blue);
    }

    partial void OnColorChanged(Vector3 value)
    {
        float red = ToLinear(ToByte(value.X));
        float green = ToLinear(ToByte(value.Y));
        float blue = ToLinear(ToByte(value.Z));
        Vector3 linear = new(red, green, blue);
        Write(linear);
    }

    private static byte ToByte(float channel)
    {
        float clamped = Math.Clamp(channel, 0, 1);
        float scaled = MathF.Round(clamped * 255);
        return (byte)scaled;
    }

    private static byte ToSrgbByte(float linear)
    {
        float clamped = Math.Clamp(linear, 0, 1);
        float srgb;
        if (clamped <= 0.0031308f)
        {
            srgb = clamped * 12.92f;
        }
        else
        {
            srgb = (1.055f * MathF.Pow(clamped, 1 / 2.4f)) - 0.055f;
        }

        float scaled = MathF.Round(srgb * 255);
        return (byte)scaled;
    }

    private static float ToLinear(byte value)
    {
        float srgb = value / 255f;
        if (srgb <= 0.04045f)
        {
            return srgb / 12.92f;
        }

        return MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }
}
