using System.Numerics;

namespace FlickerVoid.Engine.Runtime.Curves;

/// <summary>A color over time, drawn through keys channel by channel as a <see cref="Curve"/> is.</summary>
public sealed class ColorCurve
{
    private readonly ColorKey[] _keys = [];
    private readonly float[] _times = [];

    public ColorCurve()
    {
    }

    public ColorCurve(IReadOnlyList<ColorKey> keys) => Keys = keys;

    /// <summary>The keys in order of time; of keys sharing a time, the one given later is the color there.</summary>
    public IReadOnlyList<ColorKey> Keys
    {
        get => _keys;
        init
        {
            // The JSON reader sets every init property, with null for one its file leaves out.
            IEnumerable<ColorKey> keys = value ?? [];
            IOrderedEnumerable<ColorKey> ordered = keys.OrderBy(key => key.Time);
            _keys = [.. ordered];
            _times = Array.ConvertAll(_keys, key => key.Time);
        }
    }

    /// <summary>
    /// The color at a time, black without keys: held at the first and last keys outside them, or, with a period (the keys
    /// lying from 0 up to it), wrapped into it.
    /// </summary>
    public Vector3 Evaluate(float time, float period = 0)
    {
        if (_keys.Length == 0)
        {
            return Vector3.Zero;
        }

        CurvePlace place = CurveMath.Locate(_times, time, period);
        if (place.To == CurvePlace.None)
        {
            return _keys[place.From].Color;
        }

        float red = EvaluateChannel(place, 0);
        float green = EvaluateChannel(place, 1);
        float blue = EvaluateChannel(place, 2);
        return new Vector3(red, green, blue);
    }

    private float EvaluateChannel(in CurvePlace place, int channel)
    {
        CurveKey before = GetChannelKey(place.Before, channel);
        CurveKey from = GetChannelKey(place.From, channel);
        CurveKey to = GetChannelKey(place.To, channel);
        CurveKey after = GetChannelKey(place.After, channel);
        return CurveMath.Blend(place, before, from, to, after);
    }

    private CurveKey GetChannelKey(int index, int channel)
    {
        if (index == CurvePlace.None)
        {
            return default;
        }

        ColorKey key = _keys[index];
        CurveMode mode = key.Mode == CurveMode.Free ? CurveMode.Smooth : key.Mode;
        return new CurveKey { Value = key.Color[channel], Mode = mode };
    }
}
