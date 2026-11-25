using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Engine.Runtime.Tests.Curves;

/// <summary>Builds curves from the keys added, in the order given.</summary>
internal sealed class CurveTestsBuilder
{
    private readonly List<CurveKey> _keys = [];

    public CurveTestsBuilder Key(float time, float value, CurveMode mode = CurveMode.Smooth)
    {
        CurveKey key = new() { Time = time, Value = value, Mode = mode };
        _keys.Add(key);
        return this;
    }

    public CurveTestsBuilder FreeKey(float time, float value, float arrive, float leave)
    {
        CurveKey key = new() { Time = time, Value = value, Mode = CurveMode.Free, In = arrive, Out = leave };
        _keys.Add(key);
        return this;
    }

    public Curve Build() => new(_keys);
}
