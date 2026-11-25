namespace FlickerVoid.Engine.Runtime.Curves;

/// <summary>
/// Where a time falls among a curve's keys: the keys it lies between and the ones around them, each with its time moved by
/// the period when the curve wraps; an index of -1 is no key.
/// </summary>
internal readonly record struct CurvePlace
{
    public const int None = -1;

    public float Time { get; init; }

    public int Before { get; init; }

    public float BeforeTime { get; init; }

    public int From { get; init; }

    public float FromTime { get; init; }

    public int To { get; init; }

    public float ToTime { get; init; }

    public int After { get; init; }

    public float AfterTime { get; init; }
}
