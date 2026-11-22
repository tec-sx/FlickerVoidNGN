using System.Numerics;

using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Runtime.Tests.Geometry;

/// <summary>Builds rails through the points added, in order.</summary>
internal sealed class RailTestsBuilder
{
    private readonly List<Vector3> _points = [];

    public RailTestsBuilder Through(float x, float y, float z)
    {
        _points.Add(new Vector3(x, y, z));
        return this;
    }

    public Rail Build() => new(_points);
}
