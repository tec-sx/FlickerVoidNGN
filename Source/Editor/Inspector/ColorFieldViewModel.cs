using System.Numerics;

using Avalonia.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>A linear RGB colour property, marked with <c>[Color]</c>, picked as the sRGB colour it looks like on screen.</summary>
public sealed partial class ColorFieldViewModel(ComponentProperty property) : FieldViewModel(property)
{
    [ObservableProperty]
    public partial Color Color { get; set; }

    protected override void Show(object? value)
    {
        Vector3 linear = (Vector3)value!;
        byte red = ToSrgbByte(linear.X);
        byte green = ToSrgbByte(linear.Y);
        byte blue = ToSrgbByte(linear.Z);
        Color = Color.FromRgb(red, green, blue);
    }

    partial void OnColorChanged(Color value)
    {
        float red = ToLinear(value.R);
        float green = ToLinear(value.G);
        float blue = ToLinear(value.B);
        Vector3 linear = new(red, green, blue);
        Write(linear);
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
