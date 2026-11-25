namespace FlickerVoid.Engine.Runtime.Curves;

/// <summary>
/// A number over time, drawn through keys; read against a period it loops, the last key leading on to the first, as the
/// hours of a day do.
/// </summary>
public sealed class Curve
{
    private readonly CurveKey[] _keys = [];
    private readonly float[] _times = [];

    public Curve()
    {
    }

    public Curve(IReadOnlyList<CurveKey> keys) => Keys = keys;

    /// <summary>The keys in order of time; of keys sharing a time, the one given later is the value there.</summary>
    public IReadOnlyList<CurveKey> Keys
    {
        get => _keys;
        init
        {
            // The JSON reader sets every init property, with null for one its file leaves out.
            IEnumerable<CurveKey> keys = value ?? [];
            IOrderedEnumerable<CurveKey> ordered = keys.OrderBy(key => key.Time);
            _keys = [.. ordered];
            _times = Array.ConvertAll(_keys, key => key.Time);
        }
    }

    /// <summary>
    /// The value at a time, 0 without keys: held at the first and last keys outside them, or, with a period (the keys lying
    /// from 0 up to it), wrapped into it.
    /// </summary>
    public float Evaluate(float time, float period = 0)
    {
        if (_keys.Length == 0)
        {
            return 0;
        }

        CurvePlace place = CurveMath.Locate(_times, time, period);
        if (place.To == CurvePlace.None)
        {
            return _keys[place.From].Value;
        }

        CurveKey before = GetKey(place.Before);
        CurveKey after = GetKey(place.After);
        return CurveMath.Blend(place, before, _keys[place.From], _keys[place.To], after);
    }

    private CurveKey GetKey(int index)
    {
        if (index == CurvePlace.None)
        {
            return default;
        }

        return _keys[index];
    }
}
