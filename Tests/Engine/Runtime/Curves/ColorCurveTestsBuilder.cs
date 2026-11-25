using System.Numerics;

using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Engine.Runtime.Tests.Curves;

/// <summary>Builds color curves from the keys added, in the order given.</summary>
internal sealed class ColorCurveTestsBuilder
{
    private readonly List<ColorKey> _keys = [];

    public ColorCurveTestsBuilder Key(float time, float red, float green, float blue, CurveMode mode = CurveMode.Smooth)
    {
        Vector3 color = new(red, green, blue);
        ColorKey key = new() { Time = time, Color = color, Mode = mode };
        _keys.Add(key);
        return this;
    }

    public ColorCurve Build() => new(_keys);
}
