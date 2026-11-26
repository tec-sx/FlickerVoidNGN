using System.Numerics;

using FlickerVoid.Editor.Core.Curves;
using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Tests.Curves;

/// <summary>Builds a color track over a day from keys given as hours and colors, keeping the curves it commits.</summary>
internal sealed class ColorTrackTestsBuilder
{
    public List<ColorCurve> Commits { get; } = [];

    public ColorTrack Build(params (float Time, Vector3 Color)[] keys)
    {
        ColorKey[] built = Array.ConvertAll(keys, key => new ColorKey { Time = key.Time, Color = key.Color });
        ColorCurve curve = new(built);
        return new ColorTrack("Fog color", curve, Commits.Add, 24);
    }
}
